using System.Collections.Generic;

public enum SequenceOutcome
{
    Predicted,
    Destroyed,
    Hit
}

[System.Serializable]
public class SequenceResult
{
    public int roundIndex;
    public int correctQuadrant;
    public int predictedQuadrant;   // -1 if no prediction was attempted
    public int level;
    public float predictionTime;  // when they pressed F (-1 if they never did)
    public float resolvedTime;    // when the round actually ended
    public string outcome;          // "Predicted", "Destroyed", or "Hit" (string for Firebase)
    public long timestamp;          // Unix ms
}

[System.Serializable]
public class GameSessionData
{
    public string playerName;
    public string sessionId;
    public long sessionStartTimestamp;
    public int finalLevel;
    public int totalRounds;
    public int totalPredictions;
    public int totalDestroys;
    public int totalHits;
    public List<SequenceResult> results = new List<SequenceResult>();
}