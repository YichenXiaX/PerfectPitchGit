using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine.Events;
using TMPro;

// Editor-only construction utility. Text files are the source of the approved copy.
public static class researchConsentSceneBuilder
{
    private static TMP_FontAsset font;
    private static TMP_InputField inputTemplate;
    private static Color panelColor, headerColor, fieldColor, accentColor;
    private static readonly string scenePath = "Assets/Scenes/researchConsent.unity";
    private static readonly string textFolder = "Assets/UI/ResearchConsent/";

    public static string create()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before creating the consent scene.");
        if (System.IO.File.Exists(scenePath)) throw new InvalidOperationException("Consent scene already exists; edit it in place.");
        var source = UnityEngine.Object.FindFirstObjectByType<settingsUi>(FindObjectsInactive.Include);
        if (source == null) throw new InvalidOperationException("Open SampleScene first to read the existing Settings style.");
        font = source.playerNameInput.textComponent.font;
        inputTemplate = source.playerNameInput;
        panelColor = source.settingPanel.GetComponent<UnityEngine.UI.Image>().color;
        headerColor = source.settingPanel.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(i=>i.name=="Header").color;
        fieldColor = inputTemplate.GetComponent<UnityEngine.UI.Image>().color;
        accentColor = source.presetToggle.graphic.color;
        var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        var camera = new GameObject("consentCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = headerColor;
        camera.orthographic = true;
        var canvas = rect("consentCanvas", null).gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720);
        scaler.matchWidthOrHeight = 0f;
        canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var background = rect("background",canvas.transform);
        stretch(background,0,0,0,0);
        var backdrop = image(background,Color.white);
        backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/startScreenBackground.png");
        backdrop.raycastTarget = false;
        var ui = canvas.gameObject.AddComponent<researchConsentUi>();
        var events = new GameObject("consentEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
        var existing = UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.BaseInputModule>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault();
        if (existing != null) UnityEditorInternal.ComponentUtility.CopyComponent(existing);
        if (existing != null) UnityEditorInternal.ComponentUtility.PasteComponentAsNew(events);
        else events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        var opening = page(canvas.transform, "beforeYouPlay", "Before You Play", out var openingScroll);
        ui.openingPanel = openingScroll.transform.parent.gameObject;
        label(opening,"We’re conducting a research study on how people learn musical sounds while playing this game. To continue, please tell us whether you are under 18 or 18 or older so we can show you the appropriate consent form.",20);
        label(opening,"Age choices:",20);
        space(opening,28);
        var choices = row(opening,72);
        button(choices,"Under 18",ui.showChild,accentColor,260);
        button(choices,"18 or older",ui.showAdult,accentColor,260);

        ui.child = makeForm(canvas.transform,"childAssent","Child Assent to Participate","childAssent",
            new[]{"Child's Name (print):"},new[]{"FIRST AND LAST NAME"},
            "I have read (or had read to me) the information above and I agree to be in this study.",
            "Yes, I give permission to be contacted for an optional follow-up survey.",
            "No, I do not want to be contacted for follow-up.",
            "If “Yes,” email or phone to contact:","Email or phone (optional)",
            "I Do Not Want to Participate","I Agree, Continue",ui.showOpening,ui.decline,ui.continueChild);
        ui.parent = makeForm(canvas.transform,"parentConsent","Parent / Guardian Consent","parentConsent",
            new[]{"Child’s Name (printed):","Parent/Guardian’s Name (printed):"},
            new[]{"Child's first and last name","Parent/guardian first and last name"},
            "I am the parent/guardian of this child, my child is at least 12 years old, and I give permission for my child to participate in this study as described above.",
            "I give permission for my child to be contacted about an optional follow-up survey.",
            "I do not give permission for follow-up contact.",
            "If “Yes,” preferred email or phone:","Email or phone (optional)",
            "I Do Not Consent","I Consent, Continue",ui.showChild,ui.decline,ui.continueParent);
        ui.adult = makeForm(canvas.transform,"adultConsent","Adult Consent to Participate","adultConsent",
            new[]{"Participant Name (printed):"},new[]{"FIRST AND LAST NAME"},
            "I have read and understand the information above and I agree to participate in this study.",
            "Yes, I agree to be contacted for a follow-up survey.",
            "No, I do not wish to be contacted for follow-up.",
            "If “Yes,” preferred email:","email@example.com",
            "I Do Not Consent","I Consent, Continue",ui.showOpening,ui.decline,ui.continueAdult);
        ui.child.panel.SetActive(false); ui.parent.panel.SetActive(false); ui.adult.panel.SetActive(false);
        researchConsentDarkTheme.apply(ui);
        EditorSceneManager.SaveScene(scene,scenePath);
        var scenes = EditorBuildSettings.scenes.Where(s=>s.path!=scenePath).ToList();
        scenes.Insert(0,new EditorBuildSettingsScene(scenePath,true));
        EditorBuildSettings.scenes = scenes.ToArray();
        EditorSceneManager.CloseScene(scene,true);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
        return scenePath;
    }

    private static researchConsentUi.form makeForm(Transform canvas,string name,string title,string body,
        string[] nameLabels,string[] placeholders,string agreement,string yes,string no,string contact,string contactPlaceholder,
        string decline,string proceed,UnityAction backAction,UnityAction declineAction,UnityAction continueAction)
    {
        var content = page(canvas,name,title,out var scroll);
        foreach (var paragraph in AssetDatabase.LoadAssetAtPath<TextAsset>(textFolder+body+".txt").text.Trim().Split(new[]{"\r\n\r\n","\n\n"},StringSplitOptions.RemoveEmptyEntries))
        {
            var value = paragraph.Replace("\r","");
            bool heading = value.StartsWith("### ");
            if (heading) value=value.Substring(4);
            value=string.Join("\n",value.Split('\n').Select(line=>line.StartsWith("- ")?"• "+line.Substring(2):line));
            label(content,value,heading?22:19,heading);
        }
        space(content,24);
        for(int i=0;i<nameLabels.Length;i++) { label(content,nameLabels[i],19); input(content,placeholders[i]); }
        space(content,10);
        var agree = toggle(content,agreement,null);
        label(content,"Optional follow-up:",20,true);
        var group = content.gameObject.AddComponent<UnityEngine.UI.ToggleGroup>();
        group.allowSwitchOff = true;
        var yesToggle=toggle(content,yes,group);
        var noToggle=toggle(content,no,group);
        label(content,contact,19);
        input(content,contactPlaceholder);
        space(content,36);
        var actions=row(content,80);
        button(actions,"Back",backAction,fieldColor,100);
        button(actions,decline,declineAction,new Color(.55f,.2f,.2f),300);
        var next=button(actions,proceed,continueAction,accentColor,240);
        next.interactable=false;
        return new researchConsentUi.form {panel=scroll.transform.parent.gameObject,scroll=scroll,
            agreement=agree,followUpYes=yesToggle,followUpNo=noToggle,continueButton=next};
    }
    private static RectTransform page(Transform canvas,string name,string title,out UnityEngine.UI.ScrollRect scroll)
    {
        var panel=rect(name,canvas); stretch(panel,32,32,32,32);
        image(panel,panelColor);
        var header=rect("header",panel);
        header.anchorMin=new Vector2(0,1); header.anchorMax=Vector2.one;
        header.pivot=new Vector2(.5f,1); header.sizeDelta=new Vector2(0,70); header.anchoredPosition=Vector2.zero;
        image(header,headerColor);
        var titleText=label(header,title,28,true); stretch(titleText.rectTransform,24,24,12,12);
        var view=rect("scrollView",panel); stretch(view,12,12,82,12);
        scroll=view.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.horizontal=false; scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=35;
        var viewport=rect("viewport",view); stretch(viewport,0,22,0,0);
        image(viewport,Color.white); viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
        var content=rect("content",viewport);
        content.anchorMin=new Vector2(0,1); content.anchorMax=Vector2.one; content.pivot=new Vector2(.5f,1);
        content.sizeDelta=Vector2.zero;
        var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding=new RectOffset(24,24,20,24); layout.spacing=16;
        layout.childControlWidth=true; layout.childControlHeight=true; layout.childForceExpandWidth=true; layout.childForceExpandHeight=false;
        content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport; scroll.content=content;
        var bar=rect("scrollbar",view);
        bar.anchorMin=new Vector2(1,0); bar.anchorMax=Vector2.one; bar.pivot=new Vector2(1,.5f); bar.sizeDelta=new Vector2(14,0);
        image(bar,fieldColor);
        var handle=rect("handle",bar); stretch(handle,0,0,0,0); var handleImage=image(handle,accentColor);
        var scrollbar=bar.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
        scrollbar.handleRect=handle; scrollbar.targetGraphic=handleImage; scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar=scrollbar;
        return content;
    }
    private static RectTransform rect(string name,Transform parent)
    {
        var t=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        if(parent!=null)t.SetParent(parent,false);
        return t;
    }
    private static void stretch(RectTransform t,float left,float right,float top,float bottom)
    {
        t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=new Vector2(left,bottom);t.offsetMax=new Vector2(-right,-top);
    }
    private static UnityEngine.UI.Image image(RectTransform t,Color color)
    {
        var i=t.gameObject.AddComponent<UnityEngine.UI.Image>();i.color=color;return i;
    }
    private static TextMeshProUGUI label(Transform parent,string value,float size,bool bold=false)
    {
        var t=rect("text",parent).gameObject.AddComponent<TextMeshProUGUI>();
        t.font=font;t.text=value;t.fontSize=size;t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;
        t.color=Color.white;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;
        return t;
    }
    private static void space(Transform parent,float height)
    {
        rect("spacing",parent).gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=height;
    }
    private static TMP_InputField input(Transform parent,string placeholder)
    {
        var field=UnityEngine.Object.Instantiate(inputTemplate,parent);
        field.name="input";
        field.onValueChanged=new TMP_InputField.OnChangeEvent();
        field.onEndEdit=new TMP_InputField.SubmitEvent();
        field.SetTextWithoutNotify("");
        ((TMP_Text)field.placeholder).text=placeholder;
        ((TMP_Text)field.placeholder).fontSize=19;
        field.textComponent.fontSize=19;
        var size=field.GetComponent<UnityEngine.UI.LayoutElement>()??field.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        size.minHeight=48;size.preferredHeight=48;size.flexibleWidth=1;
        field.characterLimit=0;
        return field;
    }
    private static UnityEngine.UI.Toggle toggle(Transform parent,string value,UnityEngine.UI.ToggleGroup group)
    {
        var root=rect("choice",parent);
        var layout=root.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing=14;layout.childAlignment=TextAnchor.UpperLeft;
        layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;
        var box=rect("background",root);
        var bg=image(box,new Color(.22f,.22f,.28f));
        var size=box.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();size.minWidth=26;size.preferredWidth=26;size.minHeight=26;size.preferredHeight=26;
        var mark=rect("selected",box);stretch(mark,5,5,5,5);var graphic=image(mark,accentColor);
        var text=label(root,value,19);text.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth=1;
        var toggle=root.gameObject.AddComponent<UnityEngine.UI.Toggle>();
        toggle.targetGraphic=bg;toggle.graphic=graphic;toggle.isOn=false;toggle.group=group;
        if(group!=null) {
            var knob=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            bg.sprite=knob;graphic.sprite=knob;
        }
        // The full wrapped label is clickable.
        var hit=image(root,new Color(0,0,0,0));hit.raycastTarget=true;
        return toggle;
    }
    private static RectTransform row(Transform parent,float height)
    {
        var t=rect("actions",parent);t.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=height;
        var l=t.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        l.spacing=16;l.childAlignment=TextAnchor.MiddleRight;l.childControlWidth=true;l.childControlHeight=true;
        l.childForceExpandWidth=false;l.childForceExpandHeight=true;
        return t;
    }
    private static UnityEngine.UI.Button button(Transform parent,string text,UnityAction action,Color color,float width)
    {
        var t=rect("button",parent);var bg=image(t,color);
        var layout=t.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();layout.preferredWidth=width;layout.minWidth=width;
        var b=t.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=bg;
        var c=b.colors;c.disabledColor=new Color(.45f,.45f,.45f,.6f);b.colors=c;
        var labelText=label(t,text,20);labelText.alignment=TextAlignmentOptions.Center;stretch(labelText.rectTransform,12,12,8,8);
        UnityEventTools.AddPersistentListener(b.onClick,action);
        return b;
    }
}
