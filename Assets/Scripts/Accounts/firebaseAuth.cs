using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Firebase's documented REST API works in both the Unity Editor and WebGL.
// The native Firebase Unity SDK does not support WebGL. Tokens remain in memory.
public partial class cloudAccount
{
    private firebaseConfig firebase;
    private tokenResponse pendingFirebaseTokens;
    private string pendingFirebaseEmail, firebaseRecoveryCode;
#if COMET_FIREBASE_TEST
    // Only explicitly built local test players use the emulator. Never define for deployment.
    public static bool firebaseEmulator = true;
#elif UNITY_EDITOR
    public static bool firebaseEmulator;
#endif
    private string firebaseProject {
        get {
#if UNITY_EDITOR || COMET_FIREBASE_TEST
            if(firebaseEmulator) return "demo-comet-catcher";
#endif
            return firebase.projectId;
        }
    }
    private string firebaseAuthUrl {
        get {
#if UNITY_EDITOR || COMET_FIREBASE_TEST
            if(firebaseEmulator) return "http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1";
#endif
            return firebase.authUrl;
        }
    }
    private string firebaseTokenUrl {
        get {
#if UNITY_EDITOR || COMET_FIREBASE_TEST
            if(firebaseEmulator) return "http://127.0.0.1:9099/securetoken.googleapis.com/v1/token";
#endif
            return firebase.tokenUrl;
        }
    }
    private string firebaseKey {
        get {
#if UNITY_EDITOR || COMET_FIREBASE_TEST
            if(firebaseEmulator) return "test-key";
#endif
            return firebase.apiKey;
        }
    }
    // Firestore update-time preconditions must retain the exact RFC3339 string,
    // including subsecond precision; automatic DateTime parsing loses that value.
    private static JObject jsonObject(string body) {
        try { return Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(body ?? "{}",new Newtonsoft.Json.JsonSerializerSettings{DateParseHandling=Newtonsoft.Json.DateParseHandling.None}) ?? new JObject(); }
        catch { return new JObject(); }
    }
    private static tokenResponse firebaseTokens(JObject value) => new tokenResponse {
        access_token=(string)(value["idToken"]??value["id_token"]), refresh_token=(string)(value["refreshToken"]??value["refresh_token"]),
        expires_in=int.TryParse((string)(value["expiresIn"]??value["expires_in"]),out int seconds)?seconds:3600
    };
    private IEnumerator firebaseAuthRequest(string operation, JObject body, Action<response> done)
    {
        if(string.IsNullOrEmpty(firebaseKey)) { done(new response{status=503,body="{\"error\":{\"message\":\"CONFIGURATION_NOT_FOUND\"}}"}); yield break; }
        yield return request(firebaseAuthUrl+"/accounts:"+operation+"?key="+Uri.EscapeDataString(firebaseKey),"POST",body.ToString(),null,done);
    }
    private static string firebaseAuthCode(response result) => ((string)jsonObject(result.body)["error"]?["message"] ?? "").Split(new[]{" : "},StringSplitOptions.None)[0];
    private static string firebaseAuthMessage(response result, authOperation operation)
    {
        string code=firebaseAuthCode(result);
        if(result.status==0)return "Couldn't connect. Check your internet connection and try again. Your entries are kept.";
        if(result.status==429 || code=="TOO_MANY_ATTEMPTS_TRY_LATER" || code=="QUOTA_EXCEEDED")return "Too many attempts. Wait a few minutes, then try again. Email limits may take longer to reset.";
        if(code=="INVALID_LOGIN_CREDENTIALS" || code=="EMAIL_NOT_FOUND" || code=="INVALID_PASSWORD")return "Email or password is incorrect.";
        if(code=="USER_DISABLED")return "This account is unavailable. Contact the game administrator.";
        if(code=="INVALID_OOB_CODE" || code=="EXPIRED_OOB_CODE")return "This email link is invalid or expired. Request a new email and use its latest link.";
        if(code=="EMAIL_EXISTS")return "Unable to create an account with these details. Try logging in or use Forgot password.";
        if(code=="WEAK_PASSWORD" || code=="PASSWORD_DOES_NOT_MEET_REQUIREMENTS")return "This password was rejected. Check the password requirements and try again.";
        if(code=="INVALID_EMAIL")return "Enter a valid email address.";
        if(code=="CONFIGURATION_NOT_FOUND" || code=="OPERATION_NOT_ALLOWED")return "The account service needs configuration. Ask the game administrator to finish Firebase setup.";
        if(result.status>=500)return "The account service is temporarily unavailable. Your entries are kept; please try again shortly.";
        return "The request could not be completed. Your entries are kept. Try again; if it continues, contact the game administrator.";
    }
    private authResult firebaseFailure(authOperation operation,response result,string email="") => new authResult {operation=operation,email=email,message=firebaseAuthMessage(result,operation)};
    private IEnumerator authenticateFirebase(string email,string password,bool creating)
    {
        email=(email??"").Trim();var operation=creating?authOperation.signup:authOperation.login;
        if(!accountAuthPolicy.validEmail(email) || string.IsNullOrEmpty(password) || (creating && accountAuthPolicy.passwordError(password)!="")) {
            finishAuth(new authResult{operation=operation,email=email,message="Check the email and password fields."});yield break;
        }
        beginAuth(creating?"Creating your account...":"Logging in...");firebaseRecoveryCode=null;
        response result=null;
        yield return firebaseAuthRequest(creating?"signUp":"signInWithPassword",new JObject{{"email",email},{"password",password},{"returnSecureToken",true}},r=>result=r);
        password=null;
        if(!result.ok){finishAuth(firebaseFailure(operation,result,email));yield break;}
        var tokens=firebaseTokens(jsonObject(result.body));
        yield return firebaseAuthRequest("lookup",new JObject{{"idToken",tokens.access_token}},r=>result=r);
        var identity=result.ok?jsonObject(result.body)["users"]?[0]:null;
        if(identity==null){finishAuth(firebaseFailure(operation,result,email));yield break;}
        if((bool?)identity["emailVerified"]!=true) {
            pendingFirebaseTokens=tokens;pendingFirebaseEmail=email;
            if(creating) {
                yield return firebaseAuthRequest("sendOobCode",new JObject{{"requestType","VERIFY_EMAIL"},{"idToken",tokens.access_token}},r=>result=r);
                delayEmail(email);
                finishAuth(result.ok?new authResult{operation=operation,email=email,success=true,awaitingConfirmation=true,message="Check your email to verify your Comet Catcher account."}:
                    new authResult{operation=operation,email=email,code="email_not_confirmed",message="Your account was created, but the verification email was not confirmed. "+firebaseAuthMessage(result,authOperation.resend)});
            } else finishAuth(new authResult{operation=operation,email=email,code="email_not_confirmed",message="Verify your email before logging in. Open the confirmation link or request a new one."});
            yield break;
        }
        yield return acceptFirebaseSession(tokens,identity);
        bool accepted=signedIn && profileLoaded;
        finishAuth(new authResult{operation=operation,email=email,success=accepted,message=accepted?"You are logged in.":message});
        if(accepted && activeSessionId==null)enterPlay();
    }
    private IEnumerator acceptFirebaseSession(tokenResponse tokens,JToken identity)
    {
        profileLoaded=false;
        string id=(string)identity["localId"];
        if((bool?)identity["emailVerified"]!=true || string.IsNullOrEmpty(id)) {setMessage("A verified email account is required.");yield break;}
        if(ownerId!=null && ownerId!=id && activeSessionId!=null){setMessage("Log back into the original account to finish saving this run.");yield break;}
        if(userId!=id){profile=new profileRecord();progress=new progressRecord();researchConsentState.reset();}
        userId=id;accessToken=tokens.access_token;refreshToken=tokens.refresh_token;expiresAt=Time.realtimeSinceStartupAsDouble+tokens.expires_in;
        pendingFirebaseTokens=null;pendingFirebaseEmail=null;
        yield return loadProgress();
        if(!profileLoaded){accessToken=refreshToken=null;setMessage("Your account could not be loaded. Check your connection and log in again.");}
        else {restoreOutbox();if(hasPendingSave)yield return flush();}
    }
    private IEnumerator refreshFirebase()
    {
        if(refreshing){while(refreshing)yield return null;yield break;}
        if(string.IsNullOrEmpty(refreshToken))yield break;
        refreshing=true;response result=null;
        yield return request(firebaseTokenUrl+"?key="+Uri.EscapeDataString(firebaseKey),"POST","grant_type=refresh_token&refresh_token="+Uri.EscapeDataString(refreshToken),null,r=>result=r,"application/x-www-form-urlencoded");
        if(result.ok) {
            var tokens=firebaseTokens(jsonObject(result.body));accessToken=tokens.access_token;refreshToken=tokens.refresh_token;expiresAt=Time.realtimeSinceStartupAsDouble+tokens.expires_in;
        } else if(result.status==400 || result.status==401 || result.status==403) {accessToken=null;setMessage("Your session expired. Log in again to finish saving.");}
        else expiresAt=Time.realtimeSinceStartupAsDouble+120;
        refreshing=false;notify();
    }
    private IEnumerator sendFirebaseEmail(string email,bool recovering)
    {
        email=(email??"").Trim();var operation=recovering?authOperation.recover:authOperation.resend;
        if(!accountAuthPolicy.validEmail(email) || emailCooldown(email)>0){finishAuth(new authResult{operation=operation,email=email,message="Check the email address and wait for the resend countdown."});yield break;}
        if(!recovering && (pendingFirebaseTokens==null || !string.Equals(email,pendingFirebaseEmail,StringComparison.OrdinalIgnoreCase))) {
            finishAuth(new authResult{operation=operation,email=email,message="Return to Log in and enter this account's password to request a new verification email."});yield break;
        }
        beginAuth(recovering?"Requesting a reset email...":"Requesting another confirmation...");response result=null;
        if(!recovering) {
            yield return request(firebaseTokenUrl+"?key="+Uri.EscapeDataString(firebaseKey),"POST","grant_type=refresh_token&refresh_token="+Uri.EscapeDataString(pendingFirebaseTokens.refresh_token),null,r=>result=r,"application/x-www-form-urlencoded");
            if(!result.ok){finishAuth(firebaseFailure(operation,result,email));yield break;}
            pendingFirebaseTokens=firebaseTokens(jsonObject(result.body));
        }
        yield return firebaseAuthRequest("sendOobCode",recovering?new JObject{{"requestType","PASSWORD_RESET"},{"email",email}}:
            new JObject{{"requestType","VERIFY_EMAIL"},{"idToken",pendingFirebaseTokens.access_token}},r=>result=r);
        if(result.ok || result.status==429 || result.status==0)delayEmail(email);
        finishAuth(result.ok?new authResult{operation=operation,email=email,success=true,message=recovering?
            "If an account uses this address, a password-reset email will arrive shortly. Check your inbox and spam folder.":"Verification email requested. Use the latest link, then return here and log in."}:firebaseFailure(operation,result,email));
    }
    private void useFirebaseLink(string value,bool recovering)
    {
        if(!Uri.TryCreate((value??"").Trim(),UriKind.Absolute,out var link) || link.Scheme!="https" ||
            (link.Host!=firebase.authDomain && link.Host!=firebase.projectId+".web.app") || link.AbsolutePath!="/__/auth/action") {
            finishAuth(new authResult{operation=authOperation.verify,message="Paste the complete verification link from this game's email."});return;
        }
        var query=authParameters(link.Query);
        if(!query.TryGetValue("oobCode",out var code) || !query.TryGetValue("mode",out var mode) || mode!=(recovering?"resetPassword":"verifyEmail") ||
            !query.TryGetValue("apiKey",out var key) || key!=firebaseKey) {
            finishAuth(new authResult{operation=authOperation.verify,message="Use the latest email link for this account and this game."});return;
        }
        StartCoroutine(checkFirebaseLink(code,recovering));
    }
    private IEnumerator checkFirebaseLink(string code,bool recovering)
    {
        beginAuth("Checking your email link...");response result=null;
        yield return firebaseAuthRequest(recovering?"resetPassword":"update",new JObject{{"oobCode",code}},r=>result=r);
        if(!result.ok){finishAuth(firebaseFailure(authOperation.verify,result));yield break;}
        if(recovering)firebaseRecoveryCode=code;
        finishAuth(new authResult{operation=authOperation.verify,success=true,email=(string)jsonObject(result.body)["email"],recoveryReady=recovering,
            message=recovering?"Choose a new password.":"Email verified. Return to Log in to continue."});
    }
    private IEnumerator resetFirebasePassword(string password)
    {
        if(accountAuthPolicy.passwordError(password)!=""){finishAuth(new authResult{operation=authOperation.resetPassword,message=accountAuthPolicy.passwordError(password)});yield break;}
        beginAuth("Saving your new password...");response result=null;
        yield return firebaseAuthRequest("resetPassword",new JObject{{"oobCode",firebaseRecoveryCode},{"newPassword",password}},r=>result=r);password=null;
        if(!result.ok){finishAuth(firebaseFailure(authOperation.resetPassword,result));yield break;}
        firebaseRecoveryCode=null;finishAuth(new authResult{operation=authOperation.resetPassword,success=true,message="Password updated. Log in with your new password."});
    }
}
