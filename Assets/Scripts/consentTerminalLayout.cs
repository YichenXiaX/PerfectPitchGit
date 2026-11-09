using UnityEngine;
using TMPro;

// Presentation only: lays out the existing forms without changing their data or callbacks.
[ExecuteAlways]
public class consentTerminalLayout : MonoBehaviour
{
    private researchConsentUi ui;
    private GameObject previousPage;
    private Vector2 previousSize;
    private string previousMessage;
    private void LateUpdate() { reflow(); }
    public void reflow()
    {
        if(ui==null)ui=GetComponent<researchConsentUi>();
        if(ui==null)return;
        var page=ui.openingPanel.activeSelf?ui.openingPanel:ui.adult.panel.activeSelf?ui.adult.panel:
            ui.child.panel.activeSelf?ui.child.panel:ui.parent.panel.activeSelf?ui.parent.panel:null;
        if(page==null)return;
        var canvas=(RectTransform)transform;
        string message=ui.statusText==null?"":ui.statusText.text;
        if(page==previousPage && canvas.rect.size==previousSize && message==previousMessage)return;
        previousPage=page;previousSize=canvas.rect.size;previousMessage=message;
        var panel=(RectTransform)page.transform;
        bool wide=menuBackdrop.isWide(canvas.rect.width);
        float width=Mathf.Min(wide?560:580,canvas.rect.width-32);
        panel.anchorMin=panel.anchorMax=new Vector2(.5f,.5f);panel.pivot=new Vector2(.5f,.5f);
        float x=wide?menuBackdrop.entryOffset(canvas.rect.width)+(width-menuBackdrop.entryWidth)*.5f:0;
        panel.anchoredPosition=new Vector2(x,0);panel.sizeDelta=new Vector2(width,panel.sizeDelta.y);
        var header=(RectTransform)panel.Find("header");
        var title=header.GetChild(0).GetComponent<TMP_Text>();
        float titleHeight=Mathf.Max(34,title.GetPreferredValues(title.text,width-40,0).y+2);
        float headerHeight=46+titleHeight;
        terminalPanel.place(header,0,0,0,headerHeight);
        terminalPanel.place(title.rectTransform,20,33,-20,titleHeight);
        terminalPanel.place((RectTransform)header.Find("consentMetadata"),20,14,-20,18);
        terminalPanel.place((RectTransform)panel.Find("terminalTop"),0,0,0,1);
        terminalPanel.place((RectTransform)panel.Find("terminalHeader"),0,headerHeight,0,1);
        var actions=(RectTransform)panel.Find("actions");
        var buttons=actions.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        float available=width-40,gap=8;
        var natural=new float[buttons.Length];float total=gap*(buttons.Length-1);
        for(int i=0;i<buttons.Length;i++)
        {
            var label=buttons[i].GetComponentInChildren<TMP_Text>(true);
            natural[i]=label.GetPreferredValues(label.text,10000,0).x+24;total+=natural[i];
        }
        int rows=1;
        if(total>available)rows=buttons.Length==3 && natural[1]+natural[2]+gap<=available?2:buttons.Length;
        float actionHeight=rows*40+(rows-1)*gap;
        actions.anchorMin=new Vector2(0,0);actions.anchorMax=new Vector2(1,0);actions.pivot=new Vector2(.5f,0);
        actions.offsetMin=new Vector2(20,12);actions.offsetMax=new Vector2(-20,12+actionHeight);
        float cursor=0;
        for(int i=0;i<buttons.Length;i++)
        {
            float left=0,top=0,buttonWidth=available;
            if(rows==1)
            {
                buttonWidth=buttons.Length==2?(available-gap)*.5f:natural[i]+(i==0?0:(available-total)/(buttons.Length-1));
                left=cursor;cursor+=buttonWidth+gap;
            }
            else if(rows==2 && buttons.Length==3)
            {
                if(i==0)buttonWidth=natural[0];
                else {top=48;buttonWidth=natural[i]+(available-natural[1]-natural[2]-gap)*.5f;left=i==1?0:available-buttonWidth;}
            }
            else top=i*48;
            var r=(RectTransform)buttons[i].transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(left,-top);r.sizeDelta=new Vector2(buttonWidth,40);
        }
        bool showStatus=!string.IsNullOrWhiteSpace(message) && message!="You are logged in.";
        float statusHeight=showStatus?ui.statusText.GetPreferredValues(message,available,0).y+4:0;
        float footerHeight=actionHeight+24+(showStatus?statusHeight+12:0);
        if(ui.statusText!=null)
        {
            ui.statusText.transform.SetParent(panel,false);ui.statusText.gameObject.SetActive(showStatus);
            var r=ui.statusText.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=new Vector2(1,0);r.pivot=new Vector2(.5f,0);
            r.offsetMin=new Vector2(20,actionHeight+24);r.offsetMax=new Vector2(-20,actionHeight+24+statusHeight);
        }
        var rule=(RectTransform)panel.Find("terminalFooter");rule.anchorMin=Vector2.zero;rule.anchorMax=new Vector2(1,0);
        rule.pivot=new Vector2(.5f,0);rule.sizeDelta=new Vector2(0,1);rule.anchoredPosition=new Vector2(0,footerHeight);
        var scroll=page.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
        var view=(RectTransform)scroll.transform;
        signalTerminal.fill(view);view.offsetMin=new Vector2(0,footerHeight+8);view.offsetMax=new Vector2(-10,-headerHeight-8);
        scroll.viewport.offsetMin=Vector2.zero;scroll.viewport.offsetMax=new Vector2(-14,0);
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        float bodyHeight=UnityEngine.UI.LayoutUtility.GetPreferredHeight(scroll.content);
        float maximum=Mathf.Min(canvas.rect.height*.75f,canvas.rect.height-48);
        panel.sizeDelta=new Vector2(width,Mathf.Min(maximum,Mathf.Max(300,headerHeight+bodyHeight+footerHeight+16)));
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }
}
