using System;
using System.Collections.Generic;

/// <summary>
/// Reproducible modeled audio condition. Public-device playback is not a measured
/// acoustic calibration; requested playback times are not EEG/audio onset markers.
/// </summary>
[Serializable]
public class audioLoudnessRecord
{
    public string profileId;
    public string model;
    public string playbackCalibration = "uncalibratedDevice";
    public float pressurePaPerDigitalUnit;
    public float noteDurationSeconds;
    public float fadeDurationSeconds;
    public float masterVolume;
    public double plannedTargetPeakSones;
    public List<notePlaybackRecord> notes = new List<notePlaybackRecord>();
}

[Serializable]
public class notePlaybackRecord
{
    public string clipName;
    public float requestedFrequencyHz;
    public float nominalPlaybackFrequencyHz;
    public float pitchRatio;
    public int playbackStartFrame;
    public int playbackSampleRate;
    public float sourceVolume;
    public double plannedPeakSones;
    public float plannedDurationSeconds;
    public float elapsedSecondsAtResult;
    public bool resultBeforePlannedEnd;
    public float requestedTimeInSequenceSeconds;
}
