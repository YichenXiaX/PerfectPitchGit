using System.Collections.Generic;

public enum sequenceOutcome
{
    predicted,
    destroyed,
    hit
}

[System.Serializable]
public class sequenceResult
{
    public int roundIndex;
    public int correctQuadrant;
    public int predictedQuadrant;   // -1 if no prediction was attempted
    public int secondGuessQuadrant;
    public int level;
    public float predictionTime;  // when they pressed F (-1 if they never did)
    public float resolvedTime;    // when the round actually ended
    public string outcome;          // "Predicted", "Destroyed", or "Hit" (string for Firebase)
    public audioLoudnessRecord audioLoudness;
    public long timestamp;          // Unix ms
}

[System.Serializable]
public class gameSessionData
{
    public string playerName;
    public string sessionId;
    public long sessionStartTimestamp;
    public float secondGuessSuccessRate;
    public int finalLevel;
    public int totalRounds;
    public int totalPredictions;
    public int totalDestroys;
    public int totalHits;
    public List<sequenceResult> results = new List<sequenceResult>();
}