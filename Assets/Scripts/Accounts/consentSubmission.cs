using System;

// Identifying consent details travel only to the consent endpoint, never with trials.
[Serializable]
public class consentSubmission
{
    public string requestId;
    public string formVersion = researchConsentState.formVersion;
    public string decision;
    public bool adultAgreement, childAgreement, parentAgreement;
    public string participantName = "", childName = "", parentChildName = "", guardianName = "";
    public string participantFollowUp = "notSpecified", guardianFollowUp = "notSpecified";
    public string participantContact = "", guardianContact = "";
}
