using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
public class terminalKeycap : MonoBehaviour
{
    public int lane=-1;
    public bool fire, live, guided;
    private TMP_Text label;
    private UnityEngine.UI.Image plate;
    private terminalFrame border;
    public static terminalKeycap create(Transform parent,string key,int lane=-1,bool fire=false)
    {
        var r=signalTerminal.node(parent,"keycap");signalTerminal.height(r,34);signalTerminal.width(r,34);r.sizeDelta=new Vector2(34,34);
        var c=r.gameObject.AddComponent<terminalKeycap>();c.lane=lane;c.fire=fire;c.plate=signalTerminal.image(r,new Color32(18,40,57,255));c.border=signalTerminal.frame(r,signalTerminal.rule);c.border.box=true;
        c.label=signalTerminal.text(r,"key",key,16,28,true);signalTerminal.fill(c.label.rectTransform,2);c.label.alignment=TextAlignmentOptions.Center;return c;
    }
    private void Update()
    {
        if(label==null)return;
        bool pressed=live && !accountUi.isBlocking && Keyboard.current!=null && (fire?Keyboard.current.fKey.isPressed:Keyboard.current[controlLayout.key(lane,controlLayout.leftHanded)].isPressed);
        plate.color=pressed?signalTerminal.accent:new Color32(18,40,57,255);label.color=pressed?signalTerminal.background:signalTerminal.ink;border.color=guided||pressed?signalTerminal.accent:signalTerminal.rule;
        if(fire || lane>=0)label.text=fire?"F":controlLayout.label(lane);
    }
}
