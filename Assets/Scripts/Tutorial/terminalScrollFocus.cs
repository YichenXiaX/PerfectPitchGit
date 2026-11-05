using UnityEngine;
using UnityEngine.EventSystems;
public class terminalScrollFocus : MonoBehaviour
{
    private GameObject previous;
    private Vector2 previousViewportSize;
    private void LateUpdate()
    {
        var selected=EventSystem.current?.currentSelectedGameObject;
        var scroll=GetComponent<UnityEngine.UI.ScrollRect>();
        if(selected==null){previous=null;return;}
        var viewportSize=scroll.viewport.rect.size;
        if(selected==previous && viewportSize==previousViewportSize)return;
        previous=selected;previousViewportSize=viewportSize;
        if(!selected.transform.IsChildOf(scroll.content))return;
        Canvas.ForceUpdateCanvases();
        var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,selected.transform);
        float delta=bounds.min.y<scroll.viewport.rect.yMin?scroll.viewport.rect.yMin-bounds.min.y+12:bounds.max.y>scroll.viewport.rect.yMax?scroll.viewport.rect.yMax-bounds.max.y-12:0;
        scroll.content.anchoredPosition+=new Vector2(0,delta);scroll.StopMovement();
    }
}

