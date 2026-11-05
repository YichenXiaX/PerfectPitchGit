using UnityEngine;
using UnityEngine.InputSystem;

// One source for gameplay, Settings and tutorial key labels/bindings.
public static class controlLayout
{
    private const string preferenceKey = "cometCatcher.leftHanded";
    public static bool leftHanded => gameSettings.instance != null ? gameSettings.instance.leftHanded : PlayerPrefs.GetInt(preferenceKey, 0) == 1;
    public static bool savedLeftHanded => PlayerPrefs.GetInt(preferenceKey, 0) == 1;
    public static event System.Action changed;
    public static void select(bool left)
    {
        bool previous = leftHanded;
        if (gameSettings.instance != null) gameSettings.instance.leftHanded = left;
        PlayerPrefs.SetInt(preferenceKey, left ? 1 : 0);
        PlayerPrefs.Save();
        if (previous != left) changed?.Invoke();
    }
    public static Key key(int lane, bool left) => left ? new[] {Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4}[lane] : new[] {Key.P,Key.LeftBracket,Key.RightBracket,Key.Backslash}[lane];
    public static string label(int lane, bool left) => left ? (lane+1).ToString() : new[] {"P","[","]","\\"}[lane];
    public static string label(int lane) => label(lane,leftHanded);
    public static string legend(bool left) => "Q1: "+label(0,left)+"   Q2: "+label(1,left)+"   Q3: "+label(2,left)+"   Q4: "+label(3,left)+"   + hold F";
}

// Gameplay-time scheduler. A stalled frame yields one shot, never catch-up fire.
public sealed class heldFireState
{
    public const float minimumInterval = 0.05f;
    public int lane { get; private set; } = -1;
    public double nextShotTime { get; private set; }
    private bool needsRelease = true;
    public void select(int value) { if(value >= 0 && value < 4) lane = value; }
    public void reset(bool clearLane) { needsRelease = true; if(clearLane) lane = -1; }
    public bool tick(double now, float interval, bool fireHeld, bool laneHeld, bool blocked)
    {
        if(blocked) { needsRelease = true; return false; }
        if(!fireHeld) { needsRelease = false; return false; }
        if(needsRelease || lane < 0 || !laneHeld || now < nextShotTime) return false;
        float safeInterval = float.IsNaN(interval) || float.IsInfinity(interval) ? .2f : Mathf.Max(minimumInterval,interval);
        nextShotTime = now + safeInterval;
        return true;
    }
}

