using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class ToneCloudPlayer : MonoBehaviour
{
    [Header("Cloud Settings")]
    public float toneGain = 0.15f;
    public float toneFadeIn = 0.005f;
    public float toneDuration = 0.1f;
    public float baseFrequency = 440f;
    public float pulseRate = 3f;     // steady 440 Hz tones per second
    public float randomRate = 15f;    // scattered tones per second
    public float minOctaveOffset = 0.5f;
    public float maxOctaveOffset = 2.5f;

    // ©¤©¤ internal ©¤©¤

    private class Tone
    {
        public float frequency;
        public double startTime;
        public float duration;
        public float fadeIn;
        public float maxGain;
        public double phase;
    }

    private readonly List<Tone> tones = new List<Tone>();
    private int sampleRate;
    private double nextCleanup;

    void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;

        // play a silent looping clip so OnAudioFilterRead is called
        var src = GetComponent<AudioSource>();
        src.clip = AudioClip.Create("_silence", sampleRate, 1, sampleRate, false);
        src.loop = true;
        src.Play();
    }


    /// <summary>
    /// Schedule a tone cloud that lasts <paramref name="duration"/> seconds.
    /// </summary>
    public void PlayToneCloud(float duration)
    {
        double now = AudioSettings.dspTime;
        double end = now + duration;

        lock (tones)
        {
            // steady 440 Hz pulse
            double pulseInterval = 1.0 / pulseRate;
            for (double t = now; t < end; t += pulseInterval)
                AddTone(baseFrequency, toneDuration, t);

            // random scattered tones
            double randInterval = 1.0 / randomRate;
            for (double t = now; t < end; t += randInterval)
            {
                float sign = Random.value < 0.5f ? -1f : 1f;
                float oct = minOctaveOffset + Random.value * (maxOctaveOffset - minOctaveOffset);
                float freq = baseFrequency * Mathf.Pow(2f, sign * oct);
                AddTone(freq, toneDuration, t);
            }
        }
    }

    /// <summary>Stop all tones immediately.</summary>
    public void StopCloud()
    {
        lock (tones) { tones.Clear(); }
    }



    private void AddTone(float frequency, float duration, double startTime)
    {
        tones.Add(new Tone
        {
            frequency = frequency,
            startTime = startTime,
            duration = duration,
            fadeIn = toneFadeIn,
            maxGain = toneGain,
            phase = 0
        });
    }

    /// <summary>
    /// Synthesises square-wave tones on the audio thread.
    /// </summary>
    void OnAudioFilterRead(float[] data, int channels)
    {
        double time = AudioSettings.dspTime;
        double dt = 1.0 / sampleRate;

        lock (tones)
        {
            for (int i = 0; i < data.Length; i += channels)
            {
                double t = time + (i / channels) * dt;
                float mix = 0f;

                for (int j = 0; j < tones.Count; j++)
                {
                    Tone tone = tones[j];
                    double elapsed = t - tone.startTime;

                    if (elapsed < 0 || elapsed > tone.duration) continue;

                    // envelope: 5 ms ramp up ¡ú linear ramp down to 0
                    float env;
                    if (elapsed < tone.fadeIn)
                        env = (float)(elapsed / tone.fadeIn) * tone.maxGain;
                    else
                        env = Mathf.Lerp(tone.maxGain, 0f,
                              (float)((elapsed - tone.fadeIn) / (tone.duration - tone.fadeIn)));

                    // square wave
                    tone.phase += tone.frequency * dt;
                    if (tone.phase >= 1.0) tone.phase -= (int)tone.phase;
                    float wave = tone.phase < 0.5 ? 1f : -1f;

                    mix += wave * env;
                }

                for (int c = 0; c < channels; c++)
                    data[i + c] += mix;
            }

            // housekeeping ¡ª remove expired tones every 0.5 s
            if (time > nextCleanup)
            {
                double now = time;
                tones.RemoveAll(tone => now > tone.startTime + tone.duration);
                nextCleanup = time + 0.5;
            }
        }
    }
}