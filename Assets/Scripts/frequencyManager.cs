using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public class frequencyManager : MonoBehaviour
{
    public class quadrant
    {
        public float minFrequency;
        public float maxFrequency;
        public float peakFrequency;

        public quadrant(float min, float max, float peak)
        {
            minFrequency = min;
            maxFrequency = max;
            peakFrequency = peak;
        }
    }

    [Header("Quadrant Frequency Parameters")]
    public List<quadrant> quadrants = new List<quadrant>
    {
        new quadrant(427.65f, 508.57f, 466.16f),
        new quadrant(359.61f, 427.65f, 392.00f),
        new quadrant(302.40f, 359.61f, 329.63f),
        new quadrant(254.29f, 302.40f, 277.18f)
    };

    [Header("Audio")]
    public AudioSource audioSource;
    public List<AudioClip> pianoSamples;

    [Header("Fade Settings")]
    [Tooltip("The calibration profile must be generated for this fade duration.")]
    public float fadeDuration = 0.15f;

    [Header("Loudness Calibration")]
    [SerializeField] private TextAsset loudnessCalibrationData = null;
    [Range(0f, 1f)]
    [Tooltip("Maximum source volume. All notes share a feasible modeled loudness target.")]
    public float masterVolume = 0.8f;

    private int selectedOctaveShift;
    private float currentNoteVolume;
    private gameSettings.levelPreset preset => gameSettings.instance.getCurrentPreset();
    private TextAsset loadedCalibrationAsset;
    private pianoLoudnessCalibration.profileData calibrationData;
    private pianoLoudnessCalibration.profile calibrationProfile;
    private pianoLoudnessCalibration.condition calibrationCondition;
    private string conditionKey;
    private string lastCalibrationError;
    private float sequenceStartTime;
    private audioLoudnessRecord currentLoudnessRecord;

    /// <summary>Validate before starting a session, so bad data cannot silently produce uncalibrated trials.</summary>
    public bool tryPrepareLoudness()
    {
        try
        {
            prepareLoudness();
            lastCalibrationError = null;
            return true;
        }
        catch (Exception error)
        {
            string message = "Note loudness calibration is unavailable: " + error.Message;
            if (message != lastCalibrationError) Debug.LogError(message, this);
            lastCalibrationError = message;
            return false;
        }
    }

    private void prepareLoudness()
    {
        if (audioSource == null) throw new InvalidOperationException("Assign the note AudioSource.");
        if (loudnessCalibrationData == null) throw new InvalidOperationException("Assign a generated loudness profile.");
        if (calibrationProfile == null || loadedCalibrationAsset != loudnessCalibrationData)
        {
            calibrationData = JsonUtility.FromJson<pianoLoudnessCalibration.profileData>(loudnessCalibrationData.text);
            calibrationProfile = pianoLoudnessCalibration.createProfile(calibrationData);
            loadedCalibrationAsset = loudnessCalibrationData;
            conditionKey = null;
        }

        var settings = preset;
        if (settings.octaveShifts == null || settings.octaveShifts.Length == 0)
            throw new InvalidOperationException("Choose at least one octave.");
        if (pianoSamples == null || pianoSamples.Count == 0)
            throw new InvalidOperationException("Assign the note recordings.");

        var sorted = new List<AudioClip>(pianoSamples);
        if (sorted.Exists(clip => clip == null)) throw new InvalidOperationException("A note recording is missing.");
        sorted.Sort((left, right) => getClipFrequency(left).CompareTo(getClipFrequency(right)));
        var key = new StringBuilder();
        key.Append(settings.noteDuration.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(fadeDuration.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(masterVolume.ToString("R", CultureInfo.InvariantCulture));
        foreach (int shift in settings.octaveShifts) key.Append('|').Append(shift);
        foreach (var q in quadrants)
        {
            if (q == null || q.minFrequency <= 0 || q.maxFrequency < q.minFrequency
                || float.IsNaN(q.minFrequency) || float.IsNaN(q.maxFrequency)
                || float.IsInfinity(q.minFrequency) || float.IsInfinity(q.maxFrequency))
                throw new InvalidOperationException("Invalid quadrant frequency limits.");
            key.Append('|').Append(q.minFrequency.ToString("R", CultureInfo.InvariantCulture))
                .Append(':').Append(q.maxFrequency.ToString("R", CultureInfo.InvariantCulture));
        }
        foreach (var clip in sorted) key.Append('|').Append(clip.GetInstanceID());
        string nextKey = key.ToString();
        if (nextKey == conditionKey) return;

        var intervals = new List<pianoLoudnessCalibration.allowedPitchInterval>();
        for (int i = 0; i < sorted.Count; i++)
        {
            var clip = sorted[i];
            float nominal = getClipFrequency(clip);
            var entry = Array.Find(calibrationData.clips, data => data.clipName == clip.name);
            if (entry == null || !matchesRecordingDimensions(entry, clip))
                throw new InvalidOperationException("Regenerate calibration for recording " + clip.name + ".");
            double cellMin = i == 0 ? 0 : Math.Sqrt(getClipFrequency(sorted[i - 1]) * (double)nominal);
            double cellMax = i == sorted.Count - 1 ? double.PositiveInfinity : Math.Sqrt(nominal * (double)getClipFrequency(sorted[i + 1]));
            foreach (int shift in settings.octaveShifts)
            {
                double octave = Math.Pow(2, shift);
                foreach (var q in quadrants)
                {
                    double low = Math.Max(cellMin, q.minFrequency * octave);
                    double high = Math.Min(cellMax, q.maxFrequency * octave);
                    if (low > high) continue;
                    // Unity caps positive AudioSource pitch at 3. Model the actual
                    // playback rate, including the existing extreme-octave behavior.
                    float minPitch = (float)Math.Min(low / nominal, 3.0);
                    float maxPitch = (float)Math.Min(high / nominal, 3.0);
                    intervals.Add(new pianoLoudnessCalibration.allowedPitchInterval(clip.name, minPitch, maxPitch));
                }
            }
        }
        calibrationCondition = calibrationProfile.createCondition(
            intervals.ToArray(), settings.noteDuration, fadeDuration, masterVolume);
        conditionKey = nextKey;
    }

    private static bool matchesRecordingDimensions(pianoLoudnessCalibration.clipData entry, AudioClip clip)
    {
        if (entry.channels != clip.channels) return false;
#if UNITY_WEBGL && !UNITY_EDITOR
        // Web Audio may resample decoded PCM to the browser's output rate.
        // Source hashes/import settings are checked in the Editor before builds.
        double expectedDuration = entry.frames / (double)entry.sampleRate;
        double actualDuration = clip.samples / (double)clip.frequency;
        return Math.Abs(expectedDuration - actualDuration) <= 2.0 / Math.Min(entry.sampleRate, clip.frequency);
#else
        return entry.frames == clip.samples && entry.sampleRate == clip.frequency;
#endif
    }

    private static float getClipFrequency(AudioClip clip)
    {
        if (clip == null || !float.TryParse(clip.name, NumberStyles.Float, CultureInfo.InvariantCulture, out float frequency)
            || frequency <= 0 || float.IsNaN(frequency) || float.IsInfinity(frequency))
            throw new InvalidOperationException("Note recordings must have positive frequency names.");
        return frequency;
    }

    private void beginLoudnessRecord()
    {
        sequenceStartTime = Time.time;
        currentLoudnessRecord = new audioLoudnessRecord
        {
            profileId = calibrationData.profileId,
            model = calibrationData.model,
            pressurePaPerDigitalUnit = calibrationData.pressurePaPerDigitalUnit,
            noteDurationSeconds = calibrationCondition.duration,
            fadeDurationSeconds = calibrationCondition.fadeDuration,
            masterVolume = calibrationCondition.masterVolume,
            plannedTargetPeakSones = calibrationCondition.targetPeakSones
        };
    }

    public audioLoudnessRecord createLoudnessRecord()
    {
        if (currentLoudnessRecord == null) return null;
        return new audioLoudnessRecord
        {
            profileId = currentLoudnessRecord.profileId,
            model = currentLoudnessRecord.model,
            playbackCalibration = currentLoudnessRecord.playbackCalibration,
            pressurePaPerDigitalUnit = currentLoudnessRecord.pressurePaPerDigitalUnit,
            noteDurationSeconds = currentLoudnessRecord.noteDurationSeconds,
            fadeDurationSeconds = currentLoudnessRecord.fadeDurationSeconds,
            masterVolume = currentLoudnessRecord.masterVolume,
            plannedTargetPeakSones = currentLoudnessRecord.plannedTargetPeakSones,
            notes = currentLoudnessRecord.notes.ConvertAll(note => new notePlaybackRecord
            {
                clipName = note.clipName,
                requestedFrequencyHz = note.requestedFrequencyHz,
                nominalPlaybackFrequencyHz = note.nominalPlaybackFrequencyHz,
                pitchRatio = note.pitchRatio,
                playbackStartFrame = note.playbackStartFrame,
                playbackSampleRate = note.playbackSampleRate,
                sourceVolume = note.sourceVolume,
                plannedPeakSones = note.plannedPeakSones,
                plannedDurationSeconds = note.plannedDurationSeconds,
                requestedTimeInSequenceSeconds = note.requestedTimeInSequenceSeconds,
                elapsedSecondsAtResult = Mathf.Clamp(Time.time - sequenceStartTime - note.requestedTimeInSequenceSeconds, 0f, note.plannedDurationSeconds),
                resultBeforePlannedEnd = Time.time - sequenceStartTime - note.requestedTimeInSequenceSeconds < note.plannedDurationSeconds
            })
        };
    }

    public void playNoteSequence(int quadrantIndex)
    {
        if (quadrantIndex < 0 || quadrantIndex >= quadrants.Count)
            throw new ArgumentOutOfRangeException(nameof(quadrantIndex));
        prepareLoudness();
        stopSequence();
        beginLoudnessRecord();

        var settings = preset;
        selectedOctaveShift = settings.octaveShifts[UnityEngine.Random.Range(0, settings.octaveShifts.Length)];
        quadrant q = quadrants[quadrantIndex];
        float sigma = (q.maxFrequency - q.minFrequency) / 6f;
        var frequencies = new List<float>();
        for (int i = 0; i < settings.notesPerSequence; i++)
        {
            float frequency = Mathf.Clamp(sampleGaussian(q.peakFrequency, sigma), q.minFrequency, q.maxFrequency);
            frequencies.Add(applyOctaveShift(frequency, selectedOctaveShift));
        }
        Debug.Log($"=== SEQUENCE START: Quadrant Q{quadrantIndex + 1}, Octave Shift: {selectedOctaveShift} ===");
        StartCoroutine(playSequenceCoroutine(frequencies, quadrantIndex));
    }

    private IEnumerator playSequenceCoroutine(List<float> frequencies, int quadrantIndex)
    {
        float duration = calibrationCondition.duration;
        float fade = Mathf.Min(calibrationCondition.fadeDuration, duration);
        float pause = preset.pauseBetweenNotes;
        for (int i = 0; i < frequencies.Count; i++)
        {
            float frequency = frequencies[i];
            AudioClip clip = getClosestAudioClip(frequency);
            playClipAtFrequency(clip, frequency);
            Debug.Log($"[Note {i + 1}/{frequencies.Count}] Q{quadrantIndex + 1} | Octave: {selectedOctaveShift} | " +
                $"Target: {frequency:F2} Hz | Sample: {clip.name} Hz | Pitch: {audioSource.pitch:F4} | " +
                $"Volume: {currentNoteVolume:F4} | Modeled target: {calibrationCondition.targetPeakSones:F4} sones");
            yield return new WaitForSeconds(Mathf.Max(0f, duration - fade));
            yield return StartCoroutine(fadeOutOverDuration(fade));
            yield return new WaitForSeconds(pause);
        }
        Debug.Log("=== SEQUENCE END ===");
    }

    private void playClipAtFrequency(AudioClip clip, float targetFrequency)
    {
        float nominal = getClipFrequency(clip);
        audioSource.clip = clip;
        audioSource.pitch = targetFrequency / nominal;
        float effectivePitch = audioSource.pitch;
        var correction = calibrationCondition.getGain(clip.name, effectivePitch);
        currentNoteVolume = (float)correction.gain;
        // The common condition target makes every gain feasible; do not clip each
        // note independently or multiply by masterVolume a second time.
        audioSource.volume = currentNoteVolume;
        audioSource.loop = true;
        var recording = Array.Find(calibrationData.clips, entry => entry.clipName == clip.name);
        int startFrame = (int)Math.Round(calibrationProfile.getPlaybackStartFrame(clip.name)
            * (double)clip.frequency / recording.sampleRate);
        audioSource.timeSamples = startFrame;
        currentLoudnessRecord.notes.Add(new notePlaybackRecord
        {
            clipName = clip.name,
            requestedFrequencyHz = targetFrequency,
            nominalPlaybackFrequencyHz = nominal * effectivePitch,
            pitchRatio = effectivePitch,
            playbackStartFrame = startFrame,
            playbackSampleRate = clip.frequency,
            sourceVolume = currentNoteVolume,
            plannedPeakSones = correction.expectedPeakSones,
            plannedDurationSeconds = calibrationCondition.duration,
            requestedTimeInSequenceSeconds = Time.time - sequenceStartTime
        });
        audioSource.Play();
    }

    private IEnumerator fadeOutOverDuration(float duration)
    {
        float startVolume = audioSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }
        audioSource.Stop();
        audioSource.loop = false;
        audioSource.volume = currentNoteVolume;
    }

    public void stopSequence()
    {
        StopAllCoroutines();
        audioSource.Stop();
        audioSource.loop = false;
    }

    public void generateAndPlayFrequency(int quadrantIndex)
    {
        if (quadrantIndex < 0 || quadrantIndex >= quadrants.Count)
            throw new ArgumentOutOfRangeException(nameof(quadrantIndex));
        prepareLoudness();
        stopSequence();
        beginLoudnessRecord();
        var settings = preset;
        quadrant q = quadrants[quadrantIndex];
        float frequency = Mathf.Clamp(sampleGaussian(q.peakFrequency, (q.maxFrequency - q.minFrequency) / 6f),
            q.minFrequency, q.maxFrequency);
        selectedOctaveShift = settings.octaveShifts[UnityEngine.Random.Range(0, settings.octaveShifts.Length)];
        float finalFrequency = applyOctaveShift(frequency, selectedOctaveShift);
        playClipAtFrequency(getClosestAudioClip(finalFrequency), finalFrequency);
        StartCoroutine(fadeOutAfterDuration(calibrationCondition.duration));
    }

    private AudioClip getClosestAudioClip(float targetFrequency)
    {
        AudioClip closest = null;
        float distance = float.MaxValue;
        foreach (var clip in pianoSamples)
        {
            float candidate = Mathf.Abs(Mathf.Log(targetFrequency / getClipFrequency(clip)));
            if (candidate < distance) { distance = candidate; closest = clip; }
        }
        return closest;
    }

    private IEnumerator fadeOutAfterDuration(float duration)
    {
        float fade = Mathf.Min(calibrationCondition.fadeDuration, duration);
        yield return new WaitForSeconds(Mathf.Max(0f, duration - fade));
        yield return StartCoroutine(fadeOutOverDuration(fade));
    }

    private float applyOctaveShift(float frequency, int octaveShift) => frequency * Mathf.Pow(2, octaveShift);

    private float sampleGaussian(float mean, float sigma)
    {
        float u1 = 1f - UnityEngine.Random.value;
        float u2 = 1f - UnityEngine.Random.value;
        return mean + sigma * Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
    }
}
