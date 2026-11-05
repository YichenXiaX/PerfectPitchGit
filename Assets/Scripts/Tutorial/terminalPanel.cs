using UnityEngine;
using TMPro;
public class terminalPanel : MonoBehaviour
{
    public RectTransform content,actions;
    public TMP_Text metadata,title,progress;
    public float preferredWidth=620,top=145;
    public bool reserveTutorialHud;
    private RectTransform header,leftRule,bottomRule,viewport,scrollbar;
    private UnityEngine.UI.ScrollRect scroller;
    public static terminalPanel create(Transform parent,string name)
    {
        var r=signalTerminal.node(parent,name);r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,1);signalTerminal.image(r,signalTerminal.panelColor,true);
        var p=r.gameObject.AddComponent<terminalPanel>();
        p.header=signalTerminal.node(r,"header");p.header.anchorMin=new Vector2(0,1);p.header.anchorMax=Vector2.one;p.header.pivot=new Vector2(.5f,1);p.header.sizeDelta=new Vector2(0,76);
        var rule=signalTerminal.node(p.header,"topRule");rule.anchorMin=new Vector2(0,1);rule.anchorMax=Vector2.one;rule.pivot=new Vector2(.5f,1);rule.sizeDelta=new Vector2(0,1);signalTerminal.frame(rule,signalTerminal.accent);
        var hr=signalTerminal.node(p.header,"headerRule");hr.anchorMin=Vector2.zero;hr.anchorMax=new Vector2(1,0);hr.sizeDelta=new Vector2(0,1);signalTerminal.frame(hr,signalTerminal.rule,true);
        p.metadata=signalTerminal.text(p.header,"metadata","",11,16,true);place(p.metadata.rectTransform,20,14,-90,18);
        p.title=signalTerminal.text(p.header,"heading","",25,34,true);place(p.title.rectTransform,20,33,-20,35);p.title.characterSpacing=-1;
        p.progress=signalTerminal.text(p.header,"progress","",11,16,true);p.progress.color=signalTerminal.accent;p.progress.alignment=TextAlignmentOptions.Right;place(p.progress.rectTransform,0,15,-22,18);
        p.metadata.color=signalTerminal.accent;
        p.viewport=signalTerminal.node(r,"bodyViewport");signalTerminal.image(p.viewport,Color.clear,true);p.viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        p.content=signalTerminal.node(p.viewport,"body");p.content.anchorMin=new Vector2(0,1);p.content.anchorMax=Vector2.one;p.content.pivot=new Vector2(.5f,1);signalTerminal.column(p.content,12);
        p.scroller=p.viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();p.scroller.viewport=p.viewport;p.scroller.content=p.content;p.scroller.horizontal=false;p.scroller.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;p.scroller.scrollSensitivity=28;p.viewport.gameObject.AddComponent<terminalScrollFocus>();
        p.scrollbar=signalTerminal.node(r,"bodyScrollbar");signalTerminal.image(p.scrollbar,Color.clear,true);var bar=p.scrollbar.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();var handle=signalTerminal.node(p.scrollbar,"handle");signalTerminal.fill(handle);var handleImage=signalTerminal.image(handle,signalTerminal.rule,true);bar.handleRect=handle;bar.targetGraphic=handleImage;bar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;p.scroller.verticalScrollbar=bar;p.scroller.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
        p.leftRule=signalTerminal.node(r,"bodyRule");signalTerminal.frame(p.leftRule,signalTerminal.rule,false,true);
        p.actions=signalTerminal.node(r,"actions");p.actions.anchorMin=Vector2.zero;p.actions.anchorMax=new Vector2(1,0);p.actions.pivot=new Vector2(.5f,0);signalTerminal.row(p.actions,10);p.actions.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childAlignment=TextAnchor.MiddleRight;
        p.bottomRule=signalTerminal.node(r,"actionRule");signalTerminal.frame(p.bottomRule,signalTerminal.rule,true);
        return p;
    }
    public static void place(RectTransform r,float left,float down,float right,float height)
    {r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.offsetMin=new Vector2(left,-down-height);r.offsetMax=new Vector2(right,-down);}
    public void reflow()
    {
        var r=(RectTransform)transform;var parent=(RectTransform)r.parent;float w=Mathf.Min(Mathf.Min(preferredWidth,680),parent.rect.width-32);r.sizeDelta=new Vector2(w,r.sizeDelta.y);
        bool narrowLesson=reserveTutorialHud && parent.rect.width/parent.rect.height<1.5f;
        float effectiveTop=narrowLesson?Mathf.Max(top,110):top;
        float bottomMargin=narrowLesson?75:20;
        float bodyWidth=w-60;
        foreach(Transform child in content)if(child.gameObject.activeSelf){var t=child.GetComponent<TMP_Text>();if(t!=null)signalTerminal.height((RectTransform)child,Mathf.Max(t.fontSize+4,t.GetPreferredValues(t.text,bodyWidth,0).y+2));}
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float bodyHeight=UnityEngine.UI.LayoutUtility.GetPreferredHeight(content)+28;
        bool hasActions=false;foreach(Transform a in actions)if(a.gameObject.activeSelf)hasActions=true;
        float footer=hasActions?58:14;float visibleBody=Mathf.Min(bodyHeight,Mathf.Max(80,parent.rect.height-effectiveTop-bottomMargin-76-footer));float height=76+visibleBody+footer;
        r.sizeDelta=new Vector2(w,height);r.anchoredPosition=new Vector2(0,-effectiveTop);
        viewport.anchorMin=new Vector2(0,1);viewport.anchorMax=Vector2.one;viewport.pivot=new Vector2(.5f,1);viewport.offsetMin=new Vector2(40,-76-visibleBody+14);viewport.offsetMax=new Vector2(-20,-90);
        scrollbar.anchorMin=scrollbar.anchorMax=Vector2.one;scrollbar.pivot=new Vector2(1,1);scrollbar.offsetMin=new Vector2(-8,-76-visibleBody+14);scrollbar.offsetMax=new Vector2(-4,-90);
        content.sizeDelta=new Vector2(0,bodyHeight-28);scroller.vertical=bodyHeight>visibleBody+1;
        if(!scroller.vertical)content.anchoredPosition=Vector2.zero;
        leftRule.anchorMin=leftRule.anchorMax=new Vector2(0,1);leftRule.pivot=new Vector2(0,1);leftRule.anchoredPosition=new Vector2(20,-76);leftRule.sizeDelta=new Vector2(1,visibleBody);
        actions.offsetMin=new Vector2(20,10);actions.offsetMax=new Vector2(-20,50);actions.gameObject.SetActive(hasActions);
        bottomRule.anchorMin=Vector2.zero;bottomRule.anchorMax=new Vector2(1,0);bottomRule.pivot=new Vector2(.5f,0);bottomRule.anchoredPosition=new Vector2(0,footer);bottomRule.sizeDelta=new Vector2(0,1);bottomRule.gameObject.SetActive(hasActions);
    }
    private void LateUpdate(){reflow();}
}
