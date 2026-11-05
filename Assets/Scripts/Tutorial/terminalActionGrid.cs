using UnityEngine;
[ExecuteAlways]
public class terminalActionGrid : MonoBehaviour
{
    private void OnRectTransformDimensionsChange(){resize();}
    private void OnEnable(){resize();}
    private void resize()
    {
        var grid=GetComponent<UnityEngine.UI.GridLayoutGroup>();if(grid==null)return;
        grid.cellSize=new Vector2(Mathf.Max(100,(((RectTransform)transform).rect.width-grid.spacing.x)/2),44);
    }
}
