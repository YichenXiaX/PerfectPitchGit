using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Settings owns a separate canvas: the gameplay Canvas/HUD scaler is untouched.
public class terminalSettingsView : MonoBehaviour
{
    private RectTransform settingsPanel;
    private void Start()
    {
        var ui=GetComponent<settingsUi>();var panel=(RectTransform)ui.settingPanel.transform;settingsPanel=panel;
        gameMenuTerminal.isolate(ui.settingPanel,"settingsTerminal",120);
        panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;panel.sizeDelta=new Vector2(660,600);
        var adaptive=panel.gameObject.AddComponent<terminalPanelWidth>();adaptive.maximumWidth=660;
        signalTerminal.image(panel,signalTerminal.panelColor,true);
        foreach(var t in panel.GetComponentsInChildren<TMP_Text>(true))signalTerminal.type(t,false,17);
        foreach(var image in panel.GetComponentsInChildren<Image>(true))
        {
            if(image.name=="Checkmark" || image.name=="Fill" || image.name=="Handle")image.color=signalTerminal.accent;
            else if(image.transform!=panel){image.sprite=null;image.color=new Color32(14,31,45,255);}
        }
        foreach(var toggle in panel.GetComponentsInChildren<Toggle>(true))if(toggle.targetGraphic!=null)signalTerminal.frame((RectTransform)toggle.targetGraphic.transform,signalTerminal.rule).box=true;
        var header=(RectTransform)panel.Find("Header");terminalPanel.place(header,0,0,0,70);signalTerminal.image(header,signalTerminal.panelColor);
        var title=header.Find("SettingsTitle").GetComponent<TMP_Text>();title.gameObject.SetActive(true);title.transform.localScale=Vector3.one;title.margin=Vector4.zero;signalTerminal.type(title,true,25);title.alignment=TextAlignmentOptions.MidlineLeft;title.text="Settings";terminalPanel.place(title.rectTransform,20,22,-100,35);
        var close=header.GetComponentInChildren<Button>();signalTerminal.styleButton(close,signalTerminal.actionStyle.quiet);var c=(RectTransform)close.transform;c.anchorMin=c.anchorMax=new Vector2(1,.5f);c.pivot=new Vector2(1,.5f);c.sizeDelta=new Vector2(80,40);c.anchoredPosition=new Vector2(-16,0);close.GetComponentInChildren<TMP_Text>().text="Close";
        var top=signalTerminal.node(panel,"topRule");terminalPanel.place(top,0,0,0,1);signalTerminal.frame(top,signalTerminal.accent);
        var bottom=signalTerminal.node(header,"headerRule");terminalPanel.place(bottom,0,69,0,1);signalTerminal.frame(bottom,signalTerminal.rule,true);
        var scroll=panel.GetComponentInChildren<ScrollRect>();var sr=(RectTransform)scroll.transform;signalTerminal.fill(sr);sr.offsetMin=new Vector2(20,20);sr.offsetMax=new Vector2(-20,-80);scroll.scrollSensitivity=28;
        if(scroll.GetComponent<terminalScrollFocus>()==null)scroll.gameObject.AddComponent<terminalScrollFocus>();
        var content=scroll.content;var layout=content.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(0,8,0,12);layout.spacing=16;
        var controlSection=signalTerminal.node(content,"controlPreferences");signalTerminal.column(controlSection,10);controlSection.SetAsFirstSibling();
        var label=signalTerminal.text(controlSection,"label","CONTROL LAYOUT",11,16,true);label.color=signalTerminal.secondary;
        terminalLayoutSelector.create(controlSection);
        signalTerminal.text(controlSection,"instruction","Hold a lane key and F to fire. Release F before a new signal.",16,24);
        signalTerminal.height(controlSection,150);
        ui.leftHandedToggle.transform.parent.gameObject.SetActive(false);
        foreach(Transform section in content)
        {
            if(section==controlSection)continue;
            var sectionLayout=section.GetComponent<VerticalLayoutGroup>();if(sectionLayout!=null){sectionLayout.padding=new RectOffset(12,12,12,12);sectionLayout.spacing=8;}
            foreach(Transform child in section){var text=child.GetComponent<TMP_Text>();if(text!=null){signalTerminal.type(text,true,14);text.color=signalTerminal.accent;signalTerminal.height((RectTransform)child,24);}}
        }
        if(panel.GetComponent<terminalKeyboardNavigation>()==null)panel.gameObject.AddComponent<terminalKeyboardNavigation>();
    }
    private void LateUpdate()
    {
        if(settingsPanel==null)return;
        var parent=(RectTransform)settingsPanel.parent;
        bool narrow=parent.rect.width/parent.rect.height<1.5f;
        float height=narrow?Mathf.Min(520,parent.rect.height-185):600;
        settingsPanel.sizeDelta=new Vector2(settingsPanel.sizeDelta.x,height);
        settingsPanel.anchoredPosition=new Vector2(0,narrow?parent.rect.height*.5f-110-height*.5f:0);
    }
}
