using System;

/// <summary>
/// ISO 226:2023 equal-loudness reference for continuous pure tones, not a piano
/// loudness model. The legacy class name is retained for integration compatibility.
/// A piano sample's spectrum, envelope and playback level require separate analysis.
/// Digital gain alone does not establish acoustic SPL or a loudness level in phons.
/// </summary>
/// <remarks>
/// Formula (1) and Table 1, ISO 226:2023, printed pages 2-4:
/// https://cdn.standards.iteh.ai/samples/83117/6afa5bd94e0e4f32812c28c3b0a7b8ac/ISO-226-2023.pdf
/// The standard describes binaural, frontal free-field listening by otologically
/// normal young adults. This utility uses the shared supported range 20-80 phon.
/// Between tabulated frequencies, SPL in dB is interpolated linearly in log frequency.
/// That interpolation is an engineering approximation, not an additional ISO table.
/// </remarks>
public static class pianoLoudnessCompensation
{
    private const double minimumFrequencyHz = 20.0;
    private const double maximumFrequencyHz = 12500.0;
    private const double minimumLoudnessLevelPhon = 20.0;
    private const double maximumLoudnessLevelPhon = 80.0;

    // These are the 2023 coefficients. Do not substitute the older 2003 alpha array.
    private static readonly double[] frequenciesHz =
    {
        20, 25, 31.5, 40, 50, 63, 80, 100, 125, 160,
        200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600,
        2000, 2500, 3150, 4000, 5000, 6300, 8000, 10000, 12500
    };

    private static readonly double[] loudnessExponents =
    {
        0.635, 0.602, 0.569, 0.537, 0.509, 0.482, 0.456, 0.433, 0.412, 0.391,
        0.373, 0.357, 0.343, 0.330, 0.320, 0.311, 0.303, 0.300, 0.295, 0.292,
        0.290, 0.290, 0.289, 0.289, 0.289, 0.293, 0.303, 0.323, 0.354
    };

    private static readonly double[] transferLevelsDb =
    {
        -31.5, -27.2, -23.1, -19.3, -16.1, -13.1, -10.4, -8.2, -6.3, -4.6,
        -3.2, -2.1, -1.2, -0.5, 0.0, 0.4, 0.5, 0.0, -2.7, -4.2,
        -1.2, 1.4, 2.3, 1.0, -2.3, -7.2, -11.2, -10.9, -3.5
    };

    private static readonly double[] hearingThresholdsDb =
    {
        78.1, 68.7, 59.5, 51.1, 44.0, 37.5, 31.5, 26.5, 22.1, 17.9,
        14.4, 11.4, 8.6, 6.2, 4.4, 3.0, 2.2, 2.4, 3.5, 1.7,
        -1.3, -4.2, -6.0, -5.4, -1.5, 6.0, 12.6, 13.9, 12.3
    };

    /// <summary>
    /// Returns pure-tone SPL in dB for the requested loudness level.
    /// Rejects nonfinite inputs and values outside 20-12500 Hz or 20-80 phon.
    /// Exact table nodes use Formula (1); other frequencies use log-frequency
    /// interpolation of the surrounding calculated SPL values.
    /// </summary>
    public static double getSoundPressureLevelDb(double frequencyHz, double loudnessLevelPhon)
    {
        validateFrequency(frequencyHz, nameof(frequencyHz));
        validateLoudnessLevel(loudnessLevelPhon);

        int index = Array.BinarySearch(frequenciesHz, frequencyHz);
        if (index >= 0)
            return getNodeSoundPressureLevelDb(index, loudnessLevelPhon);

        int upperIndex = ~index;
        int lowerIndex = upperIndex - 1;
        double fraction = Math.Log(frequencyHz / frequenciesHz[lowerIndex])
            / Math.Log(frequenciesHz[upperIndex] / frequenciesHz[lowerIndex]);
        double lowerSpl = getNodeSoundPressureLevelDb(lowerIndex, loudnessLevelPhon);
        double upperSpl = getNodeSoundPressureLevelDb(upperIndex, loudnessLevelPhon);
        return lowerSpl + fraction * (upperSpl - lowerSpl);
    }

    /// <summary>
    /// Returns an uncapped linear amplitude ratio relative to a reference pure tone.
    /// Assumes equal uncorrected acoustic SPL at each frequency. This does not
    /// account for sample loudness or the playback device's frequency response.
    /// </summary>
    public static double getRelativeGain(
        double frequencyHz, double loudnessLevelPhon, double referenceFrequencyHz = 1000.0)
    {
        validateFrequency(referenceFrequencyHz, nameof(referenceFrequencyHz));
        double targetSpl = getSoundPressureLevelDb(frequencyHz, loudnessLevelPhon);
        double referenceSpl = getSoundPressureLevelDb(referenceFrequencyHz, loudnessLevelPhon);
        return Math.Pow(10.0, (targetSpl - referenceSpl) / 20.0);
    }

    /// <summary>
    /// Creates an immutable normalization for an entire, fixed stimulus domain.
    /// Every note must subsequently use this same profile. Equal bounds are valid.
    /// The reference frequency may be outside this domain but must be inside ISO's
    /// frequency range. No per-note clipping, boost cap or minimum gain is applied.
    /// </summary>
    public static normalizedGainProfile createNormalizedGainProfile(
        double minFrequencyHz, double maxFrequencyHz, double loudnessLevelPhon,
        double referenceFrequencyHz = 1000.0)
    {
        return new normalizedGainProfile(
            minFrequencyHz, maxFrequencyHz, loudnessLevelPhon, referenceFrequencyHz);
    }

    /// <summary>Returns a bounded gain from a previously fixed normalization profile.</summary>
    public static double getNormalizedGain(
        double frequencyHz, normalizedGainProfile profile, double masterVolume = 1.0)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));
        return profile.getNormalizedGain(frequencyHz, masterVolume);
    }

    /// <summary>
    /// Immutable pure-tone gain reference. Master volume is a common multiplier,
    /// not an acoustic phon calibration. Changing it changes actual listening SPL.
    /// </summary>
    public sealed class normalizedGainProfile
    {
        public double minFrequencyHz { get; private set; }
        public double maxFrequencyHz { get; private set; }
        public double loudnessLevelPhon { get; private set; }
        public double referenceFrequencyHz { get; private set; }
        public double normalizationDb { get; private set; }
        private readonly double referenceSplDb;

        // Validate even assembly-internal construction; callers cannot inject
        // an arbitrary normalization or reference SPL that violates gain bounds.
        internal normalizedGainProfile(
            double minFrequencyHz, double maxFrequencyHz, double loudnessLevelPhon,
            double referenceFrequencyHz)
        {
            validateFrequency(minFrequencyHz, nameof(minFrequencyHz));
            validateFrequency(maxFrequencyHz, nameof(maxFrequencyHz));
            validateFrequency(referenceFrequencyHz, nameof(referenceFrequencyHz));
            validateLoudnessLevel(loudnessLevelPhon);
            if (maxFrequencyHz < minFrequencyHz)
                throw new ArgumentException("Maximum frequency must not be below minimum frequency.", nameof(maxFrequencyHz));
    
            double maxSpl = Math.Max(
                getSoundPressureLevelDb(minFrequencyHz, loudnessLevelPhon),
                getSoundPressureLevelDb(maxFrequencyHz, loudnessLevelPhon));
            // Piecewise-linear dB interpolation cannot exceed both ends of a segment.
            // Domain endpoints AND interior ISO nodes therefore give the exact maximum
            // of this interpolated contour, even when a peak lies inside the domain.
            for (int i = 0; i < frequenciesHz.Length; i++)
            {
                if (frequenciesHz[i] > minFrequencyHz && frequenciesHz[i] < maxFrequencyHz)
                    maxSpl = Math.Max(maxSpl, getNodeSoundPressureLevelDb(i, loudnessLevelPhon));
            }
    
            double referenceSpl = getSoundPressureLevelDb(referenceFrequencyHz, loudnessLevelPhon);
            double normalizationDb = Math.Max(0.0, maxSpl - referenceSpl);
            this.minFrequencyHz = minFrequencyHz;
            this.maxFrequencyHz = maxFrequencyHz;
            this.loudnessLevelPhon = loudnessLevelPhon;
            this.referenceFrequencyHz = referenceFrequencyHz;
            this.referenceSplDb = referenceSpl;
            this.normalizationDb = normalizationDb;
        }

        /// <summary>
        /// Returns masterVolume * 10^((correctionDb-normalizationDb)/20).
        /// Notes outside the declared domain are rejected, including when muted.
        /// Relative gains are preserved; masterVolume must be finite within [0,1].
        /// </summary>
        public double getNormalizedGain(double frequencyHz, double masterVolume = 1.0)
        {
            validateFrequency(frequencyHz, nameof(frequencyHz));
            validateFiniteRange(masterVolume, 0.0, 1.0, nameof(masterVolume));
            if (frequencyHz < minFrequencyHz || frequencyHz > maxFrequencyHz)
                throw new ArgumentOutOfRangeException(
                    nameof(frequencyHz), "Frequency is outside this fixed normalization domain.");

            double correctionDb = getSoundPressureLevelDb(frequencyHz, loudnessLevelPhon) - referenceSplDb;
            return masterVolume * Math.Pow(10.0, (correctionDb - normalizationDb) / 20.0);
        }
    }

    private static double getNodeSoundPressureLevelDb(int index, double loudnessLevelPhon)
    {
        double exponent = loudnessExponents[index];
        double transferDb = transferLevelsDb[index];
        double thresholdDb = hearingThresholdsDb[index];
        double referenceTerm = Math.Pow(4e-10, 0.3 - exponent)
            * (Math.Pow(10.0, 0.03 * loudnessLevelPhon) - Math.Pow(10.0, 0.072));
        double thresholdTerm = Math.Pow(10.0, exponent * (thresholdDb + transferDb) / 10.0);
        return 10.0 / exponent * Math.Log10(referenceTerm + thresholdTerm) - transferDb;
    }

    private static void validateFrequency(double frequencyHz, string parameterName)
    {
        validateFiniteRange(frequencyHz, minimumFrequencyHz, maximumFrequencyHz, parameterName);
    }

    private static void validateLoudnessLevel(double loudnessLevelPhon)
    {
        validateFiniteRange(
            loudnessLevelPhon, minimumLoudnessLevelPhon, maximumLoudnessLevelPhon, nameof(loudnessLevelPhon));
    }

    private static void validateFiniteRange(double value, double minimum, double maximum, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(
                parameterName, "Value must be finite and within [" + minimum + ", " + maximum + "].");
    }
}
