using UnityEngine;
using System.Linq;

/// <summary>
/// Spawns a multi-layered explosion colored by a sprite.
/// Usage:  cometExplosion.spawn(transform.position, mySprite);
/// 
/// NOTE: The comet sprite texture needs Read/Write enabled in its import settings
///       so we can sample pixel colors. If it's not readable, falls back to
///       a warm orange/yellow palette.
/// </summary>
public static class cometExplosion
{
    static Material additiveMat;
    static Material softMat;

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  PUBLIC API
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    public static void spawn(Vector3 position, Sprite cometSprite, float scale = 1f)
    {
        extractColors(cometSprite, out Color bright, out Color mid, out Color dark);

        GameObject root = new GameObject("CometExplosion");
        root.transform.position = position;

        createFlash(root.transform, bright, scale);
        createSparks(root.transform, bright, mid, scale);
        createEmbers(root.transform, mid, dark, scale);
        createRing(root.transform, bright, scale);

        Object.Destroy(root, 2.5f);
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  FLASH ¡ª big bright pop, instant fade
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    static void createFlash(Transform parent, Color color, float scale)
    {
        var ps = makeSystem(parent, "Flash");
        var main = ps.main;
        main.duration = 0.1f;
        main.startLifetime = 0.15f;
        main.startSpeed = 0f;
        main.startSize = 2.5f * scale;
        main.startColor = withAlpha(color, 0.9f);
        main.maxParticles = 1;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        // Grow then vanish
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.5f)
        ));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = makeGradient(
            new[] { (Color.white, 0f), (color, 1f) },
            new[] { (1f, 0f), (0f, 1f) }
        );

        setRenderer(ps, additive: true, sortOrder: 12);
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  SPARKS ¡ª fast outward burst
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    static void createSparks(Transform parent, Color bright, Color mid, float scale)
    {
        var ps = makeSystem(parent, "Sparks");
        var main = ps.main;
        main.duration = 0.05f;
        main.startLifetime = minMax(0.25f, 0.6f);
        main.startSpeed = minMax(5f * scale, 12f * scale);
        main.startSize = minMax(0.08f * scale, 0.3f * scale);
        main.startColor = new ParticleSystem.MinMaxGradient(bright, mid);
        main.gravityModifier = 0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25, 40) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f * scale;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = makeGradient(
            new[] { (bright, 0f), (mid, 0.4f), (mid * 0.4f, 1f) },
            new[] { (1f, 0f), (0.9f, 0.2f), (0f, 1f) }
        );

        setRenderer(ps, additive: true, sortOrder: 11);
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  EMBERS ¡ª slow drifting remnants
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    static void createEmbers(Transform parent, Color mid, Color dark, float scale)
    {
        var ps = makeSystem(parent, "Embers");
        var main = ps.main;
        main.duration = 0.1f;
        main.startLifetime = minMax(0.5f, 1.5f);
        main.startSpeed = minMax(1f * scale, 3f * scale);
        main.startSize = minMax(0.04f * scale, 0.12f * scale);
        main.startColor = new ParticleSystem.MinMaxGradient(mid, dark);
        main.gravityModifier = -0.05f; // slight float up
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 10, 20) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f * scale;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = makeGradient(
            new[] { (mid, 0f), (dark, 1f) },
            new[] { (0.8f, 0f), (0.6f, 0.5f), (0f, 1f) }
        );

        setRenderer(ps, additive: true, sortOrder: 10);
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  RING ¡ª expanding shockwave circle
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    static void createRing(Transform parent, Color color, float scale)
    {
        var ps = makeSystem(parent, "Ring");
        var main = ps.main;
        main.duration = 0.05f;
        main.startLifetime = 0.3f;
        main.startSpeed = 0f;
        main.startSize = 0.5f * scale;
        main.startColor = withAlpha(color, 0.6f);
        main.maxParticles = 1;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.2f), new Keyframe(1f, 4f)
        ));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = makeGradient(
            new[] { (color, 0f), (color, 1f) },
            new[] { (0.7f, 0f), (0f, 1f) }
        );

        setRenderer(ps, additive: true, sortOrder: 9);
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  COLOR EXTRACTION
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    static void extractColors(Sprite sprite, out Color bright, out Color mid, out Color dark)
    {
        // Defaults if sprite unreadable
        bright = new Color(1f, 0.85f, 0.3f);
        mid = new Color(1f, 0.5f, 0.15f);
        dark = new Color(0.6f, 0.2f, 0.05f);

        if (sprite == null || sprite.texture == null) return;

        try
        {
            Texture2D tex = sprite.texture;
            Rect r = sprite.textureRect;

            // Sample a grid of pixels (max 32¡Á32 for perf)
            int sw = Mathf.Min((int)r.width, 32);
            int sh = Mathf.Min((int)r.height, 32);
            float stepX = r.width / sw;
            float stepY = r.height / sh;

            Color[] samples = new Color[sw * sh];
            int count = 0;
            for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                {
                    int px = (int)(r.x + x * stepX);
                    int py = (int)(r.y + y * stepY);
                    Color c = tex.GetPixel(px, py);
                    if (c.a > 0.3f)
                        samples[count++] = c;
                }

            if (count == 0) return;

            // Sort by luminance
            var valid = samples.Take(count)
                .OrderByDescending(c => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f)
                .ToArray();

            bright = valid[0];
            mid = valid[valid.Length / 3];
            dark = valid[Mathf.Min(valid.Length - 1, valid.Length * 2 / 3)];

            bright.a = mid.a = dark.a = 1f;

            // Boost saturation a bit so explosions pop
            bright = boostSaturation(bright, 1.3f);
            mid = boostSaturation(mid, 1.2f);
        }
        catch
        {
            // Texture not Read/Write ¡ª keep defaults
        }
    }

    static Color boostSaturation(Color c, float factor)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        s = Mathf.Clamp01(s * factor);
        v = Mathf.Clamp01(v * 1.1f);
        return Color.HSVToRGB(h, s, v);
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  UTILITIES
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    static ParticleSystem makeSystem(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        return ps;
    }

    static void setRenderer(ParticleSystem ps, bool additive, int sortOrder)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = getMaterial(additive);
        r.sortingOrder = sortOrder;
        r.renderMode = ParticleSystemRenderMode.Billboard;
    }

    static Material getMaterial(bool additive)
    {
        if (additive)
        {
            if (additiveMat == null)
            {
                // Try the URP particle shader first, fall back to built-in
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default");

                additiveMat = new Material(sh);
                additiveMat.SetFloat("_Surface", 1); // Transparent
                additiveMat.SetFloat("_Blend", 1);   // Additive

                // Built-in pipeline blend modes
                additiveMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                additiveMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }
            return additiveMat;
        }
        else
        {
            if (softMat == null)
            {
                Shader sh = Shader.Find("Sprites/Default");
                softMat = new Material(sh);
            }
            return softMat;
        }
    }

    static ParticleSystem.MinMaxCurve minMax(float a, float b) =>
        new ParticleSystem.MinMaxCurve(a, b);

    static Color withAlpha(Color c, float a) =>
        new Color(c.r, c.g, c.b, a);

    static Gradient makeGradient(
        (Color col, float time)[] colors,
        (float alpha, float time)[] alphas)
    {
        var g = new Gradient();
        g.SetKeys(
            colors.Select(c => new GradientColorKey(c.col, c.time)).ToArray(),
            alphas.Select(a => new GradientAlphaKey(a.alpha, a.time)).ToArray()
        );
        return g;
    }

    // convenience overload: scale defaults to size relative to sprite
    public static void spawn(Vector3 position, SpriteRenderer sr)
    {
        float scale = sr != null ? Mathf.Max(sr.bounds.size.x, sr.bounds.size.y) : 1f;
        spawn(position, sr != null ? sr.sprite : null, scale);
    }
}