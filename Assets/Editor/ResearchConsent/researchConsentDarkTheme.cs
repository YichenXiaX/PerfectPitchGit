using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

// Edits the existing hierarchy in place; preserves form copy, fields and callbacks.
public static class researchConsentDarkTheme
{
    private static readonly Color background = hex("#171819");
    private static readonly Color surface = hex("#212224");
    private static readonly Color raised = hex("#292B2D");
    private static readonly Color border = hex("#45484B");
    private static readonly Color primary = hex("#F2F3F3");
    private static readonly Color secondary = hex("#CDD0D2");
    private static readonly Color muted = hex("#92989C");
    private static readonly Color mint = hex("#A4EBC9");
    private static Sprite rounded, roundedLine, pill, pillLine, ring, document, orbit, arrow, controlBox, controlRing;
    private static TMP_FontAsset font;
    private const string folder = "Assets/UI/ResearchConsent/Theme";

    [MenuItem("Tools/Research Consent/Apply Dark Theme")]
    public static void apply()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<researchConsentUi>(FindObjectsInactive.Include);
        if(ui==null || ui.gameObject.scene.path!="Assets/Scenes/researchConsent.unity")
            throw new InvalidOperationException("Open the researchConsent scene first.");
        apply(ui);
        EditorSceneManager.SaveScene(ui.gameObject.scene);
    }

    public static void apply(researchConsentUi ui)
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play mode before editing the theme.");
        font=ui.adult.agreement.GetComponentInChildren<TMP_Text>(true).font;
        rounded=shape("rounded",16,false);
        roundedLine=shape("roundedOutline",16,true);
        pill=shape("pill",32,false);
        pillLine=shape("pillOutline",32,true);
        ring=shape("radioOutline",32,true);
        controlBox=controlOutline("checkboxOutline",10);controlRing=controlOutline("radioControlOutline",31);
        document=icon("document",0);orbit=icon("orbit",1);arrow=icon("arrow",2);
        var bg=ui.transform.Find("background").GetComponent<UnityEngine.UI.Image>();
        bg.sprite=null;bg.color=background;bg.raycastTarget=false;
        var camera=ui.gameObject.scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Camera>()).FirstOrDefault();
        if(camera!=null)camera.backgroundColor=background;

        var brand=node(ui.transform,"brandBar");
        brand.anchorMin=new Vector2(.5f,1);brand.anchorMax=new Vector2(.5f,1);
        brand.pivot=new Vector2(.5f,1);brand.anchoredPosition=new Vector2(0,-18);brand.sizeDelta=new Vector2(1040,48);
        var logo=node(brand,"orbit");
        logo.anchorMin=logo.anchorMax=new Vector2(0,.5f);logo.pivot=new Vector2(0,.5f);
        logo.anchoredPosition=new Vector2(8,0);logo.sizeDelta=new Vector2(32,32);
        paint(logo,orbit,mint,false,UnityEngine.UI.Image.Type.Simple);
        var brandText=textNode(brand,"brandName","Comet Catcher",20,false);
        stretch(brandText.rectTransform,52,0,0,0);brandText.alignment=TextAlignmentOptions.MidlineLeft;
        var line=node(brand,"divider");line.anchorMin=new Vector2(0,0);line.anchorMax=new Vector2(1,0);
        line.pivot=new Vector2(.5f,0);line.sizeDelta=new Vector2(0,1);line.anchoredPosition=Vector2.zero;
        paint(line,null,hex("#303335"),false);

        foreach(var f in new[]{ui.child,ui.parent,ui.adult}) form(f);
        opening(ui);
        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
    }

    private static void shell(GameObject panel,UnityEngine.UI.ScrollRect scroll,bool welcome)
    {
        var p=(RectTransform)panel.transform;
        p.anchorMin=new Vector2(.5f,0);p.anchorMax=new Vector2(.5f,1);
        p.pivot=new Vector2(.5f,.5f);p.sizeDelta=new Vector2(1040,-100);p.anchoredPosition=new Vector2(0,-24);
        paint(p,rounded,surface,true);
        outline(p,roundedLine,hex("#36393B"));
        var header=p.Find("header").GetComponent<RectTransform>();
        header.sizeDelta=new Vector2(0,92);header.anchoredPosition=Vector2.zero;
        header.GetComponent<UnityEngine.UI.Image>().color=Color.clear;
        var title=header.GetComponentInChildren<TMP_Text>(true);
        if(title!=null){title.fontSize=32;title.color=primary;stretch(title.rectTransform,48,48,24,16);}
        var view=(RectTransform)scroll.transform;stretch(view,24,18,welcome?16:98,22);
        stretch(scroll.viewport,0,16,0,0);
        var layout=scroll.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding=new RectOffset(24,24,welcome?24:12,28);layout.spacing=20;
        scroll.scrollSensitivity=42;
        var bar=(RectTransform)scroll.verticalScrollbar.transform;
        bar.sizeDelta=new Vector2(6,0);
        bar.GetComponent<UnityEngine.UI.Image>().color=Color.clear;
        paint(scroll.verticalScrollbar.handleRect,pill,hex("#62676A"),true);
        scroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
        foreach(var t in scroll.content.GetComponentsInChildren<TMP_Text>(true)){
            t.color=secondary;t.lineSpacing=5;t.enableAutoSizing=false;
            t.fontSize=t.fontStyle.HasFlag(FontStyles.Bold)?22:20;
        }
    }

    private static void form(researchConsentUi.form f)
    {
        shell(f.panel,f.scroll,false);
        foreach(var t in f.scroll.content.GetComponentsInChildren<TMP_Text>(true)){
            if(t.fontStyle.HasFlag(FontStyles.Bold))t.color=primary;
        }
        foreach(var field in f.panel.GetComponentsInChildren<TMP_InputField>(true)){
            var r=(RectTransform)field.transform;
            paint(r,rounded,raised,true);outline(r,roundedLine,border);
            field.customCaretColor=true;field.caretColor=mint;
            field.selectionColor=new Color(.40f,.75f,.59f,.35f);
            field.textComponent.color=primary;
            field.textComponent.fontStyle=FontStyles.Normal;
            var placeholder=(TMP_Text)field.placeholder;
            placeholder.color=muted;placeholder.fontStyle=FontStyles.Normal;
            field.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=54;
            var area=field.textViewport;
            area.offsetMin=new Vector2(16,8);area.offsetMax=new Vector2(-16,-8);
            var colors=field.colors;colors.normalColor=Color.white;colors.highlightedColor=hex("#BFCBC5");
            colors.selectedColor=hex("#BFCBC5");colors.pressedColor=Color.white;field.colors=colors;
        }
        foreach(var toggle in new[]{f.agreement,f.followUpYes,f.followUpNo}){
            bool radio=toggle!=f.agreement;
            var box=(RectTransform)toggle.targetGraphic.transform;
            paint(box,radio?controlRing:controlBox,hex("#B2BABE"),true,UnityEngine.UI.Image.Type.Simple);
            paint((RectTransform)toggle.graphic.transform,radio?pill:rounded,mint,false,UnityEngine.UI.Image.Type.Simple);
            var group=toggle.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            group.spacing=14;
            var size=box.GetComponent<UnityEngine.UI.LayoutElement>();
            size.minWidth=size.preferredWidth=24;size.minHeight=size.preferredHeight=24;
            var colors=toggle.colors;colors.normalColor=Color.white;colors.highlightedColor=mint;colors.selectedColor=mint;toggle.colors=colors;
        }
        var buttons=f.panel.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach(var b in buttons){
            string caption=b.GetComponentInChildren<TMP_Text>().text;
            bool back=caption=="Back";bool next=b==f.continueButton;
            buttonStyle(b,next,back,false);
            if(next && b.GetComponent<consentButtonLabel>()==null)b.gameObject.AddComponent<consentButtonLabel>();
            var size=b.GetComponent<UnityEngine.UI.LayoutElement>();
            size.minWidth=size.preferredWidth=back?88:next?228:caption=="I Do Not Want to Participate"?304:230;
        }
        var actions=(RectTransform)f.continueButton.transform.parent;
        actions.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=56;
        var layout=actions.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();layout.spacing=14;
        // Divider above the controls is decorative and excluded from layout.
        var divider=node(actions,"divider");
        (divider.GetComponent<UnityEngine.UI.LayoutElement>()??divider.gameObject.AddComponent<UnityEngine.UI.LayoutElement>()).ignoreLayout=true;
        divider.anchorMin=new Vector2(0,1);divider.anchorMax=Vector2.one;divider.sizeDelta=new Vector2(0,1);
        divider.anchoredPosition=new Vector2(0,20);paint(divider,null,hex("#3A3D3F"),false);
    }

    private static void opening(researchConsentUi ui)
    {
        var scroll=ui.openingPanel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
        shell(ui.openingPanel,scroll,true);
        var content=scroll.content;
        var layout=content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding=new RectOffset(90,90,24,24);layout.spacing=18;
        var header=ui.openingPanel.transform.Find("header");
        var title=header.GetComponentInChildren<TMP_Text>(true);
        if(title==null)title=content.Find("welcomeTitle")?.GetComponent<TMP_Text>();
        title.transform.SetParent(content,false);title.name="welcomeTitle";
        title.fontSize=42;title.fontStyle=FontStyles.Bold;title.color=primary;
        title.alignment=TextAlignmentOptions.Center;title.lineSpacing=0;
        var symbol=node(content,"document");
        var size=symbol.GetComponent<UnityEngine.UI.LayoutElement>()??symbol.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        size.preferredHeight=48;size.minHeight=48;
        var doc=node(symbol,"icon");doc.anchorMin=doc.anchorMax=new Vector2(.5f,.5f);
        doc.sizeDelta=new Vector2(44,48);doc.anchoredPosition=Vector2.zero;
        paint(doc,document,muted,false,UnityEngine.UI.Image.Type.Simple);
        symbol.SetAsFirstSibling();title.transform.SetSiblingIndex(1);
        header.gameObject.SetActive(false);
        foreach(Transform child in content){
            var text=child.GetComponent<TMP_Text>();
            if(text!=null && text!=title){
                text.color=secondary;text.fontSize=20;text.lineSpacing=7;
                text.alignment=text.text=="Age choices:"?TextAlignmentOptions.Left:TextAlignmentOptions.Center;
            }
            if(child.name=="spacing")child.gameObject.SetActive(false);
        }
        var actions=content.Find("actions");
        actions.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=92;
        var row=actions.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();row.spacing=20;row.childForceExpandWidth=true;
        foreach(var b in actions.GetComponentsInChildren<UnityEngine.UI.Button>(true)){
            buttonStyle(b,false,false,true);
            var element=b.GetComponent<UnityEngine.UI.LayoutElement>();
            element.minWidth=0;element.preferredWidth=0;element.flexibleWidth=1;
            var text=b.GetComponentInChildren<TMP_Text>();text.fontSize=24;text.fontStyle=FontStyles.Bold;
            text.alignment=TextAlignmentOptions.MidlineLeft;stretch(text.rectTransform,28,66,10,10);
            var iconRect=node(b.transform,"arrow");iconRect.anchorMin=iconRect.anchorMax=new Vector2(1,.5f);
            iconRect.anchoredPosition=new Vector2(-32,0);iconRect.sizeDelta=new Vector2(26,26);
            paint(iconRect,arrow,mint,false,UnityEngine.UI.Image.Type.Simple);
        }
    }
    private static void buttonStyle(UnityEngine.UI.Button b,bool next,bool back,bool card)
    {
        var r=(RectTransform)b.transform;
        paint(r,card?rounded:pill,back?Color.clear:Color.white,true);
        var colors=b.colors;
        colors.normalColor=back?Color.white:next?hex("#336B53"):raised;
        colors.highlightedColor=back?Color.white:next?hex("#418768"):hex("#383D3F");
        colors.pressedColor=back?Color.white:next?hex("#285941"):hex("#242729");
        colors.selectedColor=colors.highlightedColor;colors.disabledColor=hex("#343739");
        colors.fadeDuration=.12f;b.colors=colors;
        var text=b.GetComponentInChildren<TMP_Text>();
        text.color=back?mint:primary;text.fontStyle=FontStyles.Normal;text.fontSize=19;
        if(!back && !next)outline(r,card?roundedLine:pillLine,border);
    }

    private static RectTransform node(Transform parent,string name)
    {
        var found=parent.Find(name);
        if(found!=null)return (RectTransform)found;
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;
    }
    private static void stretch(RectTransform r,float l,float rr,float t,float b)
    {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(l,b);r.offsetMax=new Vector2(-rr,-t);}
    private static UnityEngine.UI.Image paint(RectTransform r,Sprite sprite,Color color,bool hit,UnityEngine.UI.Image.Type type=UnityEngine.UI.Image.Type.Sliced)
    {var i=r.GetComponent<UnityEngine.UI.Image>()??r.gameObject.AddComponent<UnityEngine.UI.Image>();i.sprite=sprite;i.type=type;i.color=color;i.raycastTarget=hit;return i;}
    private static void outline(RectTransform parent,Sprite sprite,Color color)
    {var r=node(parent,"outline");stretch(r,0,0,0,0);paint(r,sprite,color,false);r.SetAsLastSibling();}
    private static TMP_Text textNode(Transform parent,string name,string value,float size,bool bold)
    {var r=node(parent,name);var t=r.GetComponent<TextMeshProUGUI>()??r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;t.color=primary;t.raycastTarget=false;return t;}
    private static Color hex(string value){ColorUtility.TryParseHtmlString(value,out var c);return c;}

    private static Sprite controlOutline(string name,float radius)
    {
        return sprite(name,(x,y)=>{
            float qx=Mathf.Abs(x-32)-(31-radius),qy=Mathf.Abs(y-32)-(31-radius);
            float d=new Vector2(Mathf.Max(qx,0),Mathf.Max(qy,0)).magnitude+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
            return Mathf.Clamp01(3-Mathf.Abs(d+3));
        },Vector4.zero);
    }
    private static Sprite shape(string name,float radius,bool stroke)
    {
        return sprite(name,(x,y)=>{
            float qx=Mathf.Abs(x-32)-(31-radius),qy=Mathf.Abs(y-32)-(31-radius);
            float d=new Vector2(Mathf.Max(qx,0),Mathf.Max(qy,0)).magnitude+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
            return stroke?Mathf.Clamp01(1-Mathf.Abs(d+1)):Mathf.Clamp01(.5f-d);
        },radius==32?new Vector4(31,31,31,31):new Vector4(17,17,17,17));
    }
    private static float line(float x,float y,float ax,float ay,float bx,float by,float width=1.5f)
    {
        var p=new Vector2(x,y);var a=new Vector2(ax,ay);var b=new Vector2(bx,by);
        var ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude);
        return Mathf.Clamp01(width+.5f-Vector2.Distance(p,a+t*ab));
    }
    private static Sprite icon(string name,int kind)
    {
        return sprite(name,(x,y)=>{
            float a=0;
            if(kind==0){
                a=Mathf.Max(line(x,y,14,5,14,59),line(x,y,14,59,39,59),line(x,y,39,59,51,47),line(x,y,51,47,51,5),line(x,y,51,5,14,5),
                    line(x,y,39,59,39,47),line(x,y,39,47,51,47));
                foreach(float h in new[]{18f,29f,39f})a=Mathf.Max(a,line(x,y,23,h,42,h));
            }else if(kind==1){
                a=Mathf.Clamp01(1.5f-Mathf.Abs(Vector2.Distance(new Vector2(x,y),new Vector2(30,32))-18));
                a=Mathf.Max(a,Mathf.Clamp01(3.5f-Vector2.Distance(new Vector2(x,y),new Vector2(52,48))));
                a=Mathf.Max(a,line(x,y,12,24,6,20),line(x,y,6,20,17,17));
            }else a=Mathf.Max(line(x,y,8,32,54,32,2),line(x,y,39,47,54,32,2),line(x,y,54,32,39,17,2));
            return a;
        },Vector4.zero);
    }
    private static Sprite sprite(string name,Func<float,float,float> alpha,Vector4 slice)
    {
        System.IO.Directory.CreateDirectory(folder);
        string path=folder+"/"+name+".png";
        if(!System.IO.File.Exists(path)){
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)pixels[y*64+x]=new Color(1,1,1,alpha(x+.5f,y+.5f));
            texture.SetPixels(pixels);texture.Apply();
            System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteBorder=slice;settings.spriteMode=1;importer.SetTextureSettings(settings);importer.spritePixelsPerUnit=100;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
