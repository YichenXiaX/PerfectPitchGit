using UnityEngine;
[RequireComponent(typeof(CanvasRenderer))]
public class terminalFrame : UnityEngine.UI.MaskableGraphic
{
    public bool box, dashed, vertical;
    public float thickness=1;
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;
        if(box){quad(vh,r.xMin,r.yMin,r.width,thickness);quad(vh,r.xMin,r.yMax-thickness,r.width,thickness);quad(vh,r.xMin,r.yMin,thickness,r.height);quad(vh,r.xMax-thickness,r.yMin,thickness,r.height);}
        else if(vertical)quad(vh,r.xMin,r.yMin,thickness,r.height);
        else if(dashed){for(float x=r.xMin;x<r.xMax;x+=7)quad(vh,x,r.yMin,Mathf.Min(4,r.xMax-x),thickness);}
        else quad(vh,r.xMin,r.yMin,r.width,thickness);
    }
    private void quad(UnityEngine.UI.VertexHelper vh,float x,float y,float w,float h)
    {int n=vh.currentVertCount;vh.AddVert(new Vector3(x,y),color,Vector2.zero);vh.AddVert(new Vector3(x+w,y),color,Vector2.zero);vh.AddVert(new Vector3(x+w,y+h),color,Vector2.zero);vh.AddVert(new Vector3(x,y+h),color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
}
