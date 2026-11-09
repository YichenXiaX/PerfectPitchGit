using UnityEngine;

// One continuous field for the waiting view, launch and gameplay. Visual randomness
// is independent of UnityEngine.Random, which the pitch/gameplay systems consume.
public class starfieldBackground : MonoBehaviour
{
    public float areaWidth = 20f;
    public float areaHeight = 12f;
    public bool presentationPaused { get; set; }
    public int starCount => particles == null ? 0 : particles.Length;
    public float speedMultiplier { get; private set; } = 1;
    public float streakAmount { get; private set; }
    private ParticleSystem system;
    private ParticleSystem.Particle[] particles;
    private float[] speeds, sizes;
    private Rect field;
    private bool configured, presentation;
    private readonly System.Random random = new System.Random(19371);
    private Texture2D texture;
    private Material material;

    public void prepare(Rect coverage)
    {
        if (system != null) return;
        field = coverage; configured = true; initialize();
    }
    public void setMotion(float speed, float streak, bool usePresentationTime)
    {
        speedMultiplier = Mathf.Max(0, speed); streakAmount = Mathf.Clamp01(streak); presentation = usePresentationTime;
    }
    private void Start() { initialize(); }
    private float range(float min, float max) { return Mathf.Lerp(min, max, (float)random.NextDouble()); }
    private void initialize()
    {
        if (system != null) return;
        if (!configured) field = new Rect(-areaWidth / 2, -areaHeight / 2, areaWidth, areaHeight);
        system = gameObject.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = true; main.playOnAwake = false; main.startSpeed = 0;
        main.startLifetime = 999999; main.simulationSpeed = 0;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = system.emission; emission.enabled = false;
        var shape = system.shape; shape.enabled = false;
        int count = Mathf.Clamp(Mathf.RoundToInt(235 * field.width * field.height / 240), 235, 1600);
        main.maxParticles = count;
        particles = new ParticleSystem.Particle[count]; speeds = new float[count]; sizes = new float[count];
        texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "slipstreamStar", filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float d = new Vector2((x + .5f - 16) / 16, (y + .5f - 16) / 16).magnitude;
            texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - d), 3)));
        }
        texture.Apply();
        material = new Material(Resources.Load<Material>("Launch/launchUnlit")) { name = "slipstreamStars", mainTexture = texture };
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.material = material; renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.sortingOrder = -50;
        for (int i = 0; i < count; i++)
        {
            float layer = range(0, 235);
            speeds[i] = layer < 160 ? range(.2f, .8f) : layer < 220 ? range(.8f, 2) : range(2, 4);
            sizes[i] = layer < 160 ? range(.018f, .04f) : layer < 220 ? range(.04f, .065f) : range(.065f, .10f);
            var tint = Color.Lerp(new Color(.75f, .85f, 1), new Color(1, .95f, .8f), range(0, 1));
            tint.a = layer < 160 ? range(.2f, .42f) : layer < 220 ? range(.4f, .65f) : range(.7f, 1);
            particles[i] = new ParticleSystem.Particle {
                position = new Vector3(range(field.xMin, field.xMax), range(field.yMin, field.yMax), 0),
                startColor = tint, startLifetime = 999999, remainingLifetime = 999999,
                startSize3D = Vector3.one * sizes[i], randomSeed = (uint)(i + 1)
            };
        }
        system.SetParticles(particles, count); system.Play();
    }
    private void Update()
    {
        if (system == null || presentationPaused) return;
        float dt = Mathf.Min(presentation ? Time.unscaledDeltaTime : Time.deltaTime, .1f);
        for (int i = 0; i < particles.Length; i++)
        {
            var p = particles[i].position; p.y -= speeds[i] * speedMultiplier * dt;
            if (p.y < field.yMin) { p.y += field.height; p.x = range(field.xMin, field.xMax); }
            particles[i].position = p;
            particles[i].startSize3D = new Vector3(sizes[i], sizes[i] + streakAmount * speeds[i] * .65f, sizes[i]);
        }
        system.SetParticles(particles, particles.Length);
    }
    private void OnDestroy() { if (material != null) Destroy(material); if (texture != null) Destroy(texture); }
}
