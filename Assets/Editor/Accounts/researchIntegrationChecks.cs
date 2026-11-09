using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// Real game/controller/audio and real Firebase emulators. Fault injection only
// drops a response after commit, or temporarily simulates a disconnected network.
public static class researchIntegrationChecks
{
    public static string result="not run";
    private static readonly List<string> checks=new List<string>();
    private static cloudAccount account=>cloudAccount.instance;
    private static gameManager game=>gameManager.instance;
    private static bool offline,loseResearchAck;
    private static long status;
    private static string body;
    private static int lostAcks;
    private const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object field(object o,string name)=>o.GetType().GetField(name,flags).GetValue(o);
    private static void set(object o,string name,object value)=>o.GetType().GetField(name,flags).SetValue(o,value);
    private static object invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,flags).Invoke(o,args);
    private static string player=>"players/"+(string)field(account,"userId");
    private const string database="http://127.0.0.1:8080/v1/projects/demo-comet-catcher/databases/(default)/documents/";
    private static void ensure(bool ok,string label)
    {if(!ok){result="FAILED: "+label;throw new Exception(result);}checks.Add(label);}
    private static IEnumerator until(Func<bool> done,string label,float seconds=30)
    {double end=Time.realtimeSinceStartupAsDouble+seconds;while(!done()){ensure(Time.realtimeSinceStartupAsDouble<end,"timeout: "+label);yield return null;}}
    private static IEnumerator idle(){yield return until(()=>!account.busy&&!account.saving,"account request");yield return null;yield return null;}
    private static IEnumerator transport(string url,string method,string json,string token,Action<long,string> done)
    {
        if(!url.StartsWith("http://127.0.0.1:"))throw new Exception("Emulator-only test.");
        if(offline){yield return null;done(0,"");yield break;}
        using(var web=new UnityWebRequest(url,method)) {
            web.downloadHandler=new DownloadHandlerBuffer();
            if(json!=null){web.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));web.SetRequestHeader("Content-Type",url.Contains("securetoken")?"application/x-www-form-urlencoded":"application/json");}
            if(token!=null)web.SetRequestHeader("Authorization","Bearer "+token);
            yield return web.SendWebRequest();
            if(loseResearchAck && url.EndsWith(":commit") && json.Contains("/trials/") && web.responseCode==200) {
                loseResearchAck=false;lostAcks++;done(0,"");
            } else done(web.responseCode,web.downloadHandler.text);
        }
    }
    private static IEnumerator read(string path)
    {yield return transport(database+path,"GET",null,(string)field(account,"accessToken"),(s,b)=>{status=s;body=b;});ensure(status==200,"database read confirmed");}
    private static JObject values(string response)
    {return (JObject)typeof(cloudAccount).GetMethod("decodeFields",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{JObject.Parse(response)["fields"]});}
    private static IEnumerator list(string path,Action<int> done)
    {yield return read(path);done((JObject.Parse(body)["documents"] as JArray)?.Count??0);}
    private static IEnumerator keys(params Key[] held)
    {InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(held));yield return null;yield return null;}
    private static IEnumerator newAccount(bool agrees,Action<string,string> credentials)
    {
        string email="research-"+Guid.NewGuid().ToString("N")+"@example.test",password="Test-only-"+Guid.NewGuid().ToString("N");
        const string auth="http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:";
        yield return transport(auth+"signUp?key=test-key","POST",new JObject{{"email",email},{"password",password},{"returnSecureToken",true}}.ToString(),null,(s,b)=>{status=s;body=b;});
        ensure(status==200,"local test identity created");string id=(string)JObject.Parse(body)["localId"];
        yield return transport(auth+"update?key=test-key","POST",new JObject{{"localId",id},{"emailVerified",true}}.ToString(),"owner",(s,b)=>status=s);
        ensure(status==200,"emulator test mailbox verified");
        account.logIn(email,password);yield return idle();ensure(account.signedIn,"real Firebase login accepted");
        bool saved=false;
        account.saveConsent(new consentSubmission {requestId=Guid.NewGuid().ToString(),formVersion=researchConsentState.formVersion,
            decision=agrees?"adultAgreed":"declined",adultAgreement=agrees},ok=>saved=ok);
        yield return idle();ensure(saved,"consent confirmed by server");
        account.completeTutorial(ok=>saved=ok);yield return idle();ensure(saved,"fixture onboarding confirmed");
        account.enterPlay();yield return idle();yield return new WaitForSecondsRealtime(.1f);credentials(email,password);
    }
    private static IEnumerator start()
    {
        // Short runtime-only conditions; no serialized presets or scenes change.
        gameSettings.instance.isCustomMode=true;
        gameSettings.instance.customPreset=new gameSettings.levelPreset {notesPerSequence=3,noteDuration=.4f,pauseBetweenNotes=.15f,
            octaveShifts=new[]{-1,0,1},predictionWindow=1,cometSpawnDelay=1.5f,pauseBetweenRounds=.25f,cometSpeed=2};
        yield return keys();
        account.requestStart(()=>invoke(game,"beginGame"));yield return idle();
        ensure(game.isGameActive,"actual gameplay begins with a confirmed cloud session");
        yield return new WaitForSecondsRealtime(.12f);
    }
    public static void run()
    {
        if(!Application.isPlaying || account.signedIn)throw new Exception("Use a signed-out Play session.");
        result="running";checks.Clear();offline=false;lostAcks=0;
        cloudAccount.firebaseEmulator=true;cloudAccount.testTransport=transport;cloudAccount.testApiTransport=null;
        account.StartCoroutine(check());
    }
    private static IEnumerator check()
    {
        string email=null,password=null;yield return newAccount(true,(e,p)=>{email=e;password=p;});
        yield return start();string run=account.activeSessionId,path=player+"/sessions/"+run;
        int target=(int)field(game,"currentQuadrant"),wrong=(target+1)%4;
        yield return keys(controlLayout.key(wrong,controlLayout.leftHanded),Key.F);
        yield return new WaitForSecondsRealtime(.3f);
        var recording=(researchTrialRecord)field(game.research,"trial");
        ensure(recording.firstDecision!=null && recording.firstDecision.quadrant==wrong,"accepted real wrong shot records first decision");
        ensure(recording.secondDecision==null,"autofire in initial wrong lane does not create a second guess");
        yield return keys(controlLayout.key(target,controlLayout.leftHanded),Key.F);
        yield return new WaitForSecondsRealtime(.25f);
        ensure(recording.secondDecision!=null && recording.secondDecision.quadrant==target,"explicit lane switch plus fired shot records second guess");
        double secondTime=recording.secondDecision.atMs;
        yield return keys();Time.timeScale=0;yield return null;yield return null;
        double before=(double)invoke(game.research,"now");yield return new WaitForSecondsRealtime(.4f);
        double after=(double)invoke(game.research,"now");ensure(after-before<2,"research clock excludes deliberate pause");Time.timeScale=1;
        yield return until(()=>(float)field(game,"cometSpawnTime")>=0,"comet emergence",10);
        yield return keys(controlLayout.key(target,controlLayout.leftHanded),Key.F);
        yield return until(()=>game.roundState==gameManager.roundStateType.resolved,"actual backup-shot destruction",15);
        yield return keys();
        ensure(recording.resolution=="Destroyed" && recording.secondDecision.atMs==secondTime,"post-spawn recovery preserves the first pre-reveal second guess");
        yield return until(()=>game.roundState==gameManager.roundStateType.prediction,"next trial",15);
        yield return new WaitForSecondsRealtime(.1f);target=(int)field(game,"currentQuadrant");
        loseResearchAck=true;
        yield return keys(controlLayout.key(target,controlLayout.leftHanded),Key.F);yield return keys();
        yield return until(()=>lostAcks>0,"lost acknowledgement",10);
        account.retrySave();yield return idle();
        ensure(!account.hasPendingSave,"retry confirms real committed trial and notes");
        yield return read(path+"/trials/0");var first=values(body);
        ensure((bool)first["wasPaused"] && first["secondDecision"]["atMs"].Type!=JTokenType.Null,"stored pause flag and millisecond second-decision time");
        ensure((double)first["secondDecision"]["atMs"]<(double)first["cometSpawnedAtMs"],"second guess precedes actual emergence");
        yield return read(path+"/trials/0/notes/0");var tone=values(body);
        ensure((double)tone["requestedFrequencyHz"]>0 && (double)tone["startedAtMs"]>=0 && first["octaveShift"].Type==JTokenType.Integer,"actual calibrated playback saves pitch, octave, gain and onset");
        yield return read(path+"/trials/1");var correct=values(body);
        ensure((string)correct["resolution"]=="Predicted" && correct["secondDecision"].Type==JTokenType.Null && (int)correct["noteCount"]==1,"correct early prediction stores only initiated tone and null absent decision");
        int count=0;yield return list(path+"/trials/1/notes",n=>count=n);ensure(count==1,"lost acknowledgement did not duplicate notes");
        System.IO.Directory.CreateDirectory("Temp/ResearchChecks");System.IO.File.WriteAllText("Temp/ResearchChecks/trial-example.json",first.ToString());

        // Queue a completed result offline, then simulate an abrupt client reload:
        // no logout/exit flush is allowed to rescue the request for this check.
        yield return until(()=>game.roundState==gameManager.roundStateType.prediction,"third trial",15);offline=true;
        target=(int)field(game,"currentQuadrant");yield return keys(controlLayout.key(target,controlLayout.leftHanded),Key.F);yield return keys();
        yield return idle();ensure(account.hasPendingSave,"network failure retains pending research");
        string key=(string)typeof(cloudAccount).GetProperty("outboxKey",flags).GetValue(account);
        ensure(PlayerPrefs.HasKey(key),"pending records persisted under authenticated account key");
        string backup=PlayerPrefs.GetString(key);ensure(!backup.Contains(password)&&!backup.Contains(email),"local backup contains no password or email");
        set(account,"<activeSessionId>k__BackingField",null);
        foreach(string name in new[]{"accessToken","refreshToken","userId","ownerId","pending","latest"})set(account,name,null);
        ((Queue<string>)field(account,"researchQueue")).Clear();set(account,"researchAllowedForRun",false);set(account,"profileLoaded",false);
        Time.timeScale=1;SceneManager.LoadScene(cloudAccount.accountScene);yield return null;yield return null;offline=false;
        ensure(PlayerPrefs.GetString(key)==backup,"simulated reload keeps exact retry payloads on disk");

        yield return newAccount(false,(e,p)=>{});
        ensure(!account.hasPendingSave && PlayerPrefs.GetString(key)==backup,"different account cannot adopt or clear original pending research");
        yield return start();string declinedPath=player+"/sessions/"+account.activeSessionId;
        ensure(game.research==null && !account.canRecordResearch,"declining player never starts the research recorder");
        yield return until(()=>!game.isGameActive,"natural timeout and collision",30);
        account.retrySave();yield return idle();
        yield return list(declinedPath+"/trials",n=>count=n);ensure(count==0,"declining player stores no trials or tones");
        yield return read(declinedPath);var declined=values(body);
        ensure((bool)declined["completed"] && (long)declined["startedAtUnixMs"]>0 && (long)declined["endedAtUnixMs"]>=(long)declined["startedAtUnixMs"],"declining player still saves progress and run start/end");
        account.logOut();yield return idle();
        account.logIn(email,password);yield return idle();
        ensure(account.signedIn && !account.hasPendingSave && !PlayerPrefs.HasKey(key),"same-account login restores and acknowledges durable pending writes");
        yield return read(path+"/trials/2");ensure((string)values(body)["resolution"]=="Predicted","offline completed trial survives simulated client reload");
        yield return list(path+"/trials/2/notes",n=>count=n);ensure(count==1,"restored retries preserve one copy of each tone");

        yield return start();string timeoutPath=player+"/sessions/"+account.activeSessionId;
        yield return until(()=>!game.isGameActive,"consented timeout/collision",30);account.retrySave();yield return idle();
        yield return read(timeoutPath+"/trials/0");var timeout=values(body);
        ensure((string)timeout["resolution"]=="Hit" && timeout["firstDecision"].Type==JTokenType.Null &&
            timeout["secondDecision"].Type==JTokenType.Null && (string)timeout["predictionStatus"]=="notAttempted","actual no-response collision saves null decisions and spawn time");
        account.enterPlay();yield return idle();yield return start();string radiationPath=player+"/sessions/"+account.activeSessionId;
        yield return until(()=>(float)field(game,"cometSpawnTime")>=0,"radiation fixture emergence",10);
        game.currentHealth=.05f; // Test-only low-health fixture; the normal radiation component ends the run.
        yield return until(()=>!game.isGameActive,"radiation death",10);account.retrySave();yield return idle();
        yield return read(radiationPath+"/trials/0");ensure((string)values(body)["resolution"]=="RadiationDeath","actual radiation death records the otherwise-unresolved trial");
        yield return read(radiationPath);ensure((string)values(body)["endReason"]=="radiationDeath","session distinguishes radiation death from direct impact");
        account.enterPlay();yield return idle();
        yield return start();string exitedPath=player+"/sessions/"+account.activeSessionId;
        account.requestConsent();yield return idle();
        yield return read(exitedPath+"/trials/0");var exited=values(body);
        ensure((string)exited["resolution"]=="Interrupted" && (string)exited["interruptionReason"]=="consentReview","deliberate scene exit records unfinished trial without a fabricated answer");
        ensure(exited["firstDecision"].Type==JTokenType.Null,"unfinished trial retains null absent decision");
        account.requestTutorial();yield return idle();yield return new WaitForSecondsRealtime(.2f);
        ensure(SceneManager.GetActiveScene().name==cloudAccount.tutorialScene && account.activeSessionId==null && !account.hasPendingSave,"tutorial replay produces no session or research writes");
        account.logOut();yield return idle();
        cloudAccount.testTransport=null;cloudAccount.firebaseEmulator=false;email=password=null;
        result="PASS "+checks.Where(c=>!c.StartsWith("timeout:")).Distinct().Count()+" checks\n"+
            string.Join("\n",checks.Where(c=>!c.StartsWith("timeout:")).Distinct());
        System.IO.File.WriteAllText("Temp/ResearchChecks/results.txt",result);Debug.Log(result);
    }
}
