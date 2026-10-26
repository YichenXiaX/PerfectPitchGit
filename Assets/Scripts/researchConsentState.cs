using UnityEngine;

// Per-launch decision, not an account record or durable consent receipt.
public static class researchConsentState
{
    public const string formVersion = "2026-10-25-v1";
    public static string status = "notObtained";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void reset() { status = "notObtained"; }
}
