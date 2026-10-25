using System;
using System.Collections.Generic;

/// <summary>
/// Runtime lookup/inversion of an offline piano loudness response grid. This class
/// does not calculate ISO loudness and does not infer acoustic SPL from digital audio.
/// JSON loading and matching the profile to the actual clips/playback remain external.
/// All logPeakSones values are NATURAL LOGARITHMS (ln), not decibels or log10.
/// The modeled metric is peak over time of the mean per-channel ISO 532-1 loudness
/// histories, after the recorded pitch, duration, gain, fade and pressure calibration.
/// </summary>
public static class pianoLoudnessCalibration
{
    private const double minimumTargetSones = 1e-6;
    private const double fadeTolerance = 1e-5;

    [Serializable]
    public sealed class profileData
    {
        public int schemaVersion;
        public string profileId;
        public string model;
        public float pressurePaPerDigitalUnit;
        public float fadeDuration;
        public float[] durations;
        public float[] gainDb;
        public clipData[] clips;
    }

    [Serializable]
    public sealed class clipData
    {
        public string clipName;
        public float nominalFrequencyHz;
        public int sampleRate;
        public int channels;
        public int frames;
        // Source-frame offset modeled offline and applied before playback; zero is legacy behavior.
        public int playbackStartFrame;
        public float[] pitchRatios;
        // Flattening: ((pitchIndex * durationCount) + durationIndex) * gainCount + gainIndex.
        public float[] logPeakSones;
    }

    public sealed class allowedPitchInterval
    {
        public string clipName { get; private set; }
        public float minPitchRatio { get; private set; }
        public float maxPitchRatio { get; private set; }

        public allowedPitchInterval(string clipName, float minPitchRatio, float maxPitchRatio)
        {
            this.clipName = clipName;
            this.minPitchRatio = minPitchRatio;
            this.maxPitchRatio = maxPitchRatio;
        }
    }

    public readonly struct gainResult
    {
        public double gain { get; }
        public double expectedPeakSones { get; }

        internal gainResult(double gain, double expectedPeakSones)
        {
            this.gain = gain;
            this.expectedPeakSones = expectedPeakSones;
        }
    }

    /// <summary>Validates schema/data and deep-copies all mutable DTO arrays.</summary>
    public static profile createProfile(profileData data)
    {
        return new profile(data);
    }

    public sealed class profile
    {
        public string profileId { get; private set; }
        public string model { get; private set; }
        public float pressurePaPerDigitalUnit { get; private set; }
        public float fadeDuration { get; private set; }

        private readonly double[] durations;
        private readonly double[] gainDb;
        private readonly Dictionary<string, clipProfile> clips;

        internal profile(profileData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.schemaVersion != 1)
                throw new ArgumentException("Unsupported calibration schemaVersion; expected 1.", nameof(data));
            requireName(data.profileId, "profileId");
            requireName(data.model, "model");
            requirePositive(data.pressurePaPerDigitalUnit, "pressurePaPerDigitalUnit");
            requireFinite(data.fadeDuration, "fadeDuration");
            if (data.fadeDuration < 0)
                throw new ArgumentOutOfRangeException("fadeDuration", "Fade duration must be nonnegative.");
            durations = copyAxis(data.durations, "durations", true, 1);
            gainDb = copyAxis(data.gainDb, "gainDb", false, 2);
            if (gainDb[gainDb.Length - 1] != 0.0)
                throw new ArgumentException("gainDb must be strictly increasing and end at 0 dB.", "gainDb");
            if (data.clips == null || data.clips.Length == 0)
                throw new ArgumentException("Calibration must contain at least one clip.", "clips");

            clips = new Dictionary<string, clipProfile>(StringComparer.Ordinal);
            foreach (clipData clip in data.clips)
            {
                if (clip == null) throw new ArgumentException("A clip entry is null.", "clips");
                requireName(clip.clipName, "clipName");
                if (clips.ContainsKey(clip.clipName))
                    throw new ArgumentException("Duplicate calibration clipName: " + clip.clipName, "clips");
                clips.Add(clip.clipName, new clipProfile(clip, durations.Length, gainDb.Length));
            }

            profileId = data.profileId;
            model = data.model;
            pressurePaPerDigitalUnit = data.pressurePaPerDigitalUnit;
            fadeDuration = data.fadeDuration;
        }

        /// <summary>Returns the validated, immutable source-frame start for this clip.</summary>
        public int getPlaybackStartFrame(string clipName)
        {
            return getClip(clipName).playbackStartFrame;
        }

        /// <summary>
        /// Fixes one common loudness target over ALL supplied clip/pitch intervals.
        /// Build these intervals from the full experimental condition, never from
        /// the next random sequence. Changing clips, octaves, duration, fade or
        /// master volume requires explicitly creating a new condition.
        /// </summary>
        public condition createCondition(
            IReadOnlyList<allowedPitchInterval> intervals,
            float duration, float fadeDuration, float masterVolume)
        {
            return new condition(this, intervals, duration, fadeDuration, masterVolume);
        }

        /// <summary>
        /// Diagnostic grid query: trilinear interpolation of ln(peak sones) using
        /// ln(pitch), linear duration, and linear digital gain in dB.
        /// Returns the interpolated natural logarithm, not the loudness in sones.
        /// </summary>
        public double getLogPeakSones(string clipName, float pitchRatio, float duration, double digitalGainDb)
        {
            clipProfile clip = getClip(clipName);
            clip.validatePitch(pitchRatio);
            validateDuration(duration);
            requireRange(digitalGainDb, gainDb[0], gainDb[gainDb.Length - 1], nameof(digitalGainDb));
            return evaluate(clip, pitchRatio, duration, digitalGainDb);
        }

        internal clipProfile getClip(string clipName)
        {
            requireName(clipName, nameof(clipName));
            clipProfile clip;
            if (!clips.TryGetValue(clipName, out clip))
                throw new ArgumentException("Clip is missing from the calibration: " + clipName, nameof(clipName));
            return clip;
        }

        internal void validateDuration(double duration)
        {
            requireRange(duration, durations[0], durations[durations.Length - 1], nameof(duration));
        }

        internal int gainCount { get { return gainDb.Length; } }
        internal double minimumGainDb { get { return gainDb[0]; } }
        internal double getGainNode(int index) { return gainDb[index]; }

        internal double evaluate(clipProfile clip, double pitchRatio, double duration, double digitalGainDb)
        {
            axisPosition pitch = clip.getPitchPosition(pitchRatio);
            axisPosition time = locate(durations, duration);
            axisPosition gain = locate(gainDb, digitalGainDb);
            double lowPitchLowTime = interpolate(
                clip.getResponse(pitch.lower, time.lower, gain.lower),
                clip.getResponse(pitch.lower, time.lower, gain.upper), gain.fraction);
            double lowPitchHighTime = interpolate(
                clip.getResponse(pitch.lower, time.upper, gain.lower),
                clip.getResponse(pitch.lower, time.upper, gain.upper), gain.fraction);
            double highPitchLowTime = interpolate(
                clip.getResponse(pitch.upper, time.lower, gain.lower),
                clip.getResponse(pitch.upper, time.lower, gain.upper), gain.fraction);
            double highPitchHighTime = interpolate(
                clip.getResponse(pitch.upper, time.upper, gain.lower),
                clip.getResponse(pitch.upper, time.upper, gain.upper), gain.fraction);
            return interpolate(
                interpolate(lowPitchLowTime, lowPitchHighTime, time.fraction),
                interpolate(highPitchLowTime, highPitchHighTime, time.fraction), pitch.fraction);
        }
    }

    public sealed class condition
    {
        public double targetPeakSones { get; private set; }
        public float duration { get; private set; }
        public float fadeDuration { get; private set; }
        public float masterVolume { get; private set; }
        public bool isMuted { get; private set; }

        private readonly profile source;
        private readonly pitchInterval[] intervals;
        private readonly double masterGainDb;
        private readonly double targetLogSones;

        internal condition(
            profile source, IReadOnlyList<allowedPitchInterval> allowedIntervals,
            float duration, float fadeDuration, float masterVolume)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.validateDuration(duration);
            requireFinite(fadeDuration, nameof(fadeDuration));
            if (fadeDuration < 0 || Math.Abs((double)fadeDuration - source.fadeDuration) > fadeTolerance)
                throw new ArgumentException("Fade duration differs from the calibration profile.", nameof(fadeDuration));
            requireRange(masterVolume, 0, 1, nameof(masterVolume));
            if (allowedIntervals == null) throw new ArgumentNullException(nameof(allowedIntervals));
            if (allowedIntervals.Count == 0)
                throw new ArgumentException("The fixed condition must declare at least one clip/pitch interval.", nameof(allowedIntervals));

            intervals = new pitchInterval[allowedIntervals.Count];
            for (int i = 0; i < allowedIntervals.Count; i++)
            {
                allowedPitchInterval allowed = allowedIntervals[i];
                if (allowed == null) throw new ArgumentException("An allowed interval is null.", nameof(allowedIntervals));
                clipProfile clip = source.getClip(allowed.clipName);
                clip.validatePitch(allowed.minPitchRatio);
                clip.validatePitch(allowed.maxPitchRatio);
                if (allowed.minPitchRatio > allowed.maxPitchRatio)
                    throw new ArgumentException("An allowed pitch interval is reversed.", nameof(allowedIntervals));
                intervals[i] = new pitchInterval(clip, allowed.minPitchRatio, allowed.maxPitchRatio);
            }

            this.source = source;
            this.duration = duration;
            this.fadeDuration = fadeDuration;
            this.masterVolume = masterVolume;
            isMuted = masterVolume == 0;
            if (isMuted)
            {
                masterGainDb = double.NegativeInfinity;
                targetLogSones = double.NegativeInfinity;
                targetPeakSones = 0;
                return;
            }

            masterGainDb = 20.0 * Math.Log10(masterVolume);
            requireRange(masterGainDb, source.minimumGainDb, 0, nameof(masterVolume));
            double target = double.PositiveInfinity;
            double minimumReachableTarget = double.NegativeInfinity;
            foreach (pitchInterval interval in intervals)
            {
                includeCandidate(interval.clip, interval.minimum, ref target, ref minimumReachableTarget);
                includeCandidate(interval.clip, interval.maximum, ref target, ref minimumReachableTarget);
                for (int i = 0; i < interval.clip.pitchCount; i++)
                {
                    double pitch = interval.clip.getPitchNode(i);
                    if (pitch > interval.minimum && pitch < interval.maximum)
                        includeCandidate(interval.clip, pitch, ref target, ref minimumReachableTarget);
                }
            }

            if (target <= Math.Log(minimumTargetSones))
                throw new InvalidOperationException("Common target is at or below the model threshold of 1e-6 sones.");
            if (target < minimumReachableTarget)
                throw new InvalidOperationException(
                    "No common target is reachable within the calibration gain grid. Extend the grid to lower gains.");
            targetLogSones = target;
            targetPeakSones = Math.Exp(target);
            if (double.IsInfinity(targetPeakSones) || double.IsNaN(targetPeakSones))
                throw new InvalidOperationException("The calibration target is not representable as finite sones.");
        }

        private void includeCandidate(
            clipProfile clip, double pitch, ref double target, ref double minimumReachableTarget)
        {
            // At fixed duration/gain the interpolant is piecewise linear in ln(pitch).
            // Therefore endpoints and interior pitch-grid nodes contain every extremum.
            target = Math.Min(target, source.evaluate(clip, pitch, duration, masterGainDb));
            minimumReachableTarget = Math.Max(minimumReachableTarget,
                source.evaluate(clip, pitch, duration, source.minimumGainDb));
        }

        /// <summary>
        /// Inverts the monotonic response at this note's pitch and fixed duration.
        /// Returns the quietest modeled gain attaining the target on a plateau.
        /// Does not clip individual gains or renormalize against queried/random notes.
        /// A muted condition returns gain 0 and expectedPeakSones 0 after validation.
        /// </summary>
        public gainResult getGain(string clipName, float pitchRatio)
        {
            clipProfile clip = source.getClip(clipName);
            clip.validatePitch(pitchRatio);
            bool allowed = false;
            foreach (pitchInterval interval in intervals)
                if (ReferenceEquals(interval.clip, clip) && pitchRatio >= interval.minimum && pitchRatio <= interval.maximum)
                {
                    allowed = true;
                    break;
                }
            if (!allowed)
                throw new ArgumentOutOfRangeException(nameof(pitchRatio), "Note is outside the fixed condition's declared intervals.");
            if (isMuted) return new gainResult(0, 0);

            double lowerGainDb = source.minimumGainDb;
            double lowerResponse = source.evaluate(clip, pitchRatio, duration, lowerGainDb);
            double maximumResponse = source.evaluate(clip, pitchRatio, duration, masterGainDb);
            if (targetLogSones < lowerResponse || targetLogSones > maximumResponse)
                throw new InvalidOperationException("The note cannot reach the fixed target within the modeled gain interval.");
            if (targetLogSones == lowerResponse)
                return makeResult(lowerGainDb);

            for (int i = 1; i < source.gainCount; i++)
            {
                double upperGainDb = Math.Min(source.getGainNode(i), masterGainDb);
                if (upperGainDb <= lowerGainDb) break;
                double upperResponse = source.evaluate(clip, pitchRatio, duration, upperGainDb);
                if (targetLogSones <= upperResponse)
                {
                    double fraction = (targetLogSones - lowerResponse) / (upperResponse - lowerResponse);
                    return makeResult(interpolate(lowerGainDb, upperGainDb, fraction));
                }
                lowerGainDb = upperGainDb;
                lowerResponse = upperResponse;
                if (upperGainDb == masterGainDb) break;
            }
            throw new InvalidOperationException("No monotonic gain segment brackets the fixed target.");
        }

        private gainResult makeResult(double resolvedGainDb)
        {
            // Express the answer relative to the exact supplied master value, so
            // an endpoint solution equals master rather than a log/pow round-trip.
            double gain = masterVolume * Math.Pow(10.0, (resolvedGainDb - masterGainDb) / 20.0);
            if (double.IsNaN(gain) || double.IsInfinity(gain) || gain <= 0 || gain > masterVolume)
                throw new InvalidOperationException("Solved gain is outside the representable condition bounds.");
            return new gainResult(gain, targetPeakSones);
        }
    }

    internal sealed class clipProfile
    {
        internal readonly int playbackStartFrame;
        private readonly double[] pitches;
        private readonly double[] logPitches;
        private readonly double[] responses;
        private readonly int durationCount;
        private readonly int gainCount;

        internal clipProfile(clipData data, int durationCount, int gainCount)
        {
            requirePositive(data.nominalFrequencyHz, "nominalFrequencyHz");
            if (data.sampleRate <= 0 || data.channels <= 0 || data.frames <= 0)
                throw new ArgumentException("Clip sampleRate, channels and frames must be positive.", "clips");
            if (data.playbackStartFrame < 0 || data.playbackStartFrame >= data.frames)
                throw new ArgumentOutOfRangeException("playbackStartFrame", "Clip playbackStartFrame must be nonnegative and less than frames.");
            playbackStartFrame = data.playbackStartFrame;
            pitches = copyAxis(data.pitchRatios, "pitchRatios", true, 1);
            logPitches = new double[pitches.Length];
            for (int i = 0; i < pitches.Length; i++) logPitches[i] = Math.Log(pitches[i]);
            long expectedCount = (long)pitches.Length * durationCount * gainCount;
            if (data.logPeakSones == null || data.logPeakSones.LongLength != expectedCount)
                throw new ArgumentException("logPeakSones has incorrect pitch/duration/gain dimensions.", "logPeakSones");
            responses = new double[data.logPeakSones.Length];
            for (int i = 0; i < responses.Length; i++)
            {
                double value = data.logPeakSones[i];
                requireFinite(value, "logPeakSones");
                if (value > Math.Log(double.MaxValue))
                    throw new ArgumentException("logPeakSones implies unrepresentable loudness.", "logPeakSones");
                responses[i] = value;
            }
            this.durationCount = durationCount;
            this.gainCount = gainCount;
            for (int p = 0; p < pitches.Length; p++)
            for (int d = 0; d < durationCount; d++)
            for (int g = 1; g < gainCount; g++)
                if (getResponse(p, d, g) < getResponse(p, d, g - 1))
                    throw new ArgumentException("Gain response must be nondecreasing at every pitch/duration node.", "logPeakSones");
        }

        internal int pitchCount { get { return pitches.Length; } }
        internal double getPitchNode(int index) { return pitches[index]; }
        internal void validatePitch(double pitch)
        {
            requireRange(pitch, pitches[0], pitches[pitches.Length - 1], "pitchRatio");
        }
        internal axisPosition getPitchPosition(double pitch) { return locate(logPitches, Math.Log(pitch)); }
        internal double getResponse(int pitch, int duration, int gain)
        {
            return responses[(pitch * durationCount + duration) * gainCount + gain];
        }
    }

    internal readonly struct pitchInterval
    {
        internal readonly clipProfile clip;
        internal readonly double minimum;
        internal readonly double maximum;
        internal pitchInterval(clipProfile clip, double minimum, double maximum)
        {
            this.clip = clip;
            this.minimum = minimum;
            this.maximum = maximum;
        }
    }

    internal readonly struct axisPosition
    {
        internal readonly int lower;
        internal readonly int upper;
        internal readonly double fraction;
        internal axisPosition(int lower, int upper, double fraction)
        {
            this.lower = lower;
            this.upper = upper;
            this.fraction = fraction;
        }
    }

    private static axisPosition locate(double[] axis, double value)
    {
        int index = Array.BinarySearch(axis, value);
        if (index >= 0) return new axisPosition(index, index, 0);
        int upper = ~index;
        if (upper == 0 || upper == axis.Length)
            throw new ArgumentOutOfRangeException(nameof(value), "Interpolation would require extrapolation.");
        int lower = upper - 1;
        return new axisPosition(lower, upper, (value - axis[lower]) / (axis[upper] - axis[lower]));
    }

    private static double interpolate(double lower, double upper, double fraction)
    {
        return lower + fraction * (upper - lower);
    }

    private static double[] copyAxis(float[] data, string name, bool positive, int minimumCount)
    {
        if (data == null || data.Length < minimumCount)
            throw new ArgumentException("Axis has insufficient values: " + name, name);
        double[] copy = new double[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            requireFinite(data[i], name);
            if (positive && data[i] <= 0)
                throw new ArgumentOutOfRangeException(name, "Axis values must be positive.");
            if (i > 0 && data[i] <= data[i - 1])
                throw new ArgumentException("Axis must be strictly increasing: " + name, name);
            copy[i] = data[i];
        }
        return copy;
    }

    private static void requireName(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Identifier must be nonempty.", name);
    }
    private static void requireFinite(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name, "Value must be finite.");
    }
    private static void requirePositive(double value, string name)
    {
        requireFinite(value, name);
        if (value <= 0) throw new ArgumentOutOfRangeException(name, "Value must be positive.");
    }
    private static void requireRange(double value, double minimum, double maximum, string name)
    {
        requireFinite(value, name);
        if (value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(name, "Value is outside the calibrated range.");
    }
}
