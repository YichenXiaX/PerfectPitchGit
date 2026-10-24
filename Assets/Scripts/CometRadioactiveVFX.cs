using UnityEngine;

public class CometRadioactiveVFX : MonoBehaviour
{
    [Header("Auto-Color Settings")]
    [Tooltip("If true, particle colors are derived from the sprite's dominant color")]
    public bool useAutoColor = true;

    [Header("Manual Colors (used if useAutoColor is off)")]
    public Color sparkColor = new Color(0.4f, 1f, 0.3f, 1f);
    public Color vaporColor = new Color(0f, 0.85f, 0.3f, 0.15f);
    public Color glowColor = new Color(0f, 1f, 0.4f, 1f);

    [Header("Intensity")]
    public float sparkRate = 30f;
    public float vaporRate = 10f;
    public float glowIntensity = 1.5f;
    public float glowRange = 8f;

    [Header("Color Tweaks")]
    [Range(0f, 1f)] public float sparkSaturationBoost = 0.2f;
    [Range(0f, 1f)] public float sparkBrightnessBoost = 0.3f;
    [Range(0f, 1f)] public float vaporAlpha = 0.15f;

    private ParticleSystem sparkSystem;
    private ParticleSystem vaporSystem;
    private Light glowLight;

    void Start()
    {
        if (useAutoColor)
            SampleSpriteColors();

        CreateSparkTrail();
        CreateToxicVapor();
        CreateGlow();
    }

    void SampleSpriteColors()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        if (sr == null || sr.sprite == null)
        {
            Debug.LogWarning("CometRadioactiveVFX: No SpriteRenderer found, using manual colors.");
            return;
        }

        Color dominant = GetDominantColor(sr.sprite, sr.color);

        // Convert to HSV so we can manipulate it
        Color.RGBToHSV(dominant, out float h, out float s, out float v);

        // Spark: brighter, more saturated version
        float sparkS = Mathf.Clamp01(s + sparkSaturationBoost);
        float sparkV = Mathf.Clamp01(v + sparkBrightnessBoost);
        sparkColor = Color.HSVToRGB(h, sparkS, sparkV);
        sparkColor.a = 1f;

        // Vapor: same hue, lower saturation, very transparent
        vaporColor = Color.HSVToRGB(h, Mathf.Clamp01(s * 0.8f), Mathf.Clamp01(v * 0.9f));
        vaporColor.a = vaporAlpha;

        // Glow: full brightness version of the hue
        glowColor = Color.HSVToRGB(h, Mathf.Clamp01(s * 0.9f), 1f);
        glowColor.a = 1f;
    }

    Color GetDominantColor(Sprite sprite, Color tint)
    {
        // Read the sprite's pixels from its texture rect
        Texture2D tex = sprite.texture;

        // Check if texture is readable
        if (!tex.isReadable)
        {
            Debug.LogWarning("CometRadioactiveVFX: Sprite texture not readable. Enable Read/Write in import settings. Falling back to SpriteRenderer color.");
            return tint;
        }

        Rect rect = sprite.textureRect;
        int x = Mathf.FloorToInt(rect.x);
        int y = Mathf.FloorToInt(rect.y);
        int w = Mathf.FloorToInt(rect.width);
        int h = Mathf.FloorToInt(rect.height);

        Color[] pixels = tex.GetPixels(x, y, w, h);

        // Average the non-transparent pixels
        float rSum = 0, gSum = 0, bSum = 0;
        int count = 0;

        // Sample every Nth pixel for performance on large sprites
        int step = Mathf.Max(1, pixels.Length / 256);

        for (int i = 0; i < pixels.Length; i += step)
        {
            Color p = pixels[i];
            if (p.a < 0.3f) continue; // skip transparent pixels

            rSum += p.r;
            gSum += p.g;
            bSum += p.b;
            count++;
        }

        if (count == 0) return tint;

        Color avg = new Color(rSum / count, gSum / count, bSum / count, 1f);

        // Multiply by the SpriteRenderer tint so runtime tinting is respected
        avg *= tint;
        avg.a = 1f;

        return avg;
    }

    void CreateSparkTrail()
    {
        GameObject sparkObj = new GameObject("SparkTrail");
        sparkObj.transform.SetParent(transform);
        sparkObj.transform.localPosition = Vector3.zero;

        sparkSystem = sparkObj.AddComponent<ParticleSystem>();
        sparkSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = sparkSystem.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = sparkColor;
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = sparkSystem.emission;
        emission.rateOverTime = sparkRate;

        var shape = sparkSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.3f;

        var vel = sparkSystem.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

        var inherit = sparkSystem.inheritVelocity;
        inherit.enabled = true;
        inherit.mode = ParticleSystemInheritVelocityMode.Initial;
        inherit.curve = -0.5f;

        var sizeOverLife = sparkSystem.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve shrink = new AnimationCurve();
        shrink.AddKey(0f, 1f);
        shrink.AddKey(1f, 0f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, shrink);

        // Fade color: bright spark -> dimmer version
        Color dimSpark = sparkColor * 0.5f;
        dimSpark.a = 1f;

        var colorOverLife = sparkSystem.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient sparkGrad = new Gradient();
        sparkGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(sparkColor, 0f),
                new GradientColorKey(dimSpark, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = sparkGrad;

        var renderer = sparkObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateParticleMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        sparkSystem.Play();
    }

    void CreateToxicVapor()
    {
        GameObject vaporObj = new GameObject("ToxicVapor");
        vaporObj.transform.SetParent(transform);
        vaporObj.transform.localPosition = Vector3.zero;

        vaporSystem = vaporObj.AddComponent<ParticleSystem>();
        vaporSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = vaporSystem.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startColor = vaporColor;
        main.maxParticles = 100;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.02f;

        var emission = vaporSystem.emission;
        emission.rateOverTime = vaporRate;

        var shape = vaporSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        var sizeOverLife = vaporSystem.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve grow = new AnimationCurve();
        grow.AddKey(0f, 0.5f);
        grow.AddKey(0.3f, 1f);
        grow.AddKey(1f, 1.5f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, grow);

        var noise = vaporSystem.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.5f;
        noise.damping = true;

        // Fade color: vapor -> dimmer vapor
        Color dimVapor = vaporColor * 0.6f;
        dimVapor.a = 1f;

        var colorOverLife = vaporSystem.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient vaporGrad = new Gradient();
        vaporGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(vaporColor, 0f),
                new GradientColorKey(dimVapor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(vaporAlpha, 0.2f),
                new GradientAlphaKey(vaporAlpha * 0.7f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = vaporGrad;

        var renderer = vaporObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateParticleMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        vaporSystem.Play();
    }

    void CreateGlow()
    {
        GameObject glowObj = new GameObject("RadioactiveGlow");
        glowObj.transform.SetParent(transform);
        glowObj.transform.localPosition = Vector3.zero;

        glowLight = glowObj.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.intensity = glowIntensity;
        glowLight.range = glowRange;
        glowLight.shadows = LightShadows.None;
    }

    void Update()
    {
        if (glowLight != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.25f;
            glowLight.intensity = glowIntensity * pulse;
        }
    }

    Material CreateParticleMaterial()
    {
        Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetFloat("_Mode", 1);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetTexture("_MainTex", CreateSoftCircleTexture());
        return mat;
    }

    Texture2D CreateSoftCircleTexture()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha *= alpha;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    public void StopEffects()
    {
        if (sparkSystem != null) sparkSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (vaporSystem != null) vaporSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (glowLight != null) glowLight.enabled = false;
    }
}