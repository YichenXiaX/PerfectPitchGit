using UnityEngine;

public class StarfieldBackground : MonoBehaviour
{
    [Header("Area (match camera view + buffer)")]
    public float areaWidth = 20f;
    public float areaHeight = 12f;


    ParticleSystem ps;
    float spawnTimer;
    ParticleSystem.Particle[] buf;

    // ©¤©¤ layer definitions ©¤©¤
    struct StarLayer
    {
        public int count;
        public float minSpeed, maxSpeed;
        public float minSize, maxSize;
        public float minAlpha, maxAlpha;
    }

    readonly StarLayer[] layers = new[]
    {
        // far background ¨C tons of tiny faint dots, barely moving
        new StarLayer { count = 160, minSpeed = 0.2f, maxSpeed = 0.8f,
                        minSize = 0.01f, maxSize = 0.03f,
                        minAlpha = 0.15f, maxAlpha = 0.35f },

        // mid layer ¨C moderate
        new StarLayer { count = 60, minSpeed = 0.8f, maxSpeed = 2f,
                        minSize = 0.03f, maxSize = 0.05f,
                        minAlpha = 0.3f, maxAlpha = 0.6f },

        // near layer ¨C few bright stars
        new StarLayer { count = 15, minSpeed = 2f, maxSpeed = 4f,
                        minSize = 0.05f, maxSize = 0.09f,
                        minAlpha = 0.7f, maxAlpha = 1f },
    };

    int totalCount;

    void Start()
    {
        foreach (var l in layers) totalCount += l.count;
        BuildSystem();
        Seed();
    }

    void BuildSystem()
    {
        ps = gameObject.AddComponent<ParticleSystem>();
        var psr = GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startLifetime = 9999f;
        main.maxParticles = totalCount * 3;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var vel = ps.velocityOverLifetime; vel.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = false;

        // soft-dot texture
        int r = 32;
        float h = r / 2f;
        var tex = new Texture2D(r, r, TextureFormat.RGBA32, false);
        for (int y = 0; y < r; y++)
            for (int x = 0; x < r; x++)
            {
                float dx = (x + .5f - h) / h;
                float dy = (y + .5f - h) / h;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * a;   // sharper falloff = more star-like
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;

        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
              ?? Shader.Find("Particles/Standard Unlit")
              ?? Shader.Find("Sprites/Default");
        var mat = new Material(sh) { mainTexture = tex };

        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sortingOrder = -50;
        psr.material = mat;

        buf = new ParticleSystem.Particle[main.maxParticles];
        ps.Play();
    }

    void EmitStar(StarLayer layer, float x, float y)
    {
        float speed = Random.Range(layer.minSpeed, layer.maxSpeed);
        float alpha = Random.Range(layer.minAlpha, layer.maxAlpha);

        // slight warm/cool tint
        Color tint = Color.Lerp(
            new Color(0.75f, 0.85f, 1f),   // cool blue-white
            new Color(1f, 0.95f, 0.8f),    // warm yellow-white
            Random.value);
        tint.a = alpha;

        var ep = new ParticleSystem.EmitParams();
        ep.position = new Vector3(x, y, 0f);
        ep.velocity = new Vector3(0f, -speed, 0f);
        ep.startSize = Random.Range(layer.minSize, layer.maxSize);
        ep.startLifetime = 9999f;
        ep.startColor = (Color32)tint;
        ps.Emit(ep, 1);
    }

    void Seed()
    {
        foreach (var layer in layers)
            for (int i = 0; i < layer.count; i++)
                EmitStar(layer,
                    Random.Range(-areaWidth / 2f, areaWidth / 2f),
                    Random.Range(-areaHeight / 2f, areaHeight / 2f));
    }

    void Update()
    {
        // ©¤©¤ spawn from top ©¤©¤
        float topY = areaHeight / 2f + 0.5f;

        foreach (var layer in layers)
        {
            float avgSpeed = (layer.minSpeed + layer.maxSpeed) * 0.5f;
            float rate = layer.count / (areaHeight / avgSpeed);
            float spawns = Time.deltaTime * rate;

            // fractional spawning
            if (Random.value < (spawns % 1f)) spawns = Mathf.CeilToInt(spawns);
            else spawns = Mathf.FloorToInt(spawns);

            for (int i = 0; i < (int)spawns; i++)
                EmitStar(layer,
                    Random.Range(-areaWidth / 2f, areaWidth / 2f),
                    topY);
        }

        // ©¤©¤ kill below screen ©¤©¤
        int count = ps.GetParticles(buf);
        float bottomY = -areaHeight / 2f - 1f;
        bool dirty = false;

        for (int i = 0; i < count; i++)
        {
            if (buf[i].position.y < bottomY)
            {
                buf[i].remainingLifetime = 0f;
                dirty = true;
            }
        }

        if (dirty)
            ps.SetParticles(buf, count);
    }
}