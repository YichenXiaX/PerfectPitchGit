using UnityEngine;
[RequireComponent(typeof(CanvasRenderer))]
public class tutorialPitchConnections : UnityEngine.UI.Graphic
{
    public frequencyManager source;
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();if(source==null)return;var r=rectTransform.rect;
        // White keys are equally wide; pitch positions below are logarithmic.
        float[] notes={523.251f,493.883f,440,391.995f,349.228f,329.628f,293.665f,261.626f};
        float high=source.quadrants[0].maxFrequency,low=source.quadrants[3].minFrequency;
        for(int i=0;i<notes.Length;i++)
        {
            float x=Mathf.Log(high/notes[i])/Mathf.Log(high/low);if(x<0 || x>1)continue;
            Vector2 a=new Vector2(r.xMin+r.width*(i+.5f)/8,r.yMax),b=new Vector2(r.xMin+r.width*x,r.yMin);
            var normal=new Vector2(-(b-a).y,(b-a).x).normalized*.5f;int start=mesh.currentVertCount;
            foreach(var point in new[]{a-normal,a+normal,b+normal,b-normal})mesh.AddVert(point,color,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
