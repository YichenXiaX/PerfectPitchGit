using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using System.Linq;

// Styles the existing consent hierarchy. Copy, field bindings and callbacks stay intact.
public static class consentTerminalTheme
{
    [MenuItem("Tools/Research Consent/Apply Signal Terminal")]
    public static void apply()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play mode first.");
        var ui=Object.FindFirstObjectByType<researchConsentUi>(FindObjectsInactive.Include);
        if(ui==null || ui.gameObject.scene.path!="Assets/Scenes/researchConsent.unity")throw new System.InvalidOperationException("Open researchConsent first.");
        var before=ui.GetComponentsInChildren<TMP_Text>(true).ToDictionary(t=>t,t=>t.text);
        string bindings=EditorJsonUtility.ToJson(ui);
        var headings=new System.Collections.Generic.HashSet<string>();
        foreach(var name in new[]{"adultConsent","childAssent","parentConsent"})
            foreach(var line in AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/UI/ResearchConsent/"+name+".txt").text.Split('\n'))
                if(line.StartsWith("### "))headings.Add(line.Substring(4).TrimEnd('\r'));
        signalTerminal.canvas(ui.GetComponent<Canvas>());
        ui.transform.Find("background").GetComponent<UnityEngine.UI.Image>().color=new Color(7/255f,14/255f,26/255f,.12f);
        ui.transform.Find("brandBar").gameObject.SetActive(false);
        ui.transform.Find("terminalMetadata").gameObject.SetActive(false);
        foreach(var panel in new[]{ui.openingPanel,ui.adult.panel,ui.child.panel,ui.parent.panel})
        {
            var rect=(RectTransform)panel.transform;
            var oldWidth=panel.GetComponent<terminalPanelWidth>();if(oldWidth!=null)Object.DestroyImmediate(oldWidth);
            signalTerminal.image(rect,signalTerminal.panelColor,true);
            foreach(var text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                signalTerminal.type(text,false,18);text.margin=Vector4.zero;text.alignment=TextAlignmentOptions.TopLeft;
                bool fieldLabel=text.transform.parent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>()!=null &&
                    text.transform.GetSiblingIndex()+1<text.transform.parent.childCount &&
                    text.transform.parent.GetChild(text.transform.GetSiblingIndex()+1).GetComponent<TMP_InputField>()!=null;
                if(headings.Contains(text.text) || fieldLabel || text.text=="Optional follow-up:" || text.text=="Age choices:")
                {signalTerminal.type(text,true,15);text.color=headings.Contains(text.text)?signalTerminal.accent:signalTerminal.secondary;}
            }
            var header=(RectTransform)panel.transform.Find("header");
            var title=header.GetChild(0).GetComponent<TMP_Text>();signalTerminal.type(title,true,25);title.characterSpacing=-1;
            var metadata=header.Find("consentMetadata")?.GetComponent<TMP_Text>();
            if(metadata==null)metadata=signalTerminal.text(header,"consentMetadata","COMET CATCHER / RESEARCH",11,18,true);
            signalTerminal.type(metadata,true,11);metadata.color=signalTerminal.accent;
            var scroll=panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);scroll.scrollSensitivity=28;
            scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var layout=scroll.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding=new RectOffset(20,20,12,12);layout.spacing=12;
            // The old footer lived in the scroll content; its trailing spacer is no longer needed.
            var last=scroll.content.GetChild(scroll.content.childCount-1);
            if(last.name=="spacing")last.gameObject.SetActive(false);
            scroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            foreach(var input in panel.GetComponentsInChildren<TMP_InputField>(true))
            {
                var r=(RectTransform)input.transform;signalTerminal.height(r,40);
                input.targetGraphic=signalTerminal.image(r,new Color32(13,31,46,255),true);
                input.textViewport.offsetMin=new Vector2(12,4);input.textViewport.offsetMax=new Vector2(-12,-4);
                signalTerminal.type(input.textComponent,false,17);input.textComponent.alignment=TextAlignmentOptions.Left;
                input.textComponent.textWrappingMode=TextWrappingModes.NoWrap;input.textComponent.margin=Vector4.zero;
                if(input.placeholder is TMP_Text placeholder){signalTerminal.type(placeholder,false,16);placeholder.alignment=TextAlignmentOptions.Left;placeholder.textWrappingMode=TextWrappingModes.NoWrap;placeholder.color=signalTerminal.secondary;}
                input.caretColor=signalTerminal.accent;input.selectionColor=new Color(.3f,.65f,.8f,.4f);
                var colors=input.colors;colors.selectedColor=new Color32(169,221,241,255);colors.highlightedColor=Color.white;input.colors=colors;
                if(r.Find("terminalRule")==null)signalTerminal.frame(r,signalTerminal.rule).box=true;
            }
            foreach(var toggle in panel.GetComponentsInChildren<UnityEngine.UI.Toggle>(true))
            {
                var label=toggle.GetComponentInChildren<TMP_Text>(true);signalTerminal.type(label,false,18);
                label.alignment=TextAlignmentOptions.TopLeft;
                toggle.graphic.color=signalTerminal.accent;toggle.targetGraphic.color=signalTerminal.ink;
                if(toggle!=ui.adult.agreement && toggle!=ui.child.agreement && toggle!=ui.parent.agreement && toggle.graphic is UnityEngine.UI.Image dot)
                {dot.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");dot.type=UnityEngine.UI.Image.Type.Simple;}
            }
            var actions=panel.transform.Find("actions");
            foreach(var button in actions.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                bool primary=button==ui.adult.continueButton || button==ui.child.continueButton || button==ui.parent.continueButton;
                bool back=button.GetComponentInChildren<TMP_Text>(true).text=="Back";
                signalTerminal.styleButton(button,primary?signalTerminal.actionStyle.primary:back?signalTerminal.actionStyle.quiet:signalTerminal.actionStyle.secondary);
                var label=button.GetComponentInChildren<TMP_Text>(true);label.textWrappingMode=TextWrappingModes.NoWrap;
                var state=button.GetComponent<consentButtonLabel>();
                if(state!=null){var serialized=new SerializedObject(state);serialized.FindProperty("enabledColor").colorValue=primary?signalTerminal.background:signalTerminal.ink;serialized.FindProperty("disabledColor").colorValue=signalTerminal.secondary;serialized.ApplyModifiedPropertiesWithoutUndo();}
            }
        }
        signalTerminal.type(ui.statusText,false,15);ui.statusText.color=signalTerminal.accent;ui.statusText.alignment=TextAlignmentOptions.Left;
        var adaptive=ui.GetComponent<consentTerminalLayout>()??ui.gameObject.AddComponent<consentTerminalLayout>();adaptive.reflow();
        foreach(var pair in before)if(pair.Key.text!=pair.Value)throw new System.Exception("Consent copy changed: "+pair.Key.name);
        if(EditorJsonUtility.ToJson(ui)!=bindings)throw new System.Exception("Consent field bindings changed.");
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);EditorSceneManager.SaveScene(ui.gameObject.scene);
        Debug.Log("Consent presentation saved; original text and form bindings preserved.");
    }
}
