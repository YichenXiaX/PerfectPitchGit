using UnityEngine;
[ExecuteAlways]
public class terminalPanelWidth : MonoBehaviour
{
    public float maximumWidth=760;
    private void LateUpdate()
    {
        var rect=(RectTransform)transform;var parent=rect.parent as RectTransform;if(parent==null)return;
        var size=rect.sizeDelta;float width=Mathf.Min(maximumWidth,parent.rect.width-32);
        if(Mathf.Abs(size.x-width)>.1f){size.x=width;rect.sizeDelta=size;}
    }
}
