using UnityEngine;
using System.Collections.Generic;

// Aft exhaust follows the existing hull transform. Its sprite, tint and floating
// animation are unchanged; these renderers sit behind the hull.
public class heliosEngineGlow : MonoBehaviour
{
    public bool paused { get; set; }
    private Transform[] plumes;
    private Mesh mesh;
    private Material material;
    private float power = .2f, clock;
    public void initialize(SpriteRenderer hull)
    {
        material = new Material(Resources.Load<Material>("Launch/launchUnlit")) { name = "heliosExhaust" };
        mesh = createPlume(); plumes = new Transform[2]; var b = hull.sprite.bounds;
        for (int i = 0; i < 2; i++)
        {
            var node = new GameObject("aftEngine" + i, typeof(MeshFilter), typeof(MeshRenderer));
            node.transform.SetParent(hull.transform, false);
            node.transform.localPosition = new Vector3(b.min.x + b.size.x * (i == 0 ? .408f : .592f), b.min.y + b.size.y * .018f, 0);
            node.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.sortingLayerID = hull.sortingLayerID; renderer.sortingOrder = hull.sortingOrder - 1;
            plumes[i] = node.transform;
        }
    }
    public void setPower(float value) { power = Mathf.Clamp01(value); }
    private void Update()
    {
        if (plumes == null || paused) return;
        clock += Time.unscaledDeltaTime;
        float pulse = 1 + .045f * Mathf.Sin(clock * 3) + power * .035f * Mathf.Sin(clock * 37);
        foreach (var plume in plumes) plume.localScale = new Vector3((.74f + power * .36f) * pulse, (.28f + power * 1.5f) * pulse, 1);
    }
    private Mesh createPlume()
    {
        var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
        float[] widths = { .32f, .22f, .12f, .055f }, lengths = { .74f, .64f, .49f, .26f };
        Color[] tones = { new Color(.04f,.5f,1,.13f), new Color(.08f,.85f,1,.27f), new Color(.45f,.95f,1,.6f), new Color(.9f,1,1,.95f) };
        for (int layer = 0; layer < 4; layer++)
        {
            int start = vertices.Count; Color color = tones[layer];
            vertices.Add(new Vector3(0, -.06f, 0)); colors.Add(color);
            for (int j = 0; j <= 32; j++)
            {
                float angle = j * Mathf.PI * 2 / 32;
                vertices.Add(new Vector3(Mathf.Cos(angle) * widths[layer], -.18f + Mathf.Sin(angle) * lengths[layer], 0));
                colors.Add(new Color(color.r, color.g, color.b, 0));
                if (j > 0) { triangles.Add(start); triangles.Add(start + j); triangles.Add(start + j + 1); }
            }
        }
        var result = new Mesh { name = "heliosExhaustMesh" };
        result.SetVertices(vertices); result.SetColors(colors); result.SetTriangles(triangles, 0); result.RecalculateBounds(); return result;
    }
    private void OnDestroy()
    {
        if (plumes != null) foreach (var plume in plumes) if (plume != null) Destroy(plume.gameObject);
        if (mesh != null) Destroy(mesh); if (material != null) Destroy(material);
    }
}
