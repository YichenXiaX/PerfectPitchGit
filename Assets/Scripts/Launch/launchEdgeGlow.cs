using UnityEngine;

// Separate canvas below the existing HUD, so the edge pulse cannot tint its text.
[RequireComponent(typeof(CanvasRenderer))]
public class launchEdgeGlow : UnityEngine.UI.MaskableGraphic
{
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear(); var r = rectTransform.rect; float width = Mathf.Min(r.width * .13f, 160);
        strip(mesh, r.xMin, r.yMin, width, r.height, true);
        strip(mesh, r.xMax - width, r.yMin, width, r.height, false);
    }
    private void strip(UnityEngine.UI.VertexHelper mesh, float x, float y, float w, float h, bool left)
    {
        int start = mesh.currentVertCount; var clear = new Color(color.r, color.g, color.b, 0);
        mesh.AddVert(new Vector3(x,y), left ? color : clear, Vector2.zero);
        mesh.AddVert(new Vector3(x,y+h), left ? color : clear, Vector2.zero);
        mesh.AddVert(new Vector3(x+w,y+h), left ? clear : color, Vector2.zero);
        mesh.AddVert(new Vector3(x+w,y), left ? clear : color, Vector2.zero);
        mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start,start+2,start+3);
    }
}
