using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// Runs the real Unity UI/REST adapter against Auth + Firestore emulators.
// Only a deliberately lost acknowledgement and a connection failure are simulated.
public static class firebaseIntegrationChecks
{
    public static string result="not run";
    private static readonly List<string> checks=new List<string>();
    private static cloudAccount account=>cloudAccount.instance;
    private static accountEntry entry=>UnityEngine.Object.FindFirstObjectByType<accountEntry>();
    private static researchConsentUi form=>UnityEngine.Object.FindFirstObjectByType<researchConsentUi>();
    private static bool loseNextCommit,networkDown;
    private static string password,email;
    private static long status;
    private static string responseBody;
    public static void run()
    {
        if(!Application.isPlaying || account.signedIn)throw new Exception("Use a signed-out Play session.");
        checks.Clear();result="running";cloudAccount.firebaseEmulator=true;cloudAccount.testApiTransport=null;cloudAccount.testTransport=transport;
        password="Local-only-"+Guid.NewGuid().ToString("N");email="unity-"+Guid.NewGuid().ToString("N")+"@example.test";
        account.StartCoroutine(check());
    }
    private static void ensure(bool value,string label)
    {if(!value){result="FAILED: "+label;throw new Exception(result);}checks.Add(label);}
    private static IEnumerator idle()
    {double deadline=Time.realtimeSinceStartupAsDouble+30;while(account.busy||account.saving){if(Time.realtimeSinceStartupAsDouble>deadline){result="FAILED: request timed out";throw new Exception(result);}yield return null;}yield return null;yield return null;}
    private static IEnumerator transport(string url,string method,string json,string token,Action<long,string> done)
    {
        ensure(url.StartsWith("http://127.0.0.1:"),"test never contacts a live backend");
        if(networkDown){yield return null;done(0,"");yield break;}
        using(var web=new UnityWebRequest(url,method)) {
            web.downloadHandler=new DownloadHandlerBuffer();
            if(json!=null){web.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));web.SetRequestHeader("Content-Type",url.Contains("securetoken.googleapis.com")?"application/x-www-form-urlencoded":"application/json");}
            if(!string.IsNullOrEmpty(token))web.SetRequestHeader("Authorization","Bearer "+token);
            yield return web.SendWebRequest();
            if(loseNextCommit && url.EndsWith(":commit") && web.responseCode==200){loseNextCommit=false;done(0,"");}
            else done(web.responseCode,web.downloadHandler.text);
        }
    }
    private static IEnumerator call(string operation,string json)
    {yield return account.firebaseTestApi(operation,json,(s,b)=>{status=s;responseBody=b;});}
    private static IEnumerator screen(string name)
    {yield return null;yield return new WaitForEndOfFrame();System.IO.Directory.CreateDirectory("Temp/FirebaseChecks");ScreenCapture.CaptureScreenshot("Temp/FirebaseChecks/"+name+".png");yield return null;yield return null;}
    private static IEnumerator confirmEmail()
    {
        string body=null;
        yield return transport("http://127.0.0.1:9099/emulator/v1/projects/demo-comet-catcher/oobCodes","GET",null,null,(s,b)=>{ensure(s==200,"local mailbox available");body=b;});
        var code=JObject.Parse(body)["oobCodes"].Last(c=>(string)c["email"]==email && (string)c["requestType"]=="VERIFY_EMAIL");
        account.useEmailLink("https://comet-catcher.firebaseapp.com/__/auth/action?apiKey=test-key&mode=verifyEmail&oobCode="+(string)code["oobCode"]);
        yield return idle();ensure(account.message.Contains("Email verified"),"verification link is accepted by Firebase Auth");
    }
    private static IEnumerator login()
    {entry.showLogin();entry.loginEmail.input.text=email;entry.loginPassword.input.text=password;entry.submitLogin();yield return idle();}
    private static IEnumerator check()
    {
        terminalVisualChecks.size(1280,720);entry.showLogin();yield return idle();
        entry.loginEmail.input.text="invalid@";entry.loginPassword.input.text="";entry.submitLogin();
        ensure(entry.loginEmail.error.gameObject.activeSelf && entry.loginPassword.error.gameObject.activeSelf,"malformed email and missing password stay beside the fields");
        entry.showCreate();entry.signupEmail.input.text=email;entry.signupPassword.input.text=password;entry.confirmPassword.input.text="mismatch";entry.submitSignup();
        ensure(entry.confirmPassword.error.gameObject.activeSelf && !account.busy,"mismatched passwords never reach Firebase");
        entry.confirmPassword.input.text=password;networkDown=true;entry.submitSignup();yield return idle();networkDown=false;
        ensure(entry.state==accountEntry.entryState.createAccount && entry.signupPassword.input.text==password && account.message.Contains("Couldn't connect"),"signup connection failure preserves entered values");
        entry.submitSignup();entry.submitSignup();yield return idle();
        ensure(entry.state==accountEntry.entryState.verifyEmail && !account.signedIn,"new Firebase account waits for verified email");yield return screen("verify-email");
        yield return login();ensure(!account.signedIn && entry.state==accountEntry.entryState.verifyEmail,"unverified account cannot enter consent or gameplay");
        account.useEmailLink("https://comet-catcher.firebaseapp.com/__/auth/action?apiKey=test-key&mode=verifyEmail&oobCode=invalid-test-code");yield return idle();
        ensure(!account.signedIn && account.message.Contains("invalid or expired"),"invalid verification link gives a safe retry instruction");
        yield return confirmEmail();
        entry.showLogin();entry.loginPassword.input.text="incorrect";entry.submitLogin();yield return idle();
        ensure(!account.signedIn && account.message=="Email or password is incorrect.","existing account rejects a wrong password safely");
        yield return login();ensure(account.signedIn && form!=null && !account.consentComplete,"verified login routes to the existing consent scene");
        typeof(cloudAccount).GetField("expiresAt",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(account,0d);
        account.refreshProgress();yield return idle();ensure(account.signedIn && !account.consentComplete,"expired access token refreshes through Firebase before reading account data");
        form.showAdult();form.adult.nameFields[0].text="Local test participant";form.adult.followUpNo.isOn=true;form.adult.agreement.isOn=true;
        loseNextCommit=true;form.continueAdult();form.continueAdult();yield return idle();
        ensure(!loseNextCommit && form!=null && !account.consentComplete && form.adult.nameFields[0].text=="Local test participant" && form.adult.agreement.isOn,"lost consent acknowledgement preserves form and prevents premature routing");yield return screen("consent-retry");
        form.continueAdult();yield return idle();
        ensure(account.consentComplete && account.profile.researchCollectionEnabled && SceneManager.GetActiveScene().name==cloudAccount.tutorialScene,"consent retry confirms the existing receipt and opens mandatory training");
        ensure(account.activeSessionId==null,"onboarding creates no normal game session");
        account.logOut();yield return idle();yield return login();
        ensure(SceneManager.GetActiveScene().name==cloudAccount.tutorialScene && account.consentComplete,"returning account retains consent and unfinished tutorial state");
        // Test persistence/routing only; the tutorial's physical input lessons are unchanged.
        bool trained=false;account.completeTutorial(v=>trained=v);yield return idle();ensure(trained && account.tutorialComplete,"tutorial completion receives server confirmation");
        account.enterPlay();yield return idle();ensure(SceneManager.GetActiveScene().name==cloudAccount.playScene,"saved onboarding reaches SampleScene");
        yield return screen("ready-to-start");
        string sessionId=Guid.NewGuid().ToString();var start=new cloudAccount.startRecord{sessionId=sessionId,gameVersion=cloudAccount.gameVersion,mode="standard"};
        loseNextCommit=true;yield return call("sessions",JsonUtility.ToJson(start));ensure(status==0,"lost session acknowledgement is simulated after the real commit");
        yield return call("sessions",JsonUtility.ToJson(start));ensure(status==200,"retry confirms the same session without a duplicate");
        var checkpoint=new cloudAccount.checkpoint{sessionId=sessionId,revision=1,score=4321,highestLevel=2,elapsedSeconds=7,completed=true};
        loseNextCommit=true;yield return call("checkpoints",JsonUtility.ToJson(checkpoint));ensure(status==0,"lost checkpoint acknowledgement is simulated after commit");
        yield return call("checkpoints",JsonUtility.ToJson(checkpoint));ensure(status==200,"same checkpoint safely retries after completion");
        var trial=new sequenceResult{roundIndex=0,correctQuadrant=1,predictedQuadrant=1,secondGuessQuadrant=-1,level=2,predictionTime=.3f,resolvedTime=.5f,outcome="Predicted",audioLoudness=new audioLoudnessRecord{profileId="test",model="test",notes=new List<notePlaybackRecord>{new notePlaybackRecord{clipName="C4",requestedFrequencyHz=392.15f,nominalPlaybackFrequencyHz=261.63f,pitchRatio=1.499f,sourceVolume=.3f,playbackSampleRate=44100}}}};
        var upload=new JObject{{"sessionId",sessionId},{"trial",JObject.Parse(JsonUtility.ToJson(trial))}};
        loseNextCommit=true;yield return call("trials",upload.ToString());ensure(status==0,"lost research acknowledgement is simulated after commit");
        yield return call("trials",upload.ToString());ensure(status==200,"research retry preserves one immutable trial and its exact note records");
        account.refreshProgress();yield return idle();ensure(account.progress.bestScore==4321 && account.progress.sessionsPlayed==1 && account.progress.sessionsCompleted==1,"account summary counts a retried run once");
        account.logOut();yield return idle();yield return login();ensure(account.progress.bestScore==4321 && account.tutorialComplete && SceneManager.GetActiveScene().name==cloudAccount.playScene,"logout and returning login restore progress and training from Firebase");
        account.requestConsent();yield return idle();form.decline();yield return idle();ensure(!account.profile.researchCollectionEnabled && SceneManager.GetActiveScene().name==cloudAccount.playScene,"declining later keeps gameplay accessible");
        trial.roundIndex=1;upload["trial"]=JObject.Parse(JsonUtility.ToJson(trial));yield return call("trials",upload.ToString());ensure(status==403 && responseBody.Contains("research_not_allowed"),"backend blocks old queued research after decline");
        string second=Guid.NewGuid().ToString();start.sessionId=second;yield return call("sessions",JsonUtility.ToJson(start));ensure(status==200,"declining player can start another run");
        checkpoint.sessionId=second;checkpoint.score=5000;yield return call("checkpoints",JsonUtility.ToJson(checkpoint));ensure(status==200,"declining player still saves scores");
        account.requestTutorial();yield return idle();ensure(SceneManager.GetActiveScene().name==cloudAccount.tutorialScene && account.tutorialComplete && account.activeSessionId==null,"tutorial replay preserves completion and has no normal session");
        account.logOut();yield return idle();
        cloudAccount.testTransport=null;cloudAccount.firebaseEmulator=false;password=email=null;entry.showLogin();yield return screen("login");
        result="PASS "+checks.Distinct().Count()+" checks\n"+string.Join("\n",checks.Distinct());System.IO.File.WriteAllText("Temp/FirebaseChecks/results.txt",result);Debug.Log(result);
    }
}
