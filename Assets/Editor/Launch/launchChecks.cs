using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Editor-only end-to-end presentation checks. Transport is mocked so these runs
// cannot change participant accounts, scores, consent or research records.
public static class launchChecks
{
    public static string result = "not run";
    public static string output = Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/HeliosLaunchVerification")) + Path.DirectorySeparatorChar;
    private static readonly List<string> passed = new List<string>();
    private static readonly List<string> requests = new List<string>();
    private static bool rejectStart;
    private static int starts, trials;
    private static IEnumerator transport(string path, string method, string body, string bearer, Action<long,string> done)
    {
        yield return new WaitForSecondsRealtime(.12f);
        if (path.Contains("/token") || path.Contains("accounts:signInWithPassword")) done(200,"{\"idToken\":\"launch-test\",\"refreshToken\":\"launch-test\",\"expiresIn\":3600}");
        else if (path.Contains("accounts:lookup")) done(200,"{\"users\":[{\"localId\":\"launch-test\",\"emailVerified\":true}]}");
        else if (path.EndsWith("/profile")) done(200,JsonUtility.ToJson(new cloudAccount.profileRecord { nickname="Launch test pilot",tutorialVersion=cloudAccount.tutorialVersion,consentVersion=researchConsentState.formVersion,consentStatus="declined" }));
        else if (path.EndsWith("/progress")) done(200,"{}");
        else if (path.EndsWith("/sessions")) { requests.Add(body); done(rejectStart ? 503 : 200,rejectStart ? "{\"error\":\"unavailable\"}" : "{}"); }
        else { if(path.EndsWith("/trials"))trials++; done(200,"{}"); }
    }
    public static void run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play mode first");
        result="running";passed.Clear();requests.Clear();starts=trials=0;rejectStart=true;
        cloudAccount.testTransport=transport;cloudAccount.testApiTransport=transport;Application.runInBackground=true;
        cloudAccount.instance.StartCoroutine(check());
    }
    private static void ensure(bool value,string name)
    {
        if(!value){result="FAILED: "+name;File.WriteAllText(output+"checks.txt",result);throw new Exception(result);}
        passed.Add(name);
    }
    private static IEnumerator ready()
    {
        float deadline=Time.realtimeSinceStartup+15;
        while(Time.realtimeSinceStartup<deadline)
        {
            var sequence=UnityEngine.Object.FindFirstObjectByType<launchSequence>();
            if(sequence!=null && sequence.state==launchSequence.phase.ready && !accountUi.isBlocking){yield return null;yield break;}
            yield return null;
        }
        ensure(false,"start view became ready");
    }
    private static IEnumerator waitFor(Func<bool> predicate,string name,float timeout=12)
    {
        float deadline=Time.realtimeSinceStartup+timeout;
        while(!predicate() && Time.realtimeSinceStartup<deadline)yield return null;
        ensure(predicate(),name);
    }
    private static void capture(string name) { ScreenCapture.CaptureScreenshot(output+name+".png"); }
    private static void clickStart()
    {
        gameManager.instance.overlayObj.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="PlayButton").onClick.Invoke();
    }
    private static IEnumerator check()
    {
        Directory.CreateDirectory(output);terminalVisualChecks.size(1280,720);
        cloudAccount.instance.logIn("launch@example.invalid","test-only");yield return ready();
        var gm=gameManager.instance;var seq=UnityEngine.Object.FindFirstObjectByType<launchSequence>();
        var stars=UnityEngine.Object.FindFirstObjectByType<starfieldBackground>();
        int starId=stars.GetComponent<ParticleSystem>().GetInstanceID(),starCount=stars.starCount;
        gm.onGameStarted+=()=>starts++;
        ensure(!gm.isGameActive && cloudAccount.instance.activeSessionId==null,"idle view creates no game/cloud session");
        var start=gm.overlayObj.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="PlayButton");
        ensure(start.interactable && seq.GetComponentInChildren<terminalTransmission>().visibleCharacters<70,"Start is available while the briefing is still typing");
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Tab));yield return null;yield return null;
        ensure(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==start.gameObject,"Tab reaches Start run");
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;
        float laneWidth=(float)typeof(playerController).GetField("unitWidth",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(gm.playerController);
        ensure(Mathf.Abs(laneWidth-5.4f*2*(1280f/720)/8)<.001f,"controller caches gameplay lanes before camera widens");
        yield return new WaitForSecondsRealtime(3.4f);capture("ready-1280");yield return null;
        terminalVisualChecks.size(800,600);yield return new WaitForSecondsRealtime(.3f);capture("ready-800");yield return null;
        ensure(Camera.main.orthographicSize>=11,"smaller window fits ship below readable briefing");
        terminalVisualChecks.size(1920,1080);yield return new WaitForSecondsRealtime(.3f);capture("ready-1920");yield return null;
        terminalVisualChecks.size(1280,720);yield return new WaitForSecondsRealtime(.2f);
        // A held fire combination during launch may not become a first prediction.
        var keyboard=Keyboard.current;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(controlLayout.key(0,controlLayout.leftHanded),Key.F));yield return null;
        clickStart();clickStart();gm.startGame();yield return new WaitForSecondsRealtime(.2f);
        ensure(seq.state==launchSequence.phase.launching && requests.Count==0 && starts==0 && !gm.isGameActive,"repeated launch clicks do not create a session or start gameplay early");
        var ui=UnityEngine.Object.FindFirstObjectByType<accountUi>();ui.accountButton.onClick.Invoke();yield return null;
        float pausedAt=seq.animationTime;yield return new WaitForSecondsRealtime(.35f);
        ensure(accountUi.isBlocking && Mathf.Abs(seq.animationTime-pausedAt)<.001f,"blocking account overlay freezes launch clock");
        ui.closeButton.onClick.Invoke();yield return null;
        yield return waitFor(()=>seq.state==launchSequence.phase.failed,"failed cloud start returns a retry state");
        ensure(requests.Count==1 && starts==0 && !gm.isGameActive && cloudAccount.instance.activeSessionId==null,"failed server confirmation cannot begin a run");
        var retry=gm.overlayObj.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="PlayButton");
        ensure(retry.gameObject.activeInHierarchy && retry.interactable && gm.overlayObj.transform.Find("launchStatus").GetComponent<TMPro.TMP_Text>().text.Contains("connection"),"connection error and usable retry action remain visible");
        capture("connection-retry");yield return null;
        rejectStart=false;clickStart();clickStart();yield return waitFor(()=>gm.isGameActive,"retry reaches actual gameplay");
        ensure(requests.Count==2 && requests[0]==requests[1] && starts==1,"retry reuses the same session UUID and starts exactly once");
        ensure(Camera.main.transform.position==new Vector3(0,0,-1) && Camera.main.orthographicSize==5.4f,"handoff restores exact gameplay camera");
        ensure(stars.GetComponent<ParticleSystem>().GetInstanceID()==starId && stars.starCount==starCount && stars.speedMultiplier==1 && stars.streakAmount==0,"same stars continue through handoff at gameplay speed");
        ensure(!seq.GetComponent<AudioSource>().isPlaying,"launch sound ends before the gameplay handoff");
        yield return new WaitForSecondsRealtime(.3f);
        ensure(!gm.playerController.getHasPredictedThisRound(),"held launch inputs do not fire the first prediction");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
        ensure(trials==0,"presentation does not upload research responses");
        gm.setCurrentHealth(0);yield return waitFor(()=>seq.state==launchSequence.phase.ended,"game over reaches replay state");
        yield return waitFor(()=>!cloudAccount.instance.busy&&!cloudAccount.instance.hasPendingSave,"completed test run saves before replay");
        float replayAt=Time.realtimeSinceStartup;
        UnityEngine.Object.FindFirstObjectByType<gameOverUi>().restartButton.onClick.Invoke();
        yield return waitFor(()=>gm.isGameActive,"Replay starts the next run");
        ensure(Time.realtimeSinceStartup-replayAt<1.5f && starts==2 && requests.Count==3 && requests[2]!=requests[1],"Replay uses a short transition and one new session");
        ensure(Time.timeScale==1 && !UnityEngine.Object.FindFirstObjectByType<gameOverUi>().gameOverPanel.activeSelf,"Replay restores time and dismisses results");
        cloudAccount.instance.requestTutorial();yield return waitFor(()=>SceneManager.GetActiveScene().name==cloudAccount.tutorialScene,"tutorial replay remains accessible");
        yield return null;yield return null;
        ensure(UnityEngine.Object.FindFirstObjectByType<launchSequence>()==null && UnityEngine.Object.FindFirstObjectByType<heliosEngineGlow>()==null && cloudAccount.instance.activeSessionId==null,"tutorial has no launch controller, engine overrides or normal session");
        cloudAccount.instance.enterPlay();yield return ready();
        ensure(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length==1 && UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length==1,"scene return has one EventSystem and one AudioListener");
        ensure(Time.timeScale==1 && !gameManager.instance.isGameActive,"scene return restores a fresh waiting view");
        result="PASS\n"+string.Join("\n",passed);File.WriteAllText(output+"checks.txt",result);
    }
    public static void movie()
    {
        result="recording";rejectStart=false;cloudAccount.testTransport=transport;cloudAccount.testApiTransport=transport;
        cloudAccount.instance.StartCoroutine(record());
    }
    private static IEnumerator record()
    {
        terminalVisualChecks.size(1280,720);cloudAccount.instance.enterPlay();yield return ready();
        string directory=output+"movie/";Directory.CreateDirectory(directory);
        var times=new List<double>();double started=Time.realtimeSinceStartupAsDouble;bool launched=false;
        int frame=0;
        while(Time.realtimeSinceStartupAsDouble-started<8)
        {
            double time=Time.realtimeSinceStartupAsDouble-started;
            if(time>=3.5 && !launched){clickStart();launched=true;}
            yield return new WaitForEndOfFrame();
            times.Add(Time.realtimeSinceStartupAsDouble-started);
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(directory+"frame-"+frame.ToString("D4")+".png",texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
            frame++;yield return null;
        }
        File.WriteAllText(directory+"times.json",Newtonsoft.Json.JsonConvert.SerializeObject(times));
        result="recorded "+frame+" frames";Time.timeScale=0;
    }
}
