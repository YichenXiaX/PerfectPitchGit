using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FrequencyManager : MonoBehaviour
{
    public class Quadrant
    {
        public float minFrequency;
        public float maxFrequency;
        public float peakFrequency;

        public Quadrant(float min, float max, float peak)
        {
            minFrequency = min;
            maxFrequency = max;
            peakFrequency = peak;
        }
    }

    [Header("Quadrant Frequency Parameters")]
    public List<Quadrant> quadrants = new List<Quadrant>
    {
        new Quadrant(427.65f, 508.57f, 466.16f),
        new Quadrant(359.61f, 427.65f, 392.00f),
        new Quadrant(302.40f, 359.61f, 329.63f),
        new Quadrant(254.29f, 302.40f, 277.18f)
    };

    private int selectedOctaveShift = 0;

     

    [Header("Audio")]
    public AudioSource audioSource;
    public List<AudioClip> pianoSamples;


    [Header("Fade Settings")]                          // ◄◄◄ NEW
    [Tooltip("Duration of the fade-out in seconds")]   // ◄◄◄ NEW
    public float fadeDuration = 0.15f;                 // ◄◄◄ NEW

    [Header("Loudness Compensation")]                                    // ◄◄◄ NEW
    [Range(0f, 1f)]                                                      // ◄◄◄ NEW
    [Tooltip("Minimum volume floor for extreme low/high notes")]         // ◄◄◄ NEW
    public float minCompensationVolume = 0.15f;                          // ◄◄◄ NEW

    [Range(0f, 1f)]                                                      // ◄◄◄ NEW
    [Tooltip("Master volume applied after compensation")]                // ◄◄◄ NEW
    public float masterVolume = 0.8f;                                    // ◄◄◄ NEW

    private float currentNoteVolume = 1f; // tracks the compensated volume for fading // ◄◄◄ NEW

    private GameSettings.LevelPreset Preset => GameSettings.Instance.GetCurrentPreset();

    private Coroutine sequenceCoroutine; //current playing routine

    public void PlayNoteSequence(int quadrantIndex)
    {
        if (quadrantIndex < 0 || quadrantIndex >= quadrants.Count)
        {
            Debug.LogError("Invalid quadrant index!");
            return;
        }

        var preset = Preset;

        selectedOctaveShift = preset.octaveShifts[Random.Range(0, preset.octaveShifts.Length)];

        Quadrant q = quadrants[quadrantIndex];
        float sigma = (q.maxFrequency - q.minFrequency) / 6f;

        List<float> frequencies = new List<float>();
        for (int i = 0; i < preset.notesPerSequence; i++)
        {
            float baseFrequency = SampleGaussian(q.peakFrequency, sigma);
            baseFrequency = Mathf.Clamp(baseFrequency, q.minFrequency, q.maxFrequency);
            float finalFrequency = ApplyOctaveShift(baseFrequency, selectedOctaveShift);
            frequencies.Add(finalFrequency);
        }

        Debug.Log($"=== SEQUENCE START: Quadrant Q{quadrantIndex + 1}, Octave Shift: {selectedOctaveShift} ===");
        sequenceCoroutine = StartCoroutine(PlaySequenceCoroutine(frequencies, quadrantIndex)); //start and store the coroutine
    }



    private IEnumerator PlaySequenceCoroutine(List<float> frequencies, int quadrantIndex)
    {
        var preset = Preset;

        for (int i = 0; i < frequencies.Count; i++)
        {
            float freq = frequencies[i];
            AudioClip closestClip = GetClosestAudioClip(freq);

            if (closestClip != null)
            {
                float clipFreq;
                float.TryParse(closestClip.name, out clipFreq);
                float pitchRatio = freq / clipFreq;

                Debug.Log($"[Note {i + 1}/{frequencies.Count}] " +
                          $"Q{quadrantIndex + 1} | " +
                          $"Octave: {selectedOctaveShift} | " +
                          $"Target: {freq:F2} Hz | " +
                          $"Sample: {closestClip.name} Hz | " +
                          $"Pitch Adjust: {pitchRatio:F4} | " +
                          $"Volume: {currentNoteVolume:F3}");

                PlayClipAtFrequency(closestClip, freq);

                // Sustain then fade, all within noteDuration
                float sustainTime = Mathf.Max(0f, preset.noteDuration - fadeDuration);
                float actualFade = Mathf.Min(fadeDuration, preset.noteDuration);

                yield return new WaitForSeconds(sustainTime);
                yield return StartCoroutine(FadeOutOverDuration(actualFade));

                yield return new WaitForSeconds(preset.pauseBetweenNotes);
            }
        }

        Debug.Log($"=== SEQUENCE END ===");
    }

    private void PlayClipAtFrequency(AudioClip clip, float targetFrequency)
    {
        if (!float.TryParse(clip.name, out float clipFrequency))
        {
            Debug.LogWarning($"AudioClip {clip.name} does not have a valid frequency name!");
            return;
        }


        // ◄◄◄ NEW: compute equal loudness compensation
        //float compensation = PianoLoudnessCompensation.GetVolumeMultiplier(
        //  targetFrequency, minCompensationVolume);

        float compensation = PianoLoudnessCompensation.GetVolumeMultiplier(targetFrequency);
        currentNoteVolume = Mathf.Clamp01(masterVolume * compensation);

        currentNoteVolume = masterVolume * compensation;
        // ◄◄◄ END NEW

        audioSource.clip = clip;
        audioSource.pitch = targetFrequency / clipFrequency;
        audioSource.volume = currentNoteVolume;            // ◄◄◄ CHANGED: was implicitly 1.0
        audioSource.loop = true;
        audioSource.Play();
    }

    private IEnumerator FadeOutOverDuration(float duration)
    {
        float startVolume = audioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Stop();
        audioSource.loop = false;
        audioSource.volume = currentNoteVolume; // restore for next note
    }


    //stops the sequence when guessed correctly
    public void StopSequence()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }
        audioSource.Stop();
        audioSource.loop = false;
    }

    public void GenerateAndPlayFrequency(int quadrantIndex)
    {
        if (quadrantIndex < 0 || quadrantIndex >= quadrants.Count)
        {
            Debug.LogError("Invalid quadrant index!");
            return;
        }

        var preset = Preset;

        Quadrant q = quadrants[quadrantIndex];
        float sigma = (q.maxFrequency - q.minFrequency) / 6f;
        float baseFrequency = SampleGaussian(q.peakFrequency, sigma);
        baseFrequency = Mathf.Clamp(baseFrequency, q.minFrequency, q.maxFrequency);

        selectedOctaveShift = preset.octaveShifts[Random.Range(0, preset.octaveShifts.Length)];
        float finalFrequency = ApplyOctaveShift(baseFrequency, selectedOctaveShift);

        Debug.Log($"Single Note: Q{quadrantIndex + 1} | Octave: {selectedOctaveShift} | Freq: {finalFrequency:F2} Hz");

        AudioClip closestClip = GetClosestAudioClip(finalFrequency);
        if (closestClip != null)
        {
            PlayClipAtFrequency(closestClip, finalFrequency);
            //StartCoroutine(StopAudioAfterDuration(preset.noteDuration));
            StartCoroutine(FadeOutAfterDuration(preset.noteDuration));  // ◄◄◄ CHANGED: was StopAudioAfterDuration
        }
    }

    private AudioClip GetClosestAudioClip(float targetFrequency)
    {
        AudioClip closestClip = null;
        float closestDistance = float.MaxValue;

        foreach (var clip in pianoSamples)
        {
            if (float.TryParse(clip.name, out float clipFrequency))
            {
                float distance = Mathf.Abs(Mathf.Log(targetFrequency / clipFrequency));
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestClip = clip;
                }
            }
        }

        return closestClip;
    }

    private IEnumerator FadeOutAfterDuration(float duration)
    {
        float sustainTime = Mathf.Max(0f, duration - fadeDuration);
        float actualFade = Mathf.Min(fadeDuration, duration);

        yield return new WaitForSeconds(sustainTime);
        yield return StartCoroutine(FadeOutOverDuration(actualFade));
    }

    private float ApplyOctaveShift(float frequency, int octaveShift)
    {
        return frequency * Mathf.Pow(2, octaveShift);
    }

    private float SampleGaussian(float mean, float sigma)
    {
        float u1 = 1.0f - Random.value;
        float u2 = 1.0f - Random.value;
        float z = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        return mean + z * sigma;
    }
}