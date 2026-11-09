using System;

// Research-only data. Gameplay scoring and the displayed session results keep
// their existing records. Null means an event did not occur, never time zero.
[Serializable]
public class researchDecision
{
    public int quadrant;
    public double atMs;
    public int? mostRecentToneIndex, activeToneIndex;
}

[Serializable]
public class researchTrialSettings
{
    public string mode, controlLayout;
    public int notesPerSequence, consecutiveCorrectToAdvance;
    public int[] allowedOctaveShifts;
    public float noteDuration, pauseBetweenNotes, predictionWindow, cometSpawnDelay;
    public float pauseBetweenRounds, cometSpeed, shotInterval;
}

[Serializable]
public class researchTrialRecord
{
    public int schemaVersion = 2;
    public int roundIndex, correctQuadrant, level;
    public string state = "started";
    public long startedAtUnixMs;
    public int? octaveShift;
    public researchTrialSettings settings;
    public researchDecision firstDecision, secondDecision;
    public double? cometSpawnedAtMs, resolvedAtMs;
    public string predictionStatus = "notAttempted", resolution, interruptionReason;
    public bool wasPaused, lostFocus, settingsChanged;
    public audioLoudnessRecord audioLoudness;
}
