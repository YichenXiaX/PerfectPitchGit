using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class toneCloudPlayer : MonoBehaviour
{
    [Header("Cloud Settings")]
    public float toneGain = 0.06f;
    public float toneFadeIn = 0.03f;
    public float toneDuration = 0.2f;
    public float baseFrequency = 440f;
    public float pulseRate = 3f;     // steady 440 Hz tones per second
    public float randomRate = 8f;    // scattered tones per second
    public float minOctaveOffset = 0.3f;
    public float maxOctaveOffset = 1.5f;

    // ©¤©¤ internal ©¤©¤

    private class tone
    {
        public float frequency;
        public double startTime;
        public float duration;
        public float fadeIn;
        public float maxGain;
        public double phase;
    }

    private readonly List<tone> tones = new List<tone>();
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
    public void playToneCloud(float duration)
    {
        double now = AudioSettings.dspTime;
        double end = now + duration;

        lock (tones)
        {
            // steady 440 Hz pulse
            double pulseInterval = 1.0 / pulseRate;
            for (double t = now; t < end; t += pulseInterval)
                addTone(baseFrequency, toneDuration, t);

            // random scattered tones
            double randInterval = 1.0 / randomRate;
            for (double t = now; t < end; t += randInterval)
            {
                float sign = Random.value < 0.5f ? -1f : 1f;
                float oct = minOctaveOffset + Random.value * (maxOctaveOffset - minOctaveOffset);
                float freq = baseFrequency * Mathf.Pow(2f, sign * oct);
                addTone(freq, toneDuration, t);
            }
        }
    }

    /// <summary>Stop all tones immediately.</summary>
    public void stopCloud()
    {
        lock (tones) { tones.Clear(); }
    }



    private void addTone(float frequency, float duration, double startTime)
    {
        tones.Add(new tone
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
                    tone tone = tones[j];
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
                    //float wave = Mathf.Sin((float)(tone.phase * 2.0 * System.Math.PI));
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