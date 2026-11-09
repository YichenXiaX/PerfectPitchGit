using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// Only public project configuration belongs in a player build.
public partial class cloudAccount : MonoBehaviour
{
    public static cloudAccount instance { get; private set; }
    public const string gameVersion = "0.1.0";
    public const string accountScene = "account";
    public const string playScene = "SampleScene";
    public const string tutorialScene = "flightTraining";
    public const string consentScene = "researchConsent";
    public const int tutorialVersion = 1;
    public bool tutorialComplete => profile.tutorialVersion >= tutorialVersion;
    public bool consentComplete => profile.consentVersion == researchConsentState.formVersion &&
        (profile.consentStatus == "adultAgreed" || profile.consentStatus == "childAndParentAgreed" || profile.consentStatus == "declined");
    [Serializable] private class tokenResponse { public string access_token, refresh_token; public int expires_in; }
    [Serializable] public class progressRecord { public long bestScore; public int highestLevel; public int sessionsPlayed; public int sessionsCompleted; }
    [Serializable] public class profileRecord {
        public string nickname; public int tutorialVersion;
        public string consentVersion, consentStatus, consentReceiptId; public bool researchCollectionEnabled;
    }
    [Serializable] public class startRecord { public string sessionId; public string gameVersion; public string mode; }
    [Serializable] public class checkpoint
    {
        public string sessionId; public int revision; public int score; public int highestLevel;
        public int elapsedSeconds; public bool completed;
        public long startedAtUnixMs, endedAtUnixMs;
        public string endReason;
    }
#if UNITY_EDITOR
    public static Func<string,string,string,string,Action<long,string>,IEnumerator> testTransport;
    public static Func<string,string,string,string,Action<long,string>,IEnumerator> testApiTransport;
#endif
    private class response { public long status; public string body; public bool ok => status >= 200 && status < 300; }
    public bool signedIn => !string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(userId);
    public bool busy { get; private set; }
    public bool saving { get; private set; }
    public bool hasPendingSave => pending != null || latest != null || researchQueue.Count > 0;
    public string message { get; private set; } = "Log in to save your progress.";
    public string saveMessage { get; private set; } = "Cloud saves ready";
    public string activeSessionId { get; private set; }
    public progressRecord progress { get; private set; } = new progressRecord();
    public profileRecord profile { get; private set; } = new profileRecord();
    public event Action changed;
    private string accessToken, refreshToken, userId;
    private double expiresAt;
    private bool refreshing;
    private checkpoint pending, latest;
    private startRecord pendingStart;
    private int revision, highestLevel;
    private double startedAt, nextSave, retryAt;
    private bool finished;
    private int failureCount;
    private string ownerId;
    private bool researchAllowedForRun;
    private readonly System.Collections.Generic.Queue<string> researchQueue = new System.Collections.Generic.Queue<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void resetStatics() { instance = null;
#if UNITY_EDITOR
        testTransport = null; testApiTransport = null;
        firebaseEmulator = false;
#endif
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void bootstrap()
    {
        if (instance == null)
        {
            new GameObject("cloudAccount").AddComponent<cloudAccount>();
            var ui = Resources.Load<GameObject>("accountUi");
            if (ui != null) Instantiate(ui);
        }
    }
    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        firebase = firebaseConfig.load();
        DontDestroyOnLoad(gameObject);
    }
    private void Update()
    {
        if (!signedIn) return;
        if (!busy && !refreshing && Time.realtimeSinceStartupAsDouble >= expiresAt - 90)
            StartCoroutine(refreshSession());
        if (activeSessionId != null && !finished && gameManager.instance != null && gameManager.instance.isGameActive &&
            Time.realtimeSinceStartupAsDouble >= nextSave)
        {
            capture(false);
            nextSave = Time.realtimeSinceStartupAsDouble + 20;
        }
        if (!saving && hasPendingSave && !busy && Time.realtimeSinceStartupAsDouble >= retryAt)
            StartCoroutine(flush());
    }
    private void notify() { changed?.Invoke(); }
    public void setMessage(string value) { message = value; notify(); }
    private IEnumerator request(string path, string method, string json, string bearer, Action<response> done, string contentType = "application/json")
    {
#if UNITY_EDITOR
        if (testTransport != null)
        {
            yield return testTransport(path, method, json, bearer, (status, body) => done(new response { status = status, body = body }));
            yield break;
        }
#endif
        using (var web = new UnityWebRequest(path, method))
        {
            web.downloadHandler = new DownloadHandlerBuffer();
            if (json != null) web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            if (json != null) web.SetRequestHeader("Content-Type", contentType);
            if (!string.IsNullOrEmpty(bearer)) web.SetRequestHeader("Authorization", "Bearer " + bearer);
            web.timeout = 20;
            yield return web.SendWebRequest();
            done(new response { status = web.responseCode, body = web.downloadHandler.text });
        }
    }
    private static T parse<T>(string json) where T : new()
    {
        try { return JsonUtility.FromJson<T>(json) ?? new T(); }
        catch { return new T(); }
    }
    private string error(response result)
    {
        if (result.status == 0 || result.status >= 500) return "Cannot reach the server. Check your connection and try again.";
        if (result.status == 429) return "Too many requests. Wait a minute before trying again.";
        if (result.status == 403) return "The database did not authorize this save. Keep this window open, refresh your account, and contact the game administrator if it persists.";
        if (result.body != null && result.body.Contains("consent_required")) return "Your research choice needs updating. Open Account, then Research choice.";
        if (result.body != null && result.body.Contains("consent_conflict")) return "This choice conflicts with a saved request. Reopen Research choice and try again.";
        if (result.body != null && result.body.Contains("email_not_confirmed")) return "Verify your email first, then log in.";
        if (result.status == 409) return "This save conflicts with the server. Keep this window open and contact support.";
        if (result.status == 401 || result.status == 403) return "Login could not be verified. Check your details or log in again.";
        return "The request was not accepted. Check your details and try again.";
    }
    private static string consentError(response result)
    {
        if (result.status == 0) return "Connection lost. Keep this form open and retry; the same choice will not create a second record.";
        if (result.status == 401) return "Your login expired. Your entries are kept. Retry, or sign in again using Account.";
        if (result.status == 403) return "The database could not authorize this consent save. Your entries are kept. Ask the game administrator to verify the Firebase rules, then retry.";
        if (result.status == 409) return "Your saved choice changed in another request or window. Reopen Research choice to review the current decision.";
        if (result.status == 429) return "Too many requests. Your entries are kept. Wait a minute, then retry.";
        return "The consent service did not confirm the save. Your entries are kept. Retry shortly; if it persists, report HTTP "+result.status+" to the game administrator.";
    }
    private IEnumerator refreshSession() => refreshFirebase();
    private IEnumerator api(string path, string method, string json, Action<response> done)
    {
        if (refreshing || Time.realtimeSinceStartupAsDouble >= expiresAt - 30) yield return refreshSession();
        if (!signedIn) { done(new response { status = 401 }); yield break; }
#if UNITY_EDITOR
        // Presentation tests may stub the logical save boundary. Firebase integration
        // checks leave this null and exercise real REST requests and Security Rules.
        if (testApiTransport != null) {
            yield return testApiTransport("/"+path,method,json,accessToken,(status,body)=>done(new response{status=status,body=body}));
            yield break;
        }
#endif
        yield return firebaseApi(path, method, json, done);
    }
    public void refreshProgress() { if (!busy && signedIn) StartCoroutine(refreshProgressRoutine()); }
    private IEnumerator refreshProgressRoutine() { busy = true; notify(); yield return loadProgress(); busy = false; notify(); }
    private IEnumerator loadProgress()
    {
        response result = null;
        yield return api("profile", "GET", null, r => result = r);
        if (!result.ok) { setMessage(error(result)); yield break; }
        profile = parse<profileRecord>(result.body);
        profileLoaded=true;
        researchConsentState.status = consentComplete ? profile.consentStatus : "notObtained";
        yield return api("progress", "GET", null, r => result = r);
        if (result.ok) progress = parse<progressRecord>(result.body);
        else setMessage(error(result));
        notify();
    }
    public void requestStart(Action begin)
    {
        if (!signedIn) { setMessage("Log in before playing."); return; }
        if (!consentComplete) { setMessage("Choose whether to participate in research before playing."); enterPlay(); return; }
        if (!tutorialComplete || SceneManager.GetActiveScene().name == tutorialScene) { setMessage("Finish flight training before starting a run."); return; }
        if (!busy) StartCoroutine(startRun(begin));
    }
    private IEnumerator startRun(Action begin)
    {
        busy = true; setMessage("Preparing cloud save...");
        while (saving) yield return null;
        if (hasPendingSave) yield return flush();
        if (hasPendingSave) { busy = false; setMessage("Finish saving the previous run before playing again. " + saveMessage); yield break; }
        if (pendingStart == null) pendingStart = new startRecord {
            sessionId = Guid.NewGuid().ToString(), gameVersion = gameVersion,
            mode = gameSettings.instance != null && gameSettings.instance.isCustomMode ? "custom" : "standard" };
        response result = null;
        yield return api("sessions", "POST", JsonUtility.ToJson(pendingStart), r => result = r);
        if (result.ok)
        {
            activeSessionId = pendingStart.sessionId; pendingStart = null;
            ownerId = userId; revision = 0; highestLevel = 0; finished = false;
            runStartedAt=runEndedAt=0; runEndReason=null; runConsentReceipt=profile.consentReceiptId;
            researchAllowedForRun = consentComplete && profile.researchCollectionEnabled;
            startedAt = Time.realtimeSinceStartupAsDouble; nextSave = startedAt + 20;
            saveMessage = "Cloud session ready"; failureCount = 0;
            setMessage("");
            begin();
        }
        else { saveMessage = error(result); setMessage(saveMessage); }
        busy = false; notify();
    }
    public void capture(bool completed)
    {
        if (activeSessionId == null || ownerId != userId || finished) return;
        var manager = gameManager.instance;
        if (manager == null || manager.isPractice || manager.getSessionData() == null) return;
        highestLevel = Math.Max(highestLevel, Mathf.Clamp(gameSettings.instance.level, 0, 3));
        latest = new checkpoint {
            sessionId = activeSessionId, score = Mathf.RoundToInt(Mathf.Clamp(manager.aura, -1e9f, 1e9f)),
            highestLevel = highestLevel, elapsedSeconds = (int)Math.Min(86400, Time.realtimeSinceStartupAsDouble - startedAt),
            completed = completed, startedAtUnixMs=runStartedAt,
            endedAtUnixMs=completed ? (runEndedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) : runEndedAt,
            endReason=completed ? manager.runEndReason : runEndReason };
        finished = completed;
        persistOutbox();
        saveMessage = "Saving progress..."; retryAt = 0; notify();
    }
    public void enterPlay()
    {
        if(!signedIn || busy || hasPendingSave) return;
        Time.timeScale=1;
        SceneManager.LoadScene(!consentComplete ? consentScene : tutorialComplete ? playScene : tutorialScene);
    }
    public void requestTutorial()
    {
        if(signedIn && !busy) StartCoroutine(leaveForOnboarding(consentComplete ? tutorialScene : consentScene));
    }
    public void requestConsent()
    {
        if(signedIn && !busy) StartCoroutine(leaveForOnboarding(consentScene));
    }
    private IEnumerator leaveForOnboarding(string destination)
    {
        busy=true; setMessage("Saving this run before leaving...");
        capture(false);
        while(saving) yield return null;
        if(hasPendingSave) yield return flush();
        if(hasPendingSave) { busy=false; setMessage("Your run has not saved. Retry the save before leaving."); yield break; }
        closeResearchRun(destination==tutorialScene?"tutorialExit":"consentReview");
        if(hasPendingSave)yield return flush(); // A failed final write remains in the account's durable outbox.
        // This is a deliberate exit, not a completed run. No session remains attached to practice.
        activeSessionId=null; pendingStart=null; finished=false;
        researchAllowedForRun=false;
        busy=false; Time.timeScale=1;
        SceneManager.LoadScene(destination); setMessage("");
    }
    public void saveConsent(consentSubmission submission, Action<bool> done)
    {
        if (!signedIn || busy || activeSessionId != null) { setMessage("Return to Account before changing your research choice."); done(false); return; }
        StartCoroutine(saveConsentRoutine(JsonUtility.ToJson(submission), submission.requestId, submission.decision, done));
    }
    private IEnumerator saveConsentRoutine(string json, string requestId, string decision, Action<bool> done)
    {
        busy=true; setMessage("Saving your research choice...");
        response result=null;
        yield return api("consent", "PUT", json, r=>result=r);
        var confirmation=result.ok ? parse<profileRecord>(result.body) : null;
        bool success=confirmation!=null && confirmation.consentVersion==researchConsentState.formVersion &&
            confirmation.consentStatus==decision && !string.IsNullOrEmpty(requestId) && confirmation.consentReceiptId==requestId;
        if (success) {
            profile=confirmation;
            researchConsentState.status=profile.consentStatus;
        }
        setMessage(success ? "Your research choice is saved." : "Your choice was not confirmed. "+
            (result.ok ? "The server returned a different or incomplete receipt. Your entries are kept. Retry; if it persists, report this to the game administrator." : consentError(result)));
        busy=false; notify(); done(success);
    }
    public void completeTutorial(Action<bool> done)
    {
        if(!signedIn || busy || !consentComplete) { done(false); return; }
        StartCoroutine(saveTutorial(done));
    }
    private IEnumerator saveTutorial(Action<bool> done)
    {
        busy=true; setMessage("Saving training completion...");
        response result=null;
        yield return api("tutorial", "PUT", "{\"version\":1,\"milestones\":[\"correct\",\"miss\",\"recovery\",\"impact\",\"restart\"]}", r=>result=r);
        if(result.ok) { profile=parse<profileRecord>(result.body); setMessage("Flight training complete."); }
        else setMessage("Training completion was not saved. Retry before entering the game. "+error(result));
        busy=false; notify(); done(result.ok && tutorialComplete);
    }
    public void retrySave() { if (!saving && signedIn) StartCoroutine(flush()); }
    private IEnumerator flush()
    {
        if (saving || !signedIn || ownerId != userId) yield break;
        saving = true; notify();
        while (hasPendingSave && signedIn && ownerId == userId)
        {
            if(pending==null && latest==null && researchQueue.Count>0) {
                response trialResult=null;
                yield return api("trials","PUT",researchQueue.Peek(),r=>trialResult=r);
                if(!trialResult.ok) {
                    if(trialResult.status==403 && trialResult.body!=null && trialResult.body.Contains("research_not_allowed")) {
                        researchQueue.Clear();researchAllowedForRun=false;persistOutbox();
                        setMessage("Research uploads stopped: consent changed or the upload window expired. Scores remain saved.");
                        yield return loadProgress();continue;
                    }
                    saveMessage="Research results not saved yet. "+error(trialResult);
                    failureCount++;retryAt=Time.realtimeSinceStartupAsDouble+Math.Min(60,5*Math.Pow(2,Math.Min(4,failureCount)));break;
                }
                researchQueue.Dequeue();persistOutbox();failureCount=0;saveMessage="Progress saved";continue;
            }
            if (pending == null) { pending = latest; latest = null; pending.revision = ++revision; }
            persistOutbox();
            response result = null;
            yield return api("checkpoints", "PUT", JsonUtility.ToJson(pending), r => result = r);
            if (!result.ok)
            {
                saveMessage = "Not saved yet. " + error(result);
                failureCount++; retryAt = Time.realtimeSinceStartupAsDouble + Math.Min(60, 5 * Math.Pow(2, Math.Min(4, failureCount)));
                break; // Retain the exact payload and revision for safe retries.
            }
            bool final = pending.completed;
            pending = null; failureCount = 0; saveMessage = "Progress saved";
            persistOutbox();
            if (final) yield return loadProgress();
        }
        saving = false; notify();
    }
    public void logOut() { if (!busy) StartCoroutine(logOutRoutine()); }
    private IEnumerator logOutRoutine()
    {
        busy = true; notify();
        capture(false);
        while (refreshing) yield return null;
        while (saving) yield return null;
        if (hasPendingSave) yield return flush();
        if (hasPendingSave)
        { busy = false; setMessage("Logout paused to protect your unsaved progress. Retry when connected."); notify(); yield break; }
        closeResearchRun("logout");
        if(hasPendingSave)yield return flush();
        accessToken = refreshToken = userId = ownerId = activeSessionId = null;
        profileLoaded=false;
        pendingFirebaseTokens=null;pendingFirebaseEmail=null;firebaseRecoveryCode=null;
        pendingStart = null; pending = latest = null;
        researchQueue.Clear();researchAllowedForRun=false;
        progress = new progressRecord(); profile = new profileRecord(); finished = false;
        researchConsentState.reset();
        busy = false; Time.timeScale = 1;
        if (gameSettings.instance != null) Destroy(gameSettings.instance.gameObject);
        SceneManager.LoadScene(accountScene);
        setMessage("You are logged out.");
    }
    private void OnApplicationPause(bool paused) { if (paused) { capture(false); retrySave(); } }
    private void OnApplicationQuit() { closeResearchRun("applicationExit"); }
    // Browsers may skip shutdown callbacks. The outbox and trial-start marker are the fallback.
}
