using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using TMPro;

public static class tutorialPolishChecks
{
    public static string output="Temp/TutorialPolish/";
    public static string result="not run";
    public static void captureScreens(){cloudAccount.instance.StartCoroutine(screens());}
    private static UnityEngine.UI.Button action(string name)=>Array.Find(tutorialDirector.instance.GetComponentsInChildren<UnityEngine.UI.Button>(true),b=>b.name==name);
    private static IEnumerator screens()
    {
        terminalVisualChecks.login(false);
        while(cloudAccount.instance.busy)yield return null;
        cloudAccount.instance.enterPlay();
        while(tutorialDirector.instance==null)yield return null;
        yield return null;yield return null;
        action("continue").onClick.Invoke();action("continue").onClick.Invoke();
        foreach(var dimensions in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(800,600)})
        {
            terminalVisualChecks.size(dimensions.x,dimensions.y);yield return new WaitForSecondsRealtime(.5f);
            terminalVisualChecks.capture("piano-"+dimensions.x+"x"+dimensions.y);yield return new WaitForSecondsRealtime(.3f);
        }
        var lessonScroll=tutorialDirector.instance.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
        lessonScroll.verticalNormalizedPosition=0;yield return new WaitForSecondsRealtime(.3f);terminalVisualChecks.capture("piano-800x600-scrolled");yield return new WaitForSecondsRealtime(.3f);
        terminalVisualChecks.size(1280,720);yield return new WaitForSecondsRealtime(.5f);
        action("continue").onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);terminalVisualChecks.capture("sounding-pitch-1280x720");
        while(tutorialDirector.instance.cuePlaying)yield return null;
        var ui=UnityEngine.Object.FindFirstObjectByType<settingsUi>();ui.show();ui.customToggle.isOn=true;
        foreach(var dimensions in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(800,600)})
        {
            terminalVisualChecks.size(dimensions.x,dimensions.y);yield return new WaitForSecondsRealtime(.5f);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);yield return null;
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(ui.octave0.gameObject);yield return new WaitForSecondsRealtime(.4f);
            terminalVisualChecks.capture("five-octaves-"+dimensions.x+"x"+dimensions.y);yield return new WaitForSecondsRealtime(.3f);
        }
        // Do not save the visual preview's Custom selection into the persistent settings.
        ui.settingPanel.SetActive(false);Time.timeScale=1;ui.audioSource.UnPause();
        result="PASS: responsive piano, sounding-pitch highlight and five-octave captures complete";
    }
    public static void staticChecks()
    {
        if(string.Join(",",gameSettings.normalizeOctaves(new[]{-5,-2,0,2,5}))!="-2,0,2")throw new Exception("Legacy valid choices lost");
        if(string.Join(",",gameSettings.normalizeOctaves(new[]{-5,5}))!="0")throw new Exception("Empty octave default");
        if(string.Join(",",gameSettings.normalizeOctaves(new[]{2,-1,2,0}))!="-1,0,2")throw new Exception("Octave deduplication");
        foreach(var font in new[]{signalTerminal.bodyFont,signalTerminal.buttonFont})
        {foreach(char c in "C♯4 D♭4 ← → −2 [ ] \\")if(!font.HasCharacter(c,true))throw new Exception("Missing required glyph in "+font.name+": "+c);}
        if(signalTerminal.monoFont!=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/ScienceGothic-VariableFont_CTRS,slnt,wdth,wght SDF.asset"))throw new Exception("Heading font reference");
        var ui=UnityEngine.Object.FindFirstObjectByType<settingsUi>(FindObjectsInactive.Include);
        if(ui!=null && ui.octave0.transform.parent.GetComponentsInChildren<UnityEngine.UI.Toggle>(true).Length!=5)throw new Exception("Obsolete octave controls remain");
        result="PASS: legacy octave filtering/default/deduplication; font references and required glyphs; five serialized octave toggles";
    }
    public static void observeArrival(GameObject comet)
    {
        string mode=tutorialDirector.instance.state==tutorialDirector.lesson.impactArrival?"impact":"recovery";
        string layout=controlLayout.leftHanded?"left":"right";
        cloudAccount.instance.StartCoroutine(motion(comet,mode+"-"+layout));
    }
    private static IEnumerator motion(GameObject comet,string name)
    {
        string folder=output+"motion-"+name+"/";Directory.CreateDirectory(folder);
        var rows=new List<string>{"seconds,state,cometY,minY,maxY,health,auraText"};
        float start=Time.realtimeSinceStartup,ended=-1;int frame=0;
        while(Time.realtimeSinceStartup-start<15)
        {
            var director=tutorialDirector.instance;if(director==null)break;
            if(comet==null && ended<0)ended=Time.realtimeSinceStartup;
            if(ended>=0 && Time.realtimeSinceStartup-ended>2.6f)break;
            var bounds=comet==null?new Bounds():comet.GetComponentInChildren<SpriteRenderer>().bounds;
            var hud=UnityEngine.Object.FindFirstObjectByType<auraHud>();
            rows.Add(string.Join(",",(Time.realtimeSinceStartup-start).ToString("F3"),director.state,comet==null?"destroyed":comet.transform.position.y.ToString("F3"),bounds.min.y.ToString("F3"),bounds.max.y.ToString("F3"),gameManager.instance.currentHealth.ToString("F2"),hud.auraText.text));
            ScreenCapture.CaptureScreenshot(folder+(frame++).ToString("D3")+".png");
            yield return new WaitForSecondsRealtime(.15f);
        }
        File.WriteAllLines(folder+"timing.csv",rows);
    }
}

public class tutorialHitObserver : MonoBehaviour
{
    public static int hits;
    private void OnTriggerEnter2D(Collider2D other){if(other.GetComponent<bulletBehavior>()!=null)hits++;}
}
