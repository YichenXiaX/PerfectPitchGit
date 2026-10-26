using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Linq;

public static class accountUiBuilder
{
    private static TMP_FontAsset font;
    private static Sprite rounded;
    private static Color ink = new Color32(242,243,243,255);
    private static Color mint = new Color32(164,235,201,255);
    private static Color surface = new Color32(33,34,36,255);
    [MenuItem("Tools/Accounts/Build Account Screen")]
    public static void build()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");

        var oldScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/ResearchConsent/Theme/rounded.png");
        var root = new GameObject("accountUi", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(accountUi));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,900); scaler.matchWidthOrHeight = 1f;
        var ui = root.GetComponent<accountUi>();
        var overlay = node(root.transform,"accountPanel"); fill(overlay);
        image(overlay,new Color32(23,24,25,255),false); ui.panel = overlay.gameObject;
        var card = node(overlay,"card"); card.anchorMin=card.anchorMax=new Vector2(.5f,.5f); card.sizeDelta=new Vector2(660,850);
        image(card,surface,true);
        var content = node(card,"content"); fill(content,34);
        var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing=8;
        layout.childControlHeight=true; layout.childControlWidth=true; layout.childForceExpandHeight=false; layout.childForceExpandWidth=true;
        text(content,"brand","COMET CATCHER",20,30,mint);
        text(content,"title","Your next great run starts here.",32,48,ink);
        text(content,"privacy","Save your scores and progress. Research data collection is off.",17,42,new Color32(190,195,198,255));
        var login = node(content,"login"); height(login,360); column(login,10); ui.loginPanel=login.gameObject;
        ui.emailInput=input(login,"email","Email address",false);
        ui.passwordInput=input(login,"password","Password · 12+ characters for signup",true);
        ui.loginButton=button(login,"loginButton","Log in",true);
        ui.signupButton=button(login,"signupButton","Create account",false);
        text(login,"verifyHelp","New account? Enter the code from your confirmation email.",16,28,ink);
        ui.codeInput=input(login,"code","Email verification code",false);
        var row=node(login,"verifyActions"); height(row,42); horizontal(row);
        ui.verifyButton=button(row,"verifyButton","Verify email",false);
        ui.resendButton=button(row,"resendButton","Resend email",false);

        var dashboard=node(content,"dashboard"); height(dashboard,280); column(dashboard,10); ui.dashboardPanel=dashboard.gameObject;
        ui.progressText=text(dashboard,"progress","Loading progress...",21,180,ink);
        ui.playButton=button(dashboard,"play","Play",true);
        ui.refreshButton=button(dashboard,"refresh","Refresh progress",false);
        ui.statusText=text(content,"status","Log in to save your progress.",18,60,new Color32(205,210,212,255));
        ui.closeButton=button(content,"close","Return to game",true);
        ui.retryButton=button(content,"retry","Retry cloud save",false);
        ui.logoutButton=button(content,"logout","Save & log out",false);
        text(content,"sessionNotice","Login lasts until you close the game. Keep it open until your progress is saved.",15,42,new Color32(160,167,172,255));

        var bar=node(root.transform,"cloudStatus"); bar.anchorMin=new Vector2(0,0);bar.anchorMax=new Vector2(1,0);bar.pivot=new Vector2(.5f,0);bar.sizeDelta=new Vector2(0,44);
        image(bar,new Color32(23,24,25,240),false);ui.compactBar=bar.gameObject;
        var status=text(bar,"saveStatus","Cloud saves ready",16,30,ink);ui.saveText=status;
        status.rectTransform.anchorMin=new Vector2(0,0);status.rectTransform.anchorMax=new Vector2(1,1);status.rectTransform.offsetMin=new Vector2(20,5);status.rectTransform.offsetMax=new Vector2(-180,-5);
        ui.accountButton=button(bar,"account","Account",false);
        var accountRect=(RectTransform)ui.accountButton.transform;accountRect.anchorMin=accountRect.anchorMax=new Vector2(1,.5f);accountRect.pivot=new Vector2(1,.5f);accountRect.sizeDelta=new Vector2(150,34);accountRect.anchoredPosition=new Vector2(-16,0);
        ui.dashboardPanel.SetActive(false);ui.compactBar.SetActive(false);ui.closeButton.gameObject.SetActive(false);ui.retryButton.gameObject.SetActive(false);ui.logoutButton.gameObject.SetActive(false);
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets","Resources");
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/accountUi.prefab");
        Object.DestroyImmediate(root);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        new GameObject("eventSystem",typeof(UnityEngine.EventSystems.EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        var camera=new GameObject("camera",typeof(Camera)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(23,24,25,255);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/account.unity");
        var scenes=EditorBuildSettings.scenes.Where(s=>s.path!="Assets/Scenes/account.unity").Select(s=>new EditorBuildSettingsScene(s.path,s.path!="Assets/Scenes/researchConsent.unity" && s.enabled)).ToList();
        scenes.Insert(0,new EditorBuildSettingsScene("Assets/Scenes/account.unity",true));EditorBuildSettings.scenes=scenes.ToArray();
        EditorSceneManager.CloseScene(scene,true);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(oldScene);
        AssetDatabase.SaveAssets();
        Debug.Log("Account prefab and startup scene created. Consent demo preserved but excluded from the portfolio build.");
    }
    private static RectTransform node(Transform parent,string name) { var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r; }
    private static void fill(RectTransform r,float inset=0) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.one*inset;r.offsetMax=-Vector2.one*inset; }
    private static void height(RectTransform r,float value) { var e=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();e.preferredHeight=value;e.minHeight=value;e.flexibleHeight=0; }
    private static void column(RectTransform r,float spacing) {var l=r.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();l.spacing=spacing;l.childControlHeight=true;l.childControlWidth=true;l.childForceExpandHeight=false;l.childForceExpandWidth=true;}
    private static void horizontal(RectTransform r) {var l=r.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();l.spacing=12;l.childControlHeight=true;l.childControlWidth=true;l.childForceExpandHeight=true;l.childForceExpandWidth=true;}
    private static UnityEngine.UI.Image image(RectTransform r,Color color,bool round) {var i=r.gameObject.AddComponent<UnityEngine.UI.Image>();i.color=color;i.raycastTarget=true;if(round){i.sprite=rounded;i.type=UnityEngine.UI.Image.Type.Sliced;}return i;}
    private static TMP_Text text(Transform parent,string name,string value,int size,float h,Color color)
    {
        var r=node(parent,name);height(r,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;
    }
    private static UnityEngine.UI.Button button(Transform parent,string name,string label,bool primary)
    {
        var r=node(parent,name);height(r,42);var i=image(r,primary?mint:new Color32(48,51,54,255),true);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=i;
        var t=text(r,"label",label,19,40,primary?new Color32(20,35,28,255):ink);fill(t.rectTransform,4);t.alignment=TextAlignmentOptions.Center;
        return b;
    }
    private static TMP_InputField input(Transform parent,string name,string placeholder,bool password)
    {
        var r=node(parent,name);height(r,48);var i=image(r,new Color32(42,44,47,255),true);
        var field=r.gameObject.AddComponent<TMP_InputField>();field.targetGraphic=i;field.characterLimit=password?128:254;
        var viewport=node(r,"viewport");fill(viewport,12);viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var t=text(viewport,"text","",20,25,ink);fill(t.rectTransform);t.textWrappingMode=TextWrappingModes.NoWrap;
        var hint=text(viewport,"placeholder",placeholder,19,25,new Color32(162,169,174,255));fill(hint.rectTransform);hint.textWrappingMode=TextWrappingModes.NoWrap;
        field.textViewport=viewport;field.textComponent=(TextMeshProUGUI)t;field.placeholder=hint;field.lineType=TMP_InputField.LineType.SingleLine;
        field.contentType=password?TMP_InputField.ContentType.Password:TMP_InputField.ContentType.Standard;
        return field;
    }
}




