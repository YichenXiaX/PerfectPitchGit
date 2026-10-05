using UnityEngine;

public static class PianoLoudnessCompensation
{
    // ISO 226:2003 Equal Loudness Contour at 60 phons
    private static readonly float[] isoFreq = {
        20f, 25f, 31.5f, 40f, 50f, 63f, 80f, 100f, 125f, 160f,
        200f, 250f, 315f, 400f, 500f, 630f, 800f, 1000f, 1250f, 1600f,
        2000f, 2500f, 3150f, 4000f, 5000f, 6300f, 8000f, 10000f, 12500f
    };

    private static readonly float[] isoSPL = {
        108.0f, 103.0f, 97.0f, 92.0f, 87.0f, 83.0f, 79.0f, 75.0f, 72.0f, 69.0f,
         66.0f,  64.0f, 62.0f, 61.0f, 60.0f, 59.0f, 59.0f, 60.0f, 60.0f, 59.0f,
         57.0f,  55.0f, 54.0f, 55.0f, 57.0f, 62.0f, 65.0f, 72.0f, 78.0f
    };

    private const float REFERENCE_SPL = 60f; // SPL at 1 kHz for 60 phon

    /// <summary>
    /// Returns a volume multiplier relative to 1 kHz.
    /// Values > 1.0 mean "play louder" (ear is less sensitive here).
    /// Values < 1.0 mean "play quieter" (ear is more sensitive here).
    /// maxBoostDB caps the extreme low end to prevent clipping.
    /// </summary>
    public static float GetVolumeMultiplier(float frequencyHz, float maxBoostDB = 12f)
    {
        float spl = InterpolateSPL(frequencyHz);

        // Positive = ear is less sensitive = needs louder playback
        // Negative = ear is more sensitive = needs quieter playback
        float compensationDB = spl - REFERENCE_SPL;

        // Clamp to prevent extreme boosts at very low frequencies
        compensationDB = Mathf.Clamp(compensationDB, -maxBoostDB, maxBoostDB);

        return Mathf.Pow(10f, compensationDB / 20f);
    }

    private static float InterpolateSPL(float freq)
    {
        if (freq <= isoFreq[0]) return isoSPL[0];
        if (freq >= isoFreq[isoFreq.Length - 1]) return isoSPL[isoSPL.Length - 1];

        for (int i = 0; i < isoFreq.Length - 1; i++)
        {
            if (freq >= isoFreq[i] && freq <= isoFreq[i + 1])
            {
                float logF = Mathf.Log10(freq);
                float logF1 = Mathf.Log10(isoFreq[i]);
                float logF2 = Mathf.Log10(isoFreq[i + 1]);
                float t = (logF - logF1) / (logF2 - logF1);
                return Mathf.Lerp(isoSPL[i], isoSPL[i + 1], t);
            }
        }

        return REFERENCE_SPL;
    }
}