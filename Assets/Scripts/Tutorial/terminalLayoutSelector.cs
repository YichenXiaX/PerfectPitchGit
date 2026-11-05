using UnityEngine;
using TMPro;
public class terminalLayoutSelector : MonoBehaviour
{
    private UnityEngine.UI.Button right,left;
    private TMP_Text rightState,leftState;
    public static terminalLayoutSelector create(Transform parent)
    {
        var r=signalTerminal.node(parent,"layoutSelector");signalTerminal.height(r,90);signalTerminal.row(r,10,true);var view=r.gameObject.AddComponent<terminalLayoutSelector>();
        view.right=view.choice(r,false,out view.rightState);view.left=view.choice(r,true,out view.leftState);view.refresh();return view;
    }
    private UnityEngine.UI.Button choice(Transform parent,bool leftHand,out TMP_Text state)
    {
        var r=signalTerminal.node(parent,leftHand?"leftLayout":"rightLayout");signalTerminal.height(r,90);var image=signalTerminal.image(r,Color.white,true);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;b.onClick.AddListener(()=>controlLayout.select(leftHand));signalTerminal.styleButton(b);
        var title=signalTerminal.text(r,"title",leftHand?"LEFT HAND":"RIGHT HAND · RECOMMENDED",11,16,true);title.color=signalTerminal.secondary;title.rectTransform.anchorMin=title.rectTransform.anchorMax=new Vector2(0,1);title.rectTransform.pivot=new Vector2(0,1);title.rectTransform.anchoredPosition=new Vector2(12,-9);title.rectTransform.sizeDelta=new Vector2(260,18);
        var keys=signalTerminal.node(r,"keys");keys.anchorMin=new Vector2(0,1);keys.anchorMax=new Vector2(1,1);keys.pivot=new Vector2(.5f,1);keys.offsetMin=new Vector2(12,-66);keys.offsetMax=new Vector2(-12,-32);signalTerminal.row(keys,6);
        for(int i=0;i<4;i++)terminalKeycap.create(keys,controlLayout.label(i,leftHand));
        state=signalTerminal.text(r,"selection","",10,14,true);state.color=signalTerminal.accent;var sr=state.rectTransform;sr.anchorMin=new Vector2(0,0);sr.anchorMax=new Vector2(1,0);sr.pivot=new Vector2(.5f,0);sr.offsetMin=new Vector2(12,4);sr.offsetMax=new Vector2(-12,18);
        return b;
    }
    private void OnEnable(){controlLayout.changed+=refresh;refresh();}
    private void OnDisable(){controlLayout.changed-=refresh;}
    private void refresh()
    {if(right==null)return;paint(right,!controlLayout.leftHanded);paint(left,controlLayout.leftHanded);rightState.text=controlLayout.leftHanded?"":"SELECTED";leftState.text=controlLayout.leftHanded?"SELECTED":"";}
    private void paint(UnityEngine.UI.Button button,bool selected){var c=button.colors;c.normalColor=selected?new Color32(27,61,82,255):new Color32(13,29,45,255);button.colors=c;button.transform.Find("buttonBorder").GetComponent<terminalFrame>().color=selected?signalTerminal.accent:signalTerminal.rule;}
    public void interactable(bool value){right.interactable=left.interactable=value;}
}
