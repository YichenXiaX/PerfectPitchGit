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

    private GameSettings.LevelPreset Preset => GameSettings.Instance.GetCurrentPreset();

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
        StartCoroutine(PlaySequenceCoroutine(frequencies, quadrantIndex));
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
                          $"Pitch Adjust: {pitchRatio:F4}");

                PlayClipAtFrequency(closestClip, freq);
                yield return new WaitForSeconds(preset.noteDuration);
                audioSource.Stop();
                audioSource.loop = false;
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

        audioSource.clip = clip;
        audioSource.pitch = targetFrequency / clipFrequency;
        audioSource.loop = true;
        audioSource.Play();
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
            StartCoroutine(StopAudioAfterDuration(preset.noteDuration));
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

    private IEnumerator StopAudioAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        audioSource.Stop();
        audioSource.loop = false;
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