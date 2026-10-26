using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// Only public project configuration belongs in a player build.
public class cloudAccount : MonoBehaviour
{
    public static cloudAccount instance { get; private set; }
    public const string gameVersion = "0.1.0";
    public const string accountScene = "account";
    public const string playScene = "SampleScene";
    private const string serviceUrl = "https://smgqmnkxfokgjfbtumxz.supabase.co";
    private const string publicKey = "sb_publishable_tQztnjtJ5b2exDOp6fu5sw_Sq-wfZoU";

    [Serializable] public class userRecord { public string id; public string email_confirmed_at; public bool is_anonymous; }
    [Serializable] public class tokenResponse { public string access_token; public string refresh_token; public int expires_in; public userRecord user; }
    [Serializable] private class credentials { public string email; public string password; }
    [Serializable] private class verification { public string email; public string token; public string type = "signup"; }
    [Serializable] private class resendRequest { public string email; public string type = "signup"; }
    [Serializable] private class refreshRequest { public string refresh_token; }
    [Serializable] public class progressRecord { public long bestScore; public int highestLevel; public int sessionsPlayed; public int sessionsCompleted; }
    [Serializable] public class profileRecord { public string nickname; public bool leaderboardEnabled; }
    [Serializable] public class startRecord { public string sessionId; public string gameVersion; public string mode; }
    [Serializable] public class checkpoint
    {
        public string sessionId; public int revision; public int score; public int highestLevel;
        public int elapsedSeconds; public bool completed;
    }
#if UNITY_EDITOR
    public static Func<string,string,string,string,Action<long,string>,IEnumerator> testTransport;
#endif
    private class response { public long status; public string body; public bool ok => status >= 200 && status < 300; }
    public bool signedIn => !string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(userId);
    public bool busy { get; private set; }
    public bool saving { get; private set; }
    public bool hasPendingSave => pending != null || latest != null;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void resetStatics() { instance = null;
#if UNITY_EDITOR
        testTransport = null;
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
    private IEnumerator request(string path, string method, string json, string bearer, Action<response> done)
    {
#if UNITY_EDITOR
        if (testTransport != null)
        {
            yield return testTransport(path, method, json, bearer, (status, body) => done(new response { status = status, body = body }));
            yield break;
        }
#endif
        using (var web = new UnityWebRequest(serviceUrl + path, method))
        {
            web.downloadHandler = new DownloadHandlerBuffer();
            if (json != null) web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            web.SetRequestHeader("apikey", publicKey);
            if (json != null) web.SetRequestHeader("Content-Type", "application/json");
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
        if (result.body != null && result.body.Contains("email_not_confirmed")) return "Verify your email first, then log in.";
        if (result.body != null && result.body.Contains("email_address_not_authorized")) return "Email delivery is limited to the project's test addresses right now.";
        if (result.status == 409) return "This save conflicts with the server. Keep this window open and contact support.";
        if (result.status == 401 || result.status == 403) return "Login could not be verified. Check your details or log in again.";
        return "The request was not accepted. Check your details and try again.";
    }
    public void logIn(string email, string password) { if (!busy) StartCoroutine(authenticate(email, password, false)); }
    public void signUp(string email, string password) { if (!busy) StartCoroutine(authenticate(email, password, true)); }
    private IEnumerator authenticate(string email, string password, bool creating)
    {
        email = email.Trim();
        if (!email.Contains("@") || password.Length == 0 || (creating && password.Length < 12))
        { setMessage(creating ? "Enter your email and a password with at least 12 characters." : "Enter your email and password."); yield break; }
        busy = true; setMessage(creating ? "Creating your account..." : "Logging in...");
        response result = null;
        yield return request(creating ? "/auth/v1/signup" : "/auth/v1/token?grant_type=password", "POST",
            JsonUtility.ToJson(new credentials { email = email, password = password }), null, r => result = r);
        password = null;
        if (result.ok)
        {
            var tokens = parse<tokenResponse>(result.body);
            if (!string.IsNullOrEmpty(tokens.access_token)) yield return acceptSession(tokens);
            else setMessage("Check your email. Enter its verification code below, or follow the confirmation link and then log in.");
        }
        else setMessage(error(result));
        busy = false; notify();
    }
    public void verifyEmail(string email, string code)
    {
        if (!busy) StartCoroutine(verify(email.Trim(), code.Trim()));
    }
    private IEnumerator verify(string email, string code)
    {
        if (email.Length == 0 || code.Length < 6) { setMessage("Enter your email and its verification code."); yield break; }
        busy = true; setMessage("Verifying email...");
        response result = null;
        yield return request("/auth/v1/verify", "POST", JsonUtility.ToJson(new verification { email = email, token = code }), null, r => result = r);
        if (result.ok) yield return acceptSession(parse<tokenResponse>(result.body));
        else setMessage("Verification failed. Check the code or request another email.");
        busy = false; notify();
    }
    public void resendEmail(string email) { if (!busy) StartCoroutine(resend(email.Trim())); }
    private IEnumerator resend(string email)
    {
        if (!email.Contains("@")) { setMessage("Enter your email first."); yield break; }
        busy = true; notify();
        response result = null;
        yield return request("/auth/v1/resend", "POST", JsonUtility.ToJson(new resendRequest { email = email }), null, r => result = r);
        setMessage(result.ok ? "If confirmation is pending, a new email has been sent. Check your inbox." : error(result));
        busy = false; notify();
    }
    private IEnumerator acceptSession(tokenResponse tokens)
    {
        if (string.IsNullOrEmpty(tokens.access_token) || string.IsNullOrEmpty(tokens.refresh_token))
        { setMessage("Please log in after verifying your email."); yield break; }
        // A fresh Auth request verifies the account; token contents are never trusted locally.
        response result = null;
        yield return request("/auth/v1/user", "GET", null, tokens.access_token, r => result = r);
        var user = result.ok ? parse<userRecord>(result.body) : new userRecord();
        if (!result.ok || string.IsNullOrEmpty(user.id) || string.IsNullOrEmpty(user.email_confirmed_at) || user.is_anonymous)
        { setMessage("A verified email account is required."); yield break; }
        if (ownerId != null && ownerId != user.id && activeSessionId != null)
        { setMessage("Log back into the original account to finish saving this run."); yield break; }
        userId = user.id; accessToken = tokens.access_token; refreshToken = tokens.refresh_token;
        expiresAt = Time.realtimeSinceStartupAsDouble + Math.Max(60, tokens.expires_in);
        setMessage("You are logged in.");
        yield return loadProgress();
    }
    private IEnumerator refreshSession()
    {
        if (refreshing) { while (refreshing) yield return null; yield break; }
        if (string.IsNullOrEmpty(refreshToken)) yield break;
        refreshing = true;
        response result = null;
        yield return request("/auth/v1/token?grant_type=refresh_token", "POST",
            JsonUtility.ToJson(new refreshRequest { refresh_token = refreshToken }), null, r => result = r);
        if (result.ok)
        {
            var tokens = parse<tokenResponse>(result.body);
            if (!string.IsNullOrEmpty(tokens.access_token) && !string.IsNullOrEmpty(tokens.refresh_token))
            {
                accessToken = tokens.access_token; refreshToken = tokens.refresh_token;
                expiresAt = Time.realtimeSinceStartupAsDouble + Math.Max(60, tokens.expires_in);
            }
            else { accessToken = null; setMessage("Please log in again to continue saving."); }
        }
        else if (result.status == 400 || result.status == 401 || result.status == 403)
        { accessToken = null; setMessage("Your session expired. Log in again to finish saving."); }
        else expiresAt = Time.realtimeSinceStartupAsDouble + 120;
        refreshing = false; notify();
    }
    private IEnumerator api(string path, string method, string json, Action<response> done)
    {
        if (refreshing || Time.realtimeSinceStartupAsDouble >= expiresAt - 30) yield return refreshSession();
        if (!signedIn) { done(new response { status = 401 }); yield break; }
        response result = null;
        yield return request("/functions/v1/game-api/" + path, method, json, accessToken, r => result = r);
        if (result.status == 401)
        {
            yield return refreshSession();
            if (signedIn) yield return request("/functions/v1/game-api/" + path, method, json, accessToken, r => result = r);
        }
        done(result);
    }
    public void refreshProgress() { if (!busy && signedIn) StartCoroutine(refreshProgressRoutine()); }
    private IEnumerator refreshProgressRoutine() { busy = true; notify(); yield return loadProgress(); busy = false; notify(); }
    private IEnumerator loadProgress()
    {
        response result = null;
        yield return api("profile", "GET", null, r => result = r);
        if (!result.ok) { setMessage(error(result)); yield break; }
        profile = parse<profileRecord>(result.body);
        yield return api("progress", "GET", null, r => result = r);
        if (result.ok) progress = parse<progressRecord>(result.body);
        else setMessage(error(result));
        notify();
    }
    public void requestStart(Action begin)
    {
        if (!signedIn) { setMessage("Log in before playing."); return; }
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
            startedAt = Time.realtimeSinceStartupAsDouble; nextSave = startedAt + 20;
            saveMessage = "Cloud session ready"; failureCount = 0;
            begin();
        }
        else { saveMessage = error(result); setMessage(saveMessage); }
        busy = false; notify();
    }
    public void capture(bool completed)
    {
        if (activeSessionId == null || ownerId != userId || finished) return;
        var manager = gameManager.instance;
        if (manager == null || manager.getSessionData() == null) return;
        highestLevel = Math.Max(highestLevel, Mathf.Clamp(gameSettings.instance.level, 0, 3));
        latest = new checkpoint {
            sessionId = activeSessionId, score = Mathf.RoundToInt(Mathf.Clamp(manager.aura, -1e9f, 1e9f)),
            highestLevel = highestLevel, elapsedSeconds = (int)Math.Min(86400, Time.realtimeSinceStartupAsDouble - startedAt),
            completed = completed };
        finished = completed;
        saveMessage = "Saving progress..."; retryAt = 0; notify();
    }
    public void retrySave() { if (!saving && signedIn) StartCoroutine(flush()); }
    private IEnumerator flush()
    {
        if (saving || !signedIn || ownerId != userId) yield break;
        saving = true; notify();
        while (hasPendingSave && signedIn && ownerId == userId)
        {
            if (pending == null) { pending = latest; latest = null; pending.revision = ++revision; }
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
        response result = null;
        if (signedIn) yield return request("/auth/v1/logout?scope=local", "POST", null, accessToken, r => result = r);
        if (result != null && !result.ok && result.status != 401 && result.status != 403)
        { busy = false; setMessage("Could not log out on the server. Please retry."); notify(); yield break; }
        accessToken = refreshToken = userId = ownerId = activeSessionId = null;
        pendingStart = null; pending = latest = null;
        progress = new progressRecord(); profile = new profileRecord(); finished = false;
        busy = false; Time.timeScale = 1;
        if (gameSettings.instance != null) Destroy(gameSettings.instance.gameObject);
        SceneManager.LoadScene(accountScene);
        setMessage("You are logged out.");
    }
    private void OnApplicationPause(bool paused) { if (paused) { capture(false); retrySave(); } }
    // Browser shutdown cannot reliably complete requests. Periodic saves are the durable boundary.
}






