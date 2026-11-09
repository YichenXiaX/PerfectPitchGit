using UnityEngine;
using TMPro;

// New UI roots only. Gameplay HUD fonts, materials and canvases are never changed here.
public static class signalTerminal
{
    public static readonly Color background = new Color32(7,14,26,255);
    public static readonly Color panelColor = new Color32(9,25,34,255);
    public static readonly Color ink = new Color32(229,241,255,255);
    public static readonly Color secondary = new Color32(165,189,215,255);
    public static readonly Color accent = new Color32(145,212,233,255);
    public static readonly Color rule = new Color32(62,98,112,255);
    public enum actionStyle { primary, secondary, quiet }
    public static TMP_FontAsset bodyFont => Resources.Load<TMP_FontAsset>("SignalTerminal/barlowRegular");
    public static TMP_FontAsset buttonFont => monoFont;
    public static TMP_FontAsset monoFont => Resources.Load<terminalFontSet>("SignalTerminal/fontSet").heading;
    public static RectTransform node(Transform parent,string name)
    {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;}
    public static void fill(RectTransform r,float inset=0)
    {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.one*inset;r.offsetMax=-Vector2.one*inset;}
    public static void height(RectTransform r,float h)
    {var e=r.GetComponent<UnityEngine.UI.LayoutElement>();if(e==null)e=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();e.minHeight=h;e.preferredHeight=h;e.flexibleHeight=0;}
    public static void width(RectTransform r,float w)
    {var e=r.GetComponent<UnityEngine.UI.LayoutElement>();if(e==null)e=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();e.minWidth=w;e.preferredWidth=w;e.flexibleWidth=0;}
    public static UnityEngine.UI.Image image(RectTransform r,Color color,bool raycast=false)
    {var i=r.GetComponent<UnityEngine.UI.Image>();if(i==null)i=r.gameObject.AddComponent<UnityEngine.UI.Image>();i.sprite=null;i.color=color;i.raycastTarget=raycast;return i;}
    public static void column(RectTransform r,float gap=12)
    {var l=r.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();if(l==null)l=r.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();l.spacing=gap;l.childControlHeight=true;l.childControlWidth=true;l.childForceExpandHeight=false;l.childForceExpandWidth=true;}
    public static void row(RectTransform r,float gap=10,bool expand=false)
    {var l=r.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();if(l==null)l=r.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();l.spacing=gap;l.childControlWidth=true;l.childControlHeight=true;l.childForceExpandWidth=expand;l.childForceExpandHeight=false;l.childAlignment=TextAnchor.MiddleLeft;}
    public static TMP_Text text(Transform parent,string name,string value,float size=18,float h=28,bool mono=false)
    {var r=node(parent,name);height(r,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();type(t,mono,size);t.text=value;return t;}
    public static void type(TMP_Text t,bool mono=false,float size=18)
    {t.font=mono?monoFont:bodyFont;t.fontSize=size;t.color=ink;t.fontStyle=FontStyles.Normal;t.overflowMode=TextOverflowModes.Overflow;t.characterSpacing=0;t.lineSpacing=2;t.raycastTarget=false;t.enableAutoSizing=false;t.textWrappingMode=TextWrappingModes.Normal;}
    public static terminalFrame frame(RectTransform r,Color color,bool dashed=false,bool vertical=false)
    {var n=node(r,"terminalRule");fill(n);var f=n.gameObject.AddComponent<terminalFrame>();f.color=color;f.dashed=dashed;f.vertical=vertical;f.raycastTarget=false;return f;}
    public static UnityEngine.UI.Button button(Transform parent,string name,string title,System.Action action,actionStyle style=actionStyle.secondary,float w=170)
    {var r=node(parent,name);height(r,40);width(r,w);image(r,rule,true);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();var t=text(r,"label",title,14,28,true);fill(t.rectTransform,8);t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.NoWrap;b.onClick.AddListener(()=>action());styleButton(b,style);return b;}
    public static void styleButton(UnityEngine.UI.Button b,actionStyle style=actionStyle.secondary)
    {
        var r=(RectTransform)b.transform;r.localScale=Vector3.one;var i=image(r,Color.white,true);b.targetGraphic=i;
        var c=b.colors;c.normalColor=style==actionStyle.primary?accent:style==actionStyle.quiet?new Color(9/255f,25/255f,34/255f,0):new Color32(16,33,50,255);
        c.highlightedColor=style==actionStyle.primary?new Color32(185,232,246,255):new Color32(29,62,82,255);
        c.selectedColor=c.highlightedColor;c.pressedColor=new Color32(87,143,164,255);c.disabledColor=new Color32(35,51,61,255);c.fadeDuration=.08f;b.colors=c;
        var t=b.GetComponentInChildren<TMP_Text>(true);if(t!=null){type(t,true,15);t.font=buttonFont;t.rectTransform.localScale=Vector3.one;t.margin=Vector4.zero;fill(t.rectTransform,8);t.color=style==actionStyle.primary?background:ink;t.alignment=TextAlignmentOptions.Center;}
        var existing=r.Find("buttonBorder");if(existing==null){var border=node(r,"buttonBorder");fill(border);var f=border.gameObject.AddComponent<terminalFrame>();f.box=true;f.color=style==actionStyle.primary?accent:rule;f.raycastTarget=false;}
        var nav=b.GetComponent<terminalFocus>();if(nav==null)nav=b.gameObject.AddComponent<terminalFocus>();
    }
    public static RectTransform scroll(Transform parent,RectTransform content,float inset=16)
    {
        var root=node(parent,"transmissionScroll");fill(root,inset);var view=node(root,"viewport");fill(view);image(view,Color.white,true);var mask=view.gameObject.AddComponent<UnityEngine.UI.Mask>();mask.showMaskGraphic=false;
        content.SetParent(view,false);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
        var fit=content.GetComponent<UnityEngine.UI.ContentSizeFitter>();if(fit==null)fit=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var scroll=root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.viewport=view;scroll.content=content;scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;root.gameObject.AddComponent<terminalScrollFocus>();return root;
    }
    public static void canvas(Canvas canvas)
    {var s=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();s.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1280,720);s.matchWidthOrHeight=1;}
    public static void style(Transform root)
    {
        foreach(var i in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {if(i.sprite==null || i.sprite.name.Contains("rounded") || i.sprite.name.Contains("pill")){i.sprite=null;i.type=UnityEngine.UI.Image.Type.Simple;i.color=panelColor;}}
        foreach(var t in root.GetComponentsInChildren<TMP_Text>(true))type(t,false,18);
        foreach(var b in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))styleButton(b);
    }
}
