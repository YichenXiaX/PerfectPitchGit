using UnityEngine;
using UnityEngine.EventSystems;
public class terminalFocus : MonoBehaviour,ISelectHandler,IDeselectHandler
{
    private terminalFrame outline;
    public void OnSelect(BaseEventData data)
    {if(outline==null){var r=signalTerminal.node(transform,"keyboardFocus");signalTerminal.fill(r,-3);outline=r.gameObject.AddComponent<terminalFrame>();outline.box=true;outline.thickness=2;outline.color=signalTerminal.accent;outline.raycastTarget=false;}outline.gameObject.SetActive(true);}
    private void OnDisable(){if(outline!=null)outline.gameObject.SetActive(false);}
    public void OnDeselect(BaseEventData data){if(outline!=null)outline.gameObject.SetActive(false);}
}
