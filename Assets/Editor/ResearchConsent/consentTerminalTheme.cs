using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

// Edits only the consent scene. Original strings, fields and persistent callbacks survive unchanged.
public static class consentTerminalTheme
{
    [MenuItem("Tools/Research Consent/Apply Signal Terminal")]
    public static void apply()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play mode first.");
        var ui=Object.FindFirstObjectByType<researchConsentUi>(FindObjectsInactive.Include);
        if(ui==null || ui.gameObject.scene.path!="Assets/Scenes/researchConsent.unity")throw new System.InvalidOperationException("Open researchConsent first.");
        var headings=new System.Collections.Generic.HashSet<string>();
        foreach(var name in new[]{"adultConsent","childAssent","parentConsent"})foreach(var line in AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/UI/ResearchConsent/"+name+".txt").text.Split('\n'))if(line.StartsWith("### "))headings.Add(line.Substring(4).TrimEnd('\r'));
        var before=ui.GetComponentsInChildren<TMP_Text>(true).ToDictionary(t=>t.GetInstanceID(),t=>t.text);
        if(ui.GetComponent<terminalKeyboardNavigation>()==null)ui.gameObject.AddComponent<terminalKeyboardNavigation>();
        signalTerminal.canvas(ui.GetComponent<Canvas>());
        ui.transform.Find("background").GetComponent<Image>().color=signalTerminal.background;
        foreach(var panel in new[]{ui.openingPanel,ui.adult.panel,ui.child.panel,ui.parent.panel})
        {
            bool opening=panel==ui.openingPanel;
            var r=(RectTransform)panel.transform;r.anchorMin=new Vector2(.5f,0);r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(660,-110);r.anchoredPosition=new Vector2(0,-8);
            if(opening){r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(660,320);}
            var width=r.GetComponent<terminalPanelWidth>()??r.gameObject.AddComponent<terminalPanelWidth>();width.maximumWidth=660;
            signalTerminal.image(r,signalTerminal.panelColor,true);
            foreach(var t in panel.GetComponentsInChildren<TMP_Text>(true)){signalTerminal.type(t,false,18);t.alignment=TextAlignmentOptions.TopLeft;if(headings.Contains(t.text)){signalTerminal.type(t,true,16);t.color=signalTerminal.accent;}}
            foreach(var image in panel.GetComponentsInChildren<Image>(true))if(image.name=="outline" || image.name=="arrow" || image.name=="divider")image.gameObject.SetActive(false);
            var scroll=panel.GetComponentInChildren<ScrollRect>(true);scroll.scrollSensitivity=30;
            if(scroll.GetComponent<terminalScrollFocus>()==null)scroll.gameObject.AddComponent<terminalScrollFocus>();
            var layout=scroll.content.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(20,20,14,24);layout.spacing=14;
            var header=(RectTransform)panel.transform.Find("header");header.gameObject.SetActive(true);terminalPanel.place(header,0,0,0,78);signalTerminal.image(header,Color.clear);
            if(opening)
            {
                var title=scroll.content.Find("welcomeTitle");if(title!=null){title.SetParent(header,false);}
                var doc=scroll.content.Find("document");if(doc!=null)doc.gameObject.SetActive(false);
            }
            var heading=header.GetComponentInChildren<TMP_Text>(true);signalTerminal.type(heading,true,24);terminalPanel.place(heading.rectTransform,20,20,-20,52);
            addRule(r,"terminalTop",0,signalTerminal.accent,false);addRule(r,"terminalHeader",78,signalTerminal.rule,true);
            var view=(RectTransform)scroll.transform;signalTerminal.fill(view);view.offsetMin=new Vector2(0,opening?78:132);view.offsetMax=new Vector2(-10,-80);
            scroll.viewport.offsetMin=Vector2.zero;scroll.viewport.offsetMax=new Vector2(-14,0);
            var bar=(RectTransform)scroll.verticalScrollbar.transform;bar.sizeDelta=new Vector2(4,0);signalTerminal.image(bar,Color.clear);signalTerminal.image(scroll.verticalScrollbar.handleRect,signalTerminal.rule,true);
            foreach(var input in panel.GetComponentsInChildren<TMP_InputField>(true))
            {signalTerminal.image((RectTransform)input.transform,new Color32(17,37,52,255),true);signalTerminal.height((RectTransform)input.transform,44);input.caretColor=signalTerminal.accent;input.selectionColor=new Color(.3f,.65f,.8f,.4f);if(input.GetComponent<terminalFocus>()==null)input.gameObject.AddComponent<terminalFocus>();}
            var actions=opening?scroll.content.Find("actions") as RectTransform:null;
            if(opening && actions==null)actions=panel.transform.Find("actions") as RectTransform;
            if(opening)footer(actions,r,null);
        }
        foreach(var form in new[]{ui.adult,ui.child,ui.parent})
        {
            foreach(var toggle in new[]{form.agreement,form.followUpYes,form.followUpNo})
            {var hit=toggle.GetComponent<Image>();if(hit!=null)hit.color=Color.clear;toggle.graphic.color=signalTerminal.accent;toggle.targetGraphic.color=signalTerminal.ink;if(toggle.GetComponent<terminalFocus>()==null)toggle.gameObject.AddComponent<terminalFocus>();}
            footer((RectTransform)form.continueButton.transform.parent,(RectTransform)form.panel.transform,form.continueButton);
        }
        var brand=(RectTransform)ui.transform.Find("brandBar");brand.anchorMin=brand.anchorMax=new Vector2(.5f,1);brand.pivot=new Vector2(.5f,1);brand.sizeDelta=new Vector2(660,40);brand.anchoredPosition=new Vector2(0,-12);
        var brandWidth=brand.GetComponent<terminalPanelWidth>()??brand.gameObject.AddComponent<terminalPanelWidth>();brandWidth.maximumWidth=660;
        foreach(var t in brand.GetComponentsInChildren<TMP_Text>(true))signalTerminal.type(t,true,16);
        ui.transform.Find("brandBar/orbit").GetComponent<Image>().color=signalTerminal.accent;
        var meta=ui.transform.Find("terminalMetadata").GetComponent<TMP_Text>();signalTerminal.type(meta,true,11);meta.color=signalTerminal.secondary;var mr=meta.rectTransform;mr.anchorMin=mr.anchorMax=new Vector2(.5f,0);mr.pivot=new Vector2(.5f,0);mr.sizeDelta=new Vector2(660,18);mr.anchoredPosition=new Vector2(0,5);
        foreach(var t in ui.GetComponentsInChildren<TMP_Text>(true))if(before.TryGetValue(t.GetInstanceID(),out var copy) && copy!=t.text)throw new System.Exception("Consent copy changed: "+t.name);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);EditorSceneManager.SaveScene(ui.gameObject.scene);
        Debug.Log("Signal Terminal reconstructed. All "+before.Count+" original text strings preserved verbatim.");
    }
    private static void footer(RectTransform actions,RectTransform panel,Button primary)
    {
        actions.SetParent(panel,false);
        foreach(var layout in actions.GetComponents<LayoutGroup>())Object.DestroyImmediate(layout);
        var adaptive=actions.GetComponent<terminalActionGrid>();if(adaptive!=null)Object.DestroyImmediate(adaptive);
        actions.anchorMin=Vector2.zero;actions.anchorMax=new Vector2(1,0);actions.pivot=new Vector2(.5f,0);actions.offsetMin=new Vector2(20,18);actions.offsetMax=new Vector2(-20,primary==null?58:118);
        var buttons=actions.GetComponentsInChildren<Button>(true);
        for(int i=0;i<buttons.Length;i++)
        {
            var b=buttons[i];bool back=primary!=null && i==0;signalTerminal.styleButton(b,b==primary?signalTerminal.actionStyle.primary:back?signalTerminal.actionStyle.quiet:signalTerminal.actionStyle.secondary);
            var rect=(RectTransform)b.transform;rect.anchorMin=new Vector2(back?0:(i-(primary==null?0:1))*.5f,0);rect.anchorMax=new Vector2(back?0:(i-(primary==null?0:1)+1)*.5f,0);rect.pivot=new Vector2(.5f,0);rect.offsetMin=new Vector2(0,back?56:0);rect.offsetMax=new Vector2(back?100:-10,back?96:primary==null?40:48);
            var t=b.GetComponentInChildren<TMP_Text>();signalTerminal.type(t,false,15);t.color=b==primary?signalTerminal.background:signalTerminal.ink;t.alignment=TextAlignmentOptions.Center;signalTerminal.fill(t.rectTransform,6);
        }
        var rule=panel.Find("terminalFooter") as RectTransform;if(rule==null)rule=signalTerminal.node(panel,"terminalFooter");rule.anchorMin=Vector2.zero;rule.anchorMax=new Vector2(1,0);rule.pivot=new Vector2(.5f,0);rule.sizeDelta=new Vector2(0,1);rule.anchoredPosition=new Vector2(0,primary==null?74:130);
        if(rule.GetComponentInChildren<terminalFrame>()==null)signalTerminal.frame(rule,signalTerminal.rule,true);
    }
    private static void addRule(RectTransform parent,string name,float down,Color color,bool dashed)
    {var r=parent.Find(name) as RectTransform;if(r==null)r=signalTerminal.node(parent,name);terminalPanel.place(r,0,down,0,1);if(r.GetComponentInChildren<terminalFrame>()==null)signalTerminal.frame(r,color,dashed);}
}
