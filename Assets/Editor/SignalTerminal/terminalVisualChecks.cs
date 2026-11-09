using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
public static class terminalVisualChecks
{
    public static string output="Temp/SignalTerminalVerification/";
    public static bool complete;
    public static void size(int width,int height)
    {
        var a=typeof(EditorWindow).Assembly;var type=a.GetType("UnityEditor.GameViewSizes");
        var sizes=typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var group=type.GetProperty("currentGroup",flags).GetValue(sizes);
        string label="terminalCheck"+width+"x"+height;
        var texts=(string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null);
        int index=Array.FindIndex(texts,t=>t.Contains(label));
        if(index<0){var s=a.GetType("UnityEditor.GameViewSize");var kind=a.GetType("UnityEditor.GameViewSizeType");var value=Activator.CreateInstance(s,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{Enum.ToObject(kind,1),width,height,label},null);group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{value});texts=(string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null);index=Array.FindIndex(texts,t=>t.Contains(label));}
        var gt=a.GetType("UnityEditor.GameView");var w=EditorWindow.GetWindow(gt);gt.GetProperty("selectedSizeIndex",flags).SetValue(w,index);w.Focus();
    }
    public static void login(bool finished=false)
    {complete=finished;cloudAccount.testTransport=transport;cloudAccount.testApiTransport=transport;cloudAccount.instance.logIn("preview@example.invalid","editor-preview-only");Application.runInBackground=true;}
    private static IEnumerator transport(string path,string method,string json,string bearer,Action<long,string> done)
    {
        yield return null;
        if(path.Contains("/token") || path.Contains("accounts:signInWithPassword"))done(200,"{\"idToken\":\"preview\",\"refreshToken\":\"preview\",\"expiresIn\":3600}");
        else if(path.Contains("accounts:lookup"))done(200,"{\"users\":[{\"localId\":\"visual-preview\",\"emailVerified\":true}]}");
        else if(path.EndsWith("/profile")||path.EndsWith("/tutorial"))done(200,"{\"consentVersion\":\"2026-10-25-v1\",\"consentStatus\":\"declined\",\"researchCollectionEnabled\":false,\"nickname\":\"Pilot_Orion\",\"tutorialVersion\":"+(complete?1:0)+"}");
        else if(path.EndsWith("/progress"))done(200,"{\"bestScore\":12450,\"highestLevel\":2,\"sessionsPlayed\":18,\"sessionsCompleted\":12}");
        else done(200,"{}");
    }
    public static void capture(string name){System.IO.Directory.CreateDirectory(output);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(output+name+".png");}

    public static void consent(){cloudAccount.instance.StartCoroutine(consentScreens());}
    private static IEnumerator consentScreens()
    {
        SceneManager.LoadScene("researchConsent");yield return null;yield return null;
        var account=UnityEngine.Object.FindFirstObjectByType<accountUi>();
        var ui=UnityEngine.Object.FindFirstObjectByType<researchConsentUi>();
        size(1280,720);ui.showOpening();yield return screen("consent-opening-1280");
        ui.showAdult();yield return screen("consent-adult-top-1280");ui.adult.scroll.verticalNormalizedPosition=0;yield return screen("consent-adult-fields-1280");
        ui.showChild();yield return screen("consent-child-top-1280");ui.child.scroll.verticalNormalizedPosition=0;yield return screen("consent-child-fields-1280");
        ui.child.agreement.isOn=true;ui.continueChild();yield return screen("consent-parent-top-1280");ui.parent.scroll.verticalNormalizedPosition=0;yield return screen("consent-parent-fields-1280");
        size(800,600);ui.showAdult();yield return screen("consent-adult-top-800");ui.adult.scroll.verticalNormalizedPosition=0;yield return screen("consent-adult-fields-800");
        size(1280,720);SceneManager.LoadScene(cloudAccount.accountScene);yield return null;account.gameObject.SetActive(true);
    }
    private static IEnumerator screen(string name){yield return new WaitForSecondsRealtime(.35f);capture(name);yield return new WaitForSecondsRealtime(.25f);}
    public static void openingSizes(){cloudAccount.instance.StartCoroutine(responsiveScreens());}
    private static IEnumerator responsiveScreens()
    {
        foreach(var dimensions in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(800,600),new Vector2Int(1920,800)})
        {size(dimensions.x,dimensions.y);yield return screen("after-opening-"+dimensions.x+"x"+dimensions.y);}
        size(1280,720);
    }

    public static string navigationResult="not run";
    public static void navigation(){navigationResult="running";cloudAccount.instance.StartCoroutine(consentNavigation());}
    private static IEnumerator consentNavigation()
    {
        SceneManager.LoadScene("researchConsent");yield return null;yield return null;
        var account=UnityEngine.Object.FindFirstObjectByType<accountUi>();
        var ui=UnityEngine.Object.FindFirstObjectByType<researchConsentUi>();ui.showAdult();
        if(ui.adult.agreement.isOn || ui.child.agreement.isOn || ui.parent.agreement.isOn)throw new Exception("Consent began checked");
        var keyboard=UnityEngine.InputSystem.Keyboard.current;
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Tab));yield return null;yield return null;
        var selected=UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        if(selected==null || selected.GetComponent<TMP_InputField>()==null)throw new Exception("Consent Tab did not reach the name field");
        yield return new WaitForSecondsRealtime(.2f);
        if(ui.adult.scroll.verticalNormalizedPosition>=.99f)throw new Exception("Focus did not scroll the long consent document");
        capture("consent-keyboard-focus-1280");
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Tab));yield return null;yield return null;
        if(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject!=ui.adult.agreement.gameObject)throw new Exception("Consent Tab order is incorrect");
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return null;
        // This scene retains StandaloneInputModule. Input System injection cannot drive its legacy Submit axis.
        // Exercise the submit binding here; native Enter is separately verified in the Editor.
        UnityEngine.EventSystems.ExecuteEvents.Execute(ui.adult.agreement.gameObject,new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current),UnityEngine.EventSystems.ExecuteEvents.submitHandler);yield return null;yield return null;
        if(!ui.adult.agreement.isOn || !ui.adult.continueButton.interactable)throw new Exception("Consent keyboard agreement binding failed");
        ui.adult.agreement.isOn=false;
        if(ui.adult.continueButton.interactable)throw new Exception("Consent gate failed after unchecking");
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
        navigationResult="PASS: all forms start unchecked; Tab reaches the first field; focus scrolls long text; Tab order reaches agreement; Submit toggles agreement and enables Continue; unchecking disables Continue";
        SceneManager.LoadScene(cloudAccount.accountScene);yield return null;account.gameObject.SetActive(true);
        System.IO.File.WriteAllText(output+"consent-navigation-checks.txt",navigationResult);
    }
}
