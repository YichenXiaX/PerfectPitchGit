using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Editor-only integration check. Mock transport is not compiled into player builds.
public static class cloudIntegrationChecks
{
    public static string result = "not run";
    private static bool unverified, failStart, failSave;
    private static int checkpoints, refreshes;
    private static string firstStart, firstSave;
    private static List<string> checks;
    public static void run()
    {
        if (!Application.isPlaying || cloudAccount.instance == null) throw new Exception("Enter Play mode first.");
        result="running"; checks=new List<string>(); checkpoints=refreshes=0; firstStart=firstSave=null;
        unverified=true;failStart=true;failSave=true;
        cloudAccount.testTransport=transport;
        cloudAccount.instance.StartCoroutine(check());
    }
    private static IEnumerator transport(string path,string method,string body,string bearer,Action<long,string> done)
    {
        yield return null;
        if(path.Contains("/token"))
        {
            if(path.Contains("refresh_token"))refreshes++;
            done(200,"{\"access_token\":\"test-access\",\"refresh_token\":\"test-refresh\",\"expires_in\":3600}");yield break;
        }
        if(path=="/auth/v1/user"){done(200,unverified?"{\"id\":\"test-owner\"}":"{\"id\":\"test-owner\",\"email_confirmed_at\":\"2026-10-25T00:00:00Z\"}");yield break;}
        if(path.EndsWith("/profile")){done(200,"{\"nickname\":\"testPlayer\"}");yield break;}
        if(path.EndsWith("/progress")){done(200,"{\"bestScore\":1234,\"highestLevel\":2,\"sessionsPlayed\":3,\"sessionsCompleted\":2}");yield break;}
        if(path.EndsWith("/sessions"))
        {
            if(firstStart==null)firstStart=body;else ensure(firstStart==body,"session creation retries keep the same UUID");
            if(failStart){failStart=false;done(0,"");}else done(200,"{}");yield break;
        }
        if(path.EndsWith("/checkpoints"))
        {
            checkpoints++;
            if(firstSave==null)firstSave=body;
            else if(checkpoints==2)ensure(firstSave==body,"checkpoint retries keep the identical payload and revision");
            ensure(!body.Contains("results")&&!body.Contains("email")&&!body.Contains("predictionTime"),"summary-only save payload");
            if(failSave){failSave=false;done(0,"");}else done(200,"{}");yield break;
        }
        if(path.StartsWith("/auth/v1/logout")){done(204,"");yield break;}
        throw new Exception("Unexpected request "+path);
    }
    private static void ensure(bool value,string name)
    {
        if(!value){result="FAILED: "+name;throw new Exception(result);}
        if(!checks.Contains(name))checks.Add(name);
    }
    private static IEnumerator waitIdle()
    {
        var a=cloudAccount.instance;
        while(a.busy||a.saving)yield return null;
    }
    private static IEnumerator check()
    {
        var a=cloudAccount.instance;
        a.logIn("test@example.invalid","not-a-real-password");yield return waitIdle();
        ensure(!a.signedIn,"unverified accounts are refused");
        unverified=false;a.logIn("test@example.invalid","not-a-real-password");yield return waitIdle();
        ensure(a.signedIn && a.progress.bestScore==1234 && a.profile.nickname=="testPlayer","login loads account progress");
        var settings=new GameObject("testSettings").AddComponent<gameSettings>();settings.level=2;
        var manager=new GameObject("testManager").AddComponent<gameManager>();manager.enabled=false;manager.currentHealth=100;
        typeof(gameManager).GetField("sessionData",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager,new gameSessionData());
        a.requestStart(()=>{});yield return waitIdle();
        ensure(a.activeSessionId==null,"failed server start prevents a game session");
        a.requestStart(()=>{});yield return waitIdle();
        ensure(a.activeSessionId!=null,"successful start unlocks the session");
        a.capture(false);a.retrySave();yield return waitIdle();
        ensure(a.hasPendingSave,"connection failure retains unsaved checkpoint");
        a.retrySave();yield return waitIdle();
        ensure(!a.hasPendingSave,"retry acknowledges saved progress");
        Time.timeScale=0;
        a.capture(true);a.retrySave();yield return waitIdle();
        ensure(!a.hasPendingSave && a.saveMessage=="Progress saved","final save succeeds while time is paused");
        typeof(cloudAccount).GetField("expiresAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(a,0d);
        a.refreshProgress();yield return waitIdle();
        ensure(refreshes>0,"expired access token is refreshed");
        a.logOut();yield return waitIdle();
        ensure(!a.signedIn && a.activeSessionId==null && !a.hasPendingSave,"logout clears identity and save queue");
        cloudAccount.testTransport=null;
        UnityEngine.Object.Destroy(manager.gameObject);
        if(settings!=null)UnityEngine.Object.Destroy(settings.gameObject);
        result="PASS: "+string.Join("; ",checks);
        Debug.Log(result);
    }

    private static cloudAccount.checkpoint lastCheckpoint;
    public static void runGameplay()
    {
        result="running gameplay";
        checks=new List<string>();
        cloudAccount.testTransport=gameplayTransport;
        cloudAccount.instance.StartCoroutine(gameplay());
    }
    private static IEnumerator gameplayTransport(string path,string method,string body,string bearer,Action<long,string> done)
    {
        yield return null;
        if(path.Contains("/token"))done(200,"{\"access_token\":\"test-access\",\"refresh_token\":\"test-refresh\",\"expires_in\":3600}");
        else if(path=="/auth/v1/user")done(200,"{\"id\":\"test-owner\",\"email_confirmed_at\":\"2026-10-25T00:00:00Z\"}");
        else if(path.EndsWith("/profile"))done(200,"{\"nickname\":\"testPlayer\"}");
        else if(path.EndsWith("/progress"))done(200,"{\"bestScore\":1234,\"highestLevel\":2,\"sessionsPlayed\":3,\"sessionsCompleted\":2}");
        else if(path.EndsWith("/checkpoints")){lastCheckpoint=JsonUtility.FromJson<cloudAccount.checkpoint>(body);done(200,"{}");}
        else if(path.EndsWith("/sessions"))done(200,"{}");
        else if(path.StartsWith("/auth/v1/logout"))done(204,"");
        else throw new Exception("Unexpected test request "+path);
    }
    private static IEnumerator gameplay()
    {
        var a=cloudAccount.instance;
        a.logIn("test@example.invalid","not-a-real-password");yield return waitIdle();
        UnityEngine.Object.FindFirstObjectByType<accountUi>().playButton.onClick.Invoke();
        while(gameManager.instance==null)yield return null;
        yield return null;
        var manager=gameManager.instance;
        manager.startGame();yield return waitIdle();
        ensure(manager.isGameActive,"real game starts after server session creation");
        ensure(manager.getSessionData().sessionId==a.activeSessionId,"game and cloud session IDs match");
        var ui=UnityEngine.Object.FindFirstObjectByType<accountUi>();
        ui.accountButton.onClick.Invoke();ensure(Time.timeScale==0,"account menu pauses the game");
        ui.closeButton.onClick.Invoke();ensure(Time.timeScale>0,"return button resumes the game");
        a.capture(false);a.retrySave();yield return waitIdle();
        ensure(lastCheckpoint!=null && !lastCheckpoint.completed,"active gameplay summary is saved");
        manager.setCurrentHealth(0);
        while(manager.isGameActive)yield return null;
        yield return waitIdle();
        ensure(lastCheckpoint.completed && !a.hasPendingSave,"game-over event saves final summary at timeScale zero");
        string firstId=a.activeSessionId;
        var over=UnityEngine.Object.FindFirstObjectByType<gameOverUi>();
        over.restartButton.onClick.Invoke();yield return waitIdle();
        ensure(manager.isGameActive && firstId!=a.activeSessionId && !over.gameOverPanel.activeSelf,"replay creates a new run and hides game-over after network start");
        ui.accountButton.onClick.Invoke();
        a.logOut();yield return waitIdle();yield return null;
        ensure(!a.signedIn && !a.hasPendingSave && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==cloudAccount.accountScene,"logout saves unfinished run and returns to login");
        cloudAccount.testTransport=null;
        result="PASS: "+string.Join("; ",checks);
        Debug.Log(result);
    }
}



