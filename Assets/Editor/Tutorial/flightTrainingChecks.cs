using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// Editor-only verification. Injects keyboard events into the real controller, never into a player build.
public static class flightTrainingChecks
{
    public static string result="not run";
    public static bool captureVisuals;
    public static bool captureFullHd;
    public static void captureLesson(){cloudAccount.instance.StartCoroutine(visualLesson());}
    private static IEnumerator visualLesson()
    {
        terminalVisualChecks.login(false);while(cloudAccount.instance.busy)yield return null;
        cloudAccount.instance.enterPlay();yield return waitState(tutorialDirector.lesson.controls);
        controlLayout.select(false);captureVisuals=true;captureFullHd=false;yield return tutorial(false);
        Debug.Log("Visual lesson capture complete; no completion receipt sent.");
    }
    private static readonly List<string> checks=new List<string>();
    private static readonly Dictionary<string,int> completions=new Dictionary<string,int>();
    private static string owner="alice";
    private static bool failCompletion=true,failCheckpoint,failSession;
    private static int sessions,checkpoints,receipts;
    private static readonly List<float> shotTimes=new List<float>();
    public static void run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play mode.");
        UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        Application.runInBackground=true;
        checks.Clear();completions.Clear();sessions=checkpoints=receipts=0;owner="alice";failCompletion=true;failCheckpoint=false;failSession=false;
        result="running";cloudAccount.testTransport=transport;cloudAccount.testApiTransport=transport;
        cloudAccount.instance.StartCoroutine(runAll());
    }
    private static void ensure(bool value,string name)
    {if(!value){result="FAILED: "+name;Debug.LogError(result);throw new Exception(result);}checks.Add(name);}
    private static IEnumerator idle(){while(cloudAccount.instance.busy||cloudAccount.instance.saving)yield return null;yield return null;}
    private static IEnumerator transport(string path,string method,string json,string bearer,Action<long,string> done)
    {
        yield return null;
        if(path.Contains("/token") || path.Contains("accounts:signInWithPassword"))done(200,"{\"idToken\":\"test\",\"refreshToken\":\"test\",\"expiresIn\":3600}");
        else if(path.Contains("accounts:lookup"))done(200,"{\"users\":[{\"localId\":\""+owner+"\",\"emailVerified\":true}]}");
        else if(path.EndsWith("/profile"))done(200,profile());
        else if(path.EndsWith("/progress"))done(200,"{\"bestScore\":1234,\"highestLevel\":2,\"sessionsPlayed\":3,\"sessionsCompleted\":2}");
        else if(path.EndsWith("/tutorial")){receipts++;if(failCompletion){failCompletion=false;done(503,"{}");}else{completions[owner]=1;done(200,profile());}}
        else if(path.EndsWith("/sessions")){if(failSession)done(503,"{}");else{sessions++;done(200,"{}");}}
        else if(path.EndsWith("/checkpoints")){checkpoints++;done(failCheckpoint?503:200,"{}");}
        else {result="FAILED: unexpected transport "+path;done(500,"{}");}
    }
    private static string profile()=>"{\"consentVersion\":\"2026-10-25-v1\",\"consentStatus\":\"declined\",\"researchCollectionEnabled\":false,\"nickname\":\""+owner+"\",\"tutorialVersion\":"+(completions.ContainsKey(owner)?completions[owner]:0)+"}";
    private static IEnumerator keys(params Key[] values)
    {InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(values));yield return null;yield return null;}
    private static void next()
    {
        var button=tutorialDirector.instance.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        Array.Find(button,b=>b.name=="continue").onClick.Invoke();
    }
    private static IEnumerator waitState(tutorialDirector.lesson state,float seconds=5)
    {
        float until=Time.realtimeSinceStartup+seconds;
        while(tutorialDirector.instance==null || tutorialDirector.instance.state!=state)
        {if(Time.realtimeSinceStartup>until){ensure(false,"reached "+state);yield break;}yield return null;}
        yield return null;
    }
    private static IEnumerator runAll()
    {
        var account=cloudAccount.instance;
        account.logIn("test@example.invalid","not-a-real-password");yield return idle();
        account.enterPlay();yield return waitState(tutorialDirector.lesson.controls);
        ensure(account.activeSessionId==null && sessions==0,"first login routes to training without a cloud game session");
        yield return keys();yield return keys(Key.Tab);
        ensure(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.transform.IsChildOf(tutorialDirector.instance.transform),"Tab focuses tutorial controls instead of the hidden account panel");
        var exitUi=UnityEngine.Object.FindFirstObjectByType<accountUi>();exitUi.accountButton.onClick.Invoke();
        yield return keys();yield return keys(Key.Tab);
        ensure(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.transform.IsChildOf(exitUi.panel.transform),"account overlay owns keyboard focus while open");
        yield return keys();exitUi.playButton.onClick.Invoke();yield return null;yield return null;
        ensure(SceneManager.GetActiveScene().name==cloudAccount.accountScene && Time.timeScale==1,"Exit training returns to the account without logging out or leaving time paused");
        ensure(!account.tutorialComplete,"abandoning training does not complete it");
        account.enterPlay();yield return waitState(tutorialDirector.lesson.controls);
        controlLayout.select(false);yield return tutorial(false);
        ensure(sessions==0 && checkpoints==0 && receipts==0,"all required practice events create no runs, checkpoints or early receipts");
        next();yield return idle();
        ensure(tutorialDirector.instance.state==tutorialDirector.lesson.complete && !account.tutorialComplete,"failed completion save stays on final step");
        next();yield return idle();yield return null;yield return null;
        ensure(account.tutorialComplete && receipts==2,"completion saved only after required impact and restart, retry succeeds");
        yield return null;yield return null;
        ensure(SceneManager.GetActiveScene().name==cloudAccount.playScene && sessions==0,"entering normal scene does not start a cloud run");
        yield return normalControls();
        var ui=UnityEngine.Object.FindFirstObjectByType<accountUi>();ui.accountButton.onClick.Invoke();
        failCheckpoint=true;account.requestTutorial();yield return idle();
        ensure(SceneManager.GetActiveScene().name==cloudAccount.playScene && account.hasPendingSave,"failed active-run save prevents tutorial exit");
        failCheckpoint=false;account.retrySave();yield return idle();account.requestTutorial();yield return waitState(tutorialDirector.lesson.controls);
        ensure(account.tutorialComplete && account.activeSessionId==null,"replay retains completion and detaches the saved run");
        int oldSessions=sessions,oldCheckpoints=checkpoints;
        controlLayout.select(true);yield return tutorial(true);
        ensure(sessions==oldSessions && checkpoints==oldCheckpoints,"left-hand replay does not alter cloud progress");
        account.logOut();yield return idle();yield return null;
        owner="bob";account.logIn("test@example.invalid","not-a-real-password");yield return idle();
        ensure(!account.tutorialComplete,"different account is not onboarded by another player's completion");
        account.enterPlay();yield return waitState(tutorialDirector.lesson.controls);
        account.logOut();yield return idle();yield return null;
        owner="alice";account.logIn("test@example.invalid","not-a-real-password");yield return idle();
        ensure(account.tutorialComplete,"logout/relogin restores this account's server completion");
        account.enterPlay();yield return null;yield return null;
        ensure(SceneManager.GetActiveScene().name==cloudAccount.playScene,"returning account bypasses completed training");
        ensure(Time.timeScale==1 && UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length==1 && UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length==1 && UnityEngine.Object.FindObjectsByType<gameManager>(FindObjectsSortMode.None).Length==1,"exit restores time and leaves exactly one scene service of each kind");
        yield return keys();controlLayout.select(false);
        result="PASS: "+checks.Count+" checks\n"+string.Join("\n",checks);
        System.IO.File.WriteAllText("C:/Users/Ethan/Documents/Codex/2026-10-24/lo/work/flight-runtime-checks.txt",result);Debug.Log(result);
    }
    private static IEnumerator cue()
    {
        float end=Time.realtimeSinceStartup+10;
        while(tutorialDirector.instance.cuePlaying){if(Time.realtimeSinceStartup>=end){ensure(false,"audio cue finishes");yield break;}yield return null;}
        yield return null;
    }
    private static IEnumerator tutorial(bool left)
    {
        string originalPreset=JsonUtility.ToJson(gameSettings.instance.getCurrentPreset());
        if(captureVisuals)gameManager.instance.onCometCreated+=tutorialPolishChecks.observeArrival;
        tutorialHitObserver.hits=0;
        gameManager.instance.onCometCreated+=observeHit;
        yield return snap("opening",left);next();next();yield return snap("spectrum",left);next();
        for(int q=0;q<4;q++)
        {
            ensure(tutorialDirector.instance.state==tutorialDirector.lesson.demonstration && tutorialDirector.instance.demoQuadrant==q,"each quadrant receives its own demonstration");
            next();ensure(tutorialDirector.instance.demoQuadrant==q,"Continue cannot skip a playing demonstration");
            if(q==0)
            {
                yield return new WaitForSecondsRealtime(.25f);
                Array.Find(tutorialDirector.instance.GetComponentsInChildren<UnityEngine.UI.Button>(true),b=>b.name=="listen").onClick.Invoke();
            }
            yield return cue();
            var record=gameManager.instance.frequencyManager.createLoudnessRecord();var range=gameManager.instance.frequencyManager.quadrants[q];
            ensure(record.notes.Count==3 && record.notes.TrueForAll(n=>n.requestedFrequencyHz>range.minFrequency && n.requestedFrequencyHz<range.maxFrequency),"three calibrated pitches stay inside their shared quadrant");
            ensure(record.notes[0].requestedFrequencyHz!=record.notes[1].requestedFrequencyHz,"teaching pitches are distinct");
            yield return snap("quadrant"+(q+1),left);next();
        }
        yield return cue();yield return snap("octave",left);next();yield return waitState(tutorialDirector.lesson.correct);yield return cue();yield return snap("prediction",left);
        next();ensure(tutorialDirector.instance.state==tutorialDirector.lesson.correct,"click-through cannot pass interactive correct trial");
        yield return keys();yield return keys(controlLayout.key(1,!left),Key.F);yield return new WaitForSecondsRealtime(.25f);
        ensure(tutorialDirector.instance.state==tutorialDirector.lesson.correct && gameManager.instance.playerController.getPlayerQuadrant()==-1,"inactive layout does not select or fire");
        yield return keys();yield return keys(controlLayout.key(2,left),Key.F);yield return new WaitForSecondsRealtime(.25f);
        ensure(tutorialDirector.instance.state==tutorialDirector.lesson.correct && !gameManager.instance.playerController.getHasPredictedThisRound(),"wrong guided input gets retry without spending a prediction");
        yield return keys();yield return keys(controlLayout.key(1,left),Key.F);yield return waitState(tutorialDirector.lesson.success);
        ensure(gameManager.instance.aura>0 && gameManager.instance.getSessionData().results.Count==0,"correct practice triggers Aura without research records");
        yield return snap("success",left);next();yield return cue();yield return snap("deliberate-miss",left);
        yield return keys(controlLayout.key(0,left),Key.F);yield return new WaitForSecondsRealtime(.3f);
        ensure(tutorialDirector.instance.state==tutorialDirector.lesson.miss,"held F cannot cross lesson boundary");
        yield return keys();yield return keys(controlLayout.key(0,left),Key.F);yield return waitState(tutorialDirector.lesson.spotlight,10);
        ensure(Time.timeScale==0 && gameManager.instance.currentHealth==100,"entrance and spotlight preserve hull health");
        var hud=UnityEngine.Object.FindFirstObjectByType<auraHud>();
        ensure(hud.auraText.text==Mathf.RoundToInt(gameManager.instance.aura).ToString(),"Aura number settles before freezing");
        yield return snap("spotlight",left);next();yield return snap("recovery",left);yield return keys(controlLayout.key(3,left),Key.F);yield return new WaitForSecondsRealtime(.3f);
        ensure(UnityEngine.Object.FindFirstObjectByType<practiceTarget>()!=null,"held F cannot skip paused explanation");
        yield return keys();yield return keys(controlLayout.key(3,left),Key.F);yield return waitState(tutorialDirector.lesson.secondGuess);
        ensure(tutorialHitObserver.hits==1,"the first actual projectile contact destroys the practice comet");
        ensure(gameManager.instance.currentHealth>0,"one-hit recovery does not cause accidental death");
        yield return snap("second-guess",left);next();yield return new WaitForSecondsRealtime(.2f);
        ensure(tutorialDirector.instance.state==tutorialDirector.lesson.release,"impact trial waits for F release");
        yield return keys();yield return waitState(tutorialDirector.lesson.impactSpotlight,10);yield return snap("impact-spotlight",left);next();
        yield return keys(controlLayout.key(3,left),Key.F);yield return waitState(tutorialDirector.lesson.gameOver,12);
        ensure(gameManager.instance.currentHealth<=0 && !gameManager.instance.isGameActive,"real collision ends practice despite held fire");
        yield return snap("practice-gameover",left);next();ensure(tutorialDirector.instance.state==tutorialDirector.lesson.gameOver,"generic Next cannot replace Replay");
        UnityEngine.Object.FindFirstObjectByType<gameOverUi>().restartButton.onClick.Invoke();yield return waitState(tutorialDirector.lesson.assessment);yield return cue();
        ensure(!tutorialDirector.instance.assessmentDone,"Replay alone cannot complete training");
        yield return keys();yield return keys(controlLayout.key(0,left),Key.F);yield return waitState(tutorialDirector.lesson.assessmentRetry);
        ensure(!tutorialDirector.instance.assessmentDone,"wrong independent attempt receives a hint instead of completion");
        next();yield return cue();yield return keys();yield return keys(controlLayout.key(2,left),Key.F);yield return waitState(tutorialDirector.lesson.complete);
        ensure(tutorialDirector.instance.assessmentDone && gameManager.instance.currentHealth==100,"independent correct interception is required after Replay");yield return snap("completion",left);
        ensure(JsonUtility.ToJson(gameSettings.instance.getCurrentPreset())==originalPreset,"tutorial audio leaves the saved preset unchanged");
        gameManager.instance.onCometCreated-=observeHit;
        if(captureVisuals)gameManager.instance.onCometCreated-=tutorialPolishChecks.observeArrival;
    }
    private static void observeHit(GameObject comet){comet.AddComponent<tutorialHitObserver>();}
    private static IEnumerator snap(string name,bool left)
    {
        if(!captureVisuals)yield break;
        yield return new WaitForSecondsRealtime(.2f);terminalVisualChecks.capture(name+(left?"-left":"-right")+"-runtime");yield return new WaitForSecondsRealtime(.15f);
        if(captureFullHd && !left && (name=="spectrum" || name=="quadrant2" || name=="spotlight" || name=="impact-spotlight"))
        {terminalVisualChecks.size(1920,1080);yield return new WaitForSecondsRealtime(.3f);terminalVisualChecks.capture(name+"-1920x1080");yield return new WaitForSecondsRealtime(.2f);terminalVisualChecks.size(1280,720);yield return new WaitForSecondsRealtime(.2f);}
    }
    private static object field(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    private static void set(object obj,string name,object value)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
    private static void round(gameManager manager,int lane)
    {
        manager.StopAllCoroutines();set(manager,"currentQuadrant",lane);set(manager,"predictedQuadrant",-1);set(manager,"predictionPenaltyApplied",false);
        set(manager,"roundStartTime",Time.time);set(manager,"secondGuessRecorded",false);set(manager,"secondGuessQuadrant",-1);
        manager.roundState=gameManager.roundStateType.prediction;
        ((Action<int>)field(manager,"onRoundStart"))?.Invoke(lane);
    }
    private static IEnumerator normalControls()
    {
        var account=cloudAccount.instance;var manager=gameManager.instance;var player=manager.playerController;
        manager.startGame();yield return idle();ensure(manager.isGameActive,"normal run starts after training and creates a session");
        manager.StopAllCoroutines();controlLayout.select(false);yield return keys();
        player.onShotFired+=(lane,prediction)=>shotTimes.Add(Time.time);
        foreach(bool left in new[]{false,true})
        {
            controlLayout.select(left);yield return keys();round(manager,1);yield return keys();shotTimes.Clear();
            yield return keys(controlLayout.key(1,left),Key.F);yield return new WaitForSeconds(.35f);
            ensure(manager.roundState==gameManager.roundStateType.resolved && shotTimes.Count==1,"correct prediction fires and rewards once: "+left);
            int records=manager.getSessionData().results.Count;
            yield return new WaitForSeconds(.25f);ensure(manager.getSessionData().results.Count==records,"held F cannot duplicate result records: "+left);
            round(manager,3);yield return keys(controlLayout.key(0,left),Key.F);yield return new WaitForSeconds(.25f);
            ensure(manager.roundState==gameManager.roundStateType.prediction,"new round requires F release: "+left);
            yield return keys();shotTimes.Clear();
            float previousDelay=gameSettings.instance.getCurrentPreset().cometSpawnDelay;gameSettings.instance.getCurrentPreset().cometSpawnDelay=1.8f;
            yield return keys(controlLayout.key(0,left),Key.F);yield return new WaitForSeconds(.45f);
            ensure(shotTimes.Count>=2 && (int)field(manager,"secondGuessQuadrant")==-1,"same wrong-lane autofire leaves second guess available: "+left);
            yield return keys(controlLayout.key(0,left),controlLayout.key(2,left),Key.F);yield return new WaitForSeconds(.25f);
            ensure(player.getPlayerQuadrant()==2 && (int)field(manager,"secondGuessQuadrant")==2,"latest held key selects lane and first fired switch captures second guess: "+left);
            yield return keys(controlLayout.key(0,left),controlLayout.key(2,left),controlLayout.key(1,left),Key.F);yield return new WaitForSeconds(.25f);
            ensure(player.getPlayerQuadrant()==1 && (int)field(manager,"secondGuessQuadrant")==2,"later switches cannot overwrite captured second guess: "+left);
            int count=shotTimes.Count;yield return keys(controlLayout.key(0,left),controlLayout.key(2,left),Key.F);yield return new WaitForSeconds(.25f);
            ensure(shotTimes.Count==count,"releasing selected key stops fire although older lane keys remain held: "+left);
            for(int i=1;i<shotTimes.Count;i++)ensure(shotTimes[i]-shotTimes[i-1]>=player.shotInterval-.002f,"shot cadence respects interval: "+left+" tick "+i);
            var ui=UnityEngine.Object.FindFirstObjectByType<accountUi>();ui.accountButton.onClick.Invoke();count=shotTimes.Count;
            yield return keys(controlLayout.key(3,left),Key.F);yield return new WaitForSecondsRealtime(.2f);ui.closeButton.onClick.Invoke();yield return new WaitForSeconds(.25f);
            ensure(shotTimes.Count==count,"account pause/resume requires fresh F: "+left);
            manager.StopAllCoroutines();foreach(var comet in UnityEngine.Object.FindObjectsByType<cometBehavior>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(comet.gameObject);
            foreach(var b in UnityEngine.Object.FindObjectsByType<bulletBehavior>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(b.gameObject);
            gameSettings.instance.getCurrentPreset().cometSpawnDelay=previousDelay;
            yield return keys();
        }
        // Timeout branch is the same backup path, with no initial prediction.
        round(manager,3);manager.roundState=gameManager.roundStateType.cometActive;player.setHasPredictedThisRound(true);yield return keys();shotTimes.Clear();
        yield return keys(controlLayout.key(0,controlLayout.leftHanded),Key.F);yield return new WaitForSeconds(.25f);
        ensure(shotTimes.Count>=1 && (int)field(manager,"predictedQuadrant")==-1,"timeout uses backup fire, not a fabricated prediction");        yield return keys();shotTimes.Clear();player.shotInterval=.35f;
        yield return keys(controlLayout.key(0,controlLayout.leftHanded),Key.F);yield return new WaitForSeconds(.8f);
        ensure(shotTimes.Count>=2 && shotTimes.Count<=3,"Inspector interval tuning changes held-fire cadence");
        for(int i=1;i<shotTimes.Count;i++)ensure(shotTimes[i]-shotTimes[i-1]>=.348f,"tuned cooldown is respected");
        player.shotInterval=.2f;int shotsBefore=shotTimes.Count;
        controlLayout.select(!controlLayout.leftHanded);yield return null;yield return keys(controlLayout.key(0,controlLayout.leftHanded),Key.F);yield return new WaitForSeconds(.25f);
        ensure(shotTimes.Count==shotsBefore,"switching layouts requires fresh F");
        var settings=UnityEngine.Object.FindFirstObjectByType<settingsUi>();settings.show();
        yield return new WaitForSecondsRealtime(.1f);settings.close();yield return new WaitForSeconds(.25f);
        ensure(shotTimes.Count==shotsBefore && Time.timeScale==1,"Settings overlay blocks fire and resume requires fresh F");
        yield return keys();yield return keys(controlLayout.key(0,controlLayout.leftHanded),Key.F);
        manager.setCurrentHealth(0);yield return null;yield return idle();
        ensure(!manager.isGameActive && Time.timeScale==0,"normal game over stops held fire and saves final summary");
        string oldSession=account.activeSessionId;
        failSession=true;UnityEngine.Object.FindFirstObjectByType<gameOverUi>().restartButton.onClick.Invoke();yield return idle();
        ensure(!manager.isGameActive && UnityEngine.Object.FindFirstObjectByType<gameOverUi>().gameOverPanel.activeSelf,"failed Replay session start leaves game-over actions visible for retry");
        failSession=false;
        UnityEngine.Object.FindFirstObjectByType<gameOverUi>().restartButton.onClick.Invoke();yield return idle();
        ensure(manager.isGameActive && account.activeSessionId!=oldSession && !UnityEngine.Object.FindFirstObjectByType<gameOverUi>().gameOverPanel.activeSelf,"normal Replay starts a distinct cloud run and hides game over");
        round(manager,0);shotsBefore=shotTimes.Count;yield return new WaitForSeconds(.3f);
        ensure(shotTimes.Count==shotsBefore,"Replay cannot inherit a held F prediction");
        yield return keys();
    }
}


