using System;
using System.Collections.Generic;
using UnityEngine;

// Authentication entry points shared by the existing account screens.
// Firebase owns passwords, verification and reset emails; tokens stay in memory.
public partial class cloudAccount
{
    public enum authOperation { login, signup, verify, resend, recover, resetPassword }
    public class authResult
    {
        public authOperation operation;
        public bool success, awaitingConfirmation, recoveryReady;
        public string email, code, message;
    }
    public event Action<authResult> authenticationFinished;
    public bool authenticating { get; private set; }
    public bool canResetPassword => firebaseRecoveryCode!=null;
    public void cancelPasswordReset() { if(!busy)firebaseRecoveryCode=null; }
    private bool profileLoaded;
    private readonly Dictionary<string,double> nextEmailSend=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
    public int emailCooldown(string email)
    {return nextEmailSend.TryGetValue((email??"").Trim(),out var until)?Math.Max(0,(int)Math.Ceiling(until-Time.realtimeSinceStartupAsDouble)):0;}
    private void delayEmail(string email)
    {nextEmailSend[(email??"").Trim()]=Time.realtimeSinceStartupAsDouble+accountAuthPolicy.resendCooldownSeconds;}
    private void beginAuth(string text) { busy=authenticating=true;setMessage(text); }
    private void finishAuth(authResult result)
    {busy=authenticating=false;setMessage(result.message);authenticationFinished?.Invoke(result);}
    public void logIn(string email,string password) {if(!busy)StartCoroutine(authenticateFirebase(email,password,false));}
    public void signUp(string email,string password) {if(!busy)StartCoroutine(authenticateFirebase(email,password,true));}
    public void resendEmail(string email) {if(!busy)StartCoroutine(sendFirebaseEmail(email,false));}
    public void recoverPassword(string email) {if(!busy)StartCoroutine(sendFirebaseEmail(email,true));}
    public void useEmailLink(string value,bool recovering=false) {if(!busy)useFirebaseLink(value,recovering);}
    public void resetPassword(string password) {if(!busy && canResetPassword)StartCoroutine(resetFirebasePassword(password));}
    private static Dictionary<string,string> authParameters(string query)
    {
        var values=new Dictionary<string,string>();
        foreach(string part in query.TrimStart('?','#').Split('&')){var pair=part.Split(new[]{'='},2);if(pair.Length==2)values[Uri.UnescapeDataString(pair[0])]=Uri.UnescapeDataString(pair[1]);}
        return values;
    }
}
