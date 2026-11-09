using UnityEngine;

// Session mirror of the server's account-owned consent receipt. Never an authorization source.
public static class researchConsentState
{
    public const string formVersion = "2026-10-25-v1";
    public static string status = "notObtained";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void reset() { status = "notObtained"; }
}
