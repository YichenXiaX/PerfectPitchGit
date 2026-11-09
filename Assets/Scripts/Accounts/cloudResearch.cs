using System;
using Newtonsoft.Json;
using UnityEngine;

public partial class cloudAccount
{
    private long runStartedAt, runEndedAt;
    private string runEndReason, runConsentReceipt;
    public bool canRecordResearch => signedIn && ownerId==userId && activeSessionId!=null &&
        consentComplete && profile.researchCollectionEnabled && researchAllowedForRun;

    public void gameplayStarted(long timestamp)
    { runStartedAt=timestamp; capture(false); }

    public void recordResearch(researchTrialRecord trial)
    {
        if(trial==null || !canRecordResearch || gameManager.instance==null || gameManager.instance.isPractice)return;
        // Freeze a retry's content; never serialize the mutable gameplay session or contact details.
        researchQueue.Enqueue(JsonConvert.SerializeObject(new {sessionId=activeSessionId,trial}));
        persistOutbox();retryAt=0;saveMessage="Saving progress...";notify();
    }
    private void closeResearchRun(string reason)
    {
        if(activeSessionId==null || finished || gameManager.instance==null || gameManager.instance.isPractice)return;
        gameManager.instance.research?.finish("Interrupted",reason);
        runEndedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();runEndReason=reason;
        capture(false);
    }

    // One account-scoped retry buffer, with no credentials. PlayerPrefs is backed
    // by browser IndexedDB in WebGL. A successful server acknowledgement removes
    // a request; an interrupted acknowledgement simply retries the same ID.
    [Serializable] private class saveOutbox
    {
        public int revision;
        public string consentReceipt;
        public checkpoint pending, latest;
        public string[] research;
    }
    private string outboxKey => "comet.save.v2."+firebaseProject+"."+userId;
    private void persistOutbox()
    {
        if(string.IsNullOrEmpty(userId) || ownerId!=userId)return;
        try {
            if(!hasPendingSave)PlayerPrefs.DeleteKey(outboxKey);
            else PlayerPrefs.SetString(outboxKey,JsonConvert.SerializeObject(new saveOutbox {
                revision=revision,consentReceipt=runConsentReceipt,pending=pending,latest=latest,research=researchQueue.ToArray()}));
            PlayerPrefs.Save();
        } catch(Exception) {
            // Keep the in-memory request. Never report a local backup as successful.
            setMessage("Local save backup is unavailable. Keep the game open until cloud saving finishes.");
        }
    }
    private void restoreOutbox()
    {
        if(activeSessionId!=null || hasPendingSave || !PlayerPrefs.HasKey(outboxKey))return;
        try {
            var saved=JsonConvert.DeserializeObject<saveOutbox>(PlayerPrefs.GetString(outboxKey));
            if(saved==null)return;
            ownerId=userId;revision=saved.revision;pending=saved.pending;latest=saved.latest;
            runConsentReceipt=saved.consentReceipt;
            if(profile.researchCollectionEnabled && profile.consentReceiptId==saved.consentReceipt)
                foreach(var record in saved.research??Array.Empty<string>())researchQueue.Enqueue(record);
            persistOutbox();retryAt=0;
        } catch(Exception) {setMessage("The local save backup could not be read. Contact the game administrator before clearing browser data.");}
    }
}
