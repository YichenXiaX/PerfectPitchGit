using UnityEngine;
using System.Collections;

public class LaserBeamVFX : MonoBehaviour
{
    [Header("References")]
    public Transform firePoint;

    [Header("Beam Settings")]
    public float beamLength = 40f;
    public float beamDuration = 0.7f;
    public float coreWidth = 0.15f;
    public float glowWidth = 0.6f;
    public int segments = 30;
    public float electricNoise = 0.08f;

    [Header("Colors")]
    public Color coreColor = new Color(0.85f, 0.95f, 1f, 1f);       // bright white-blue
    public Color glowColor = new Color(0f, 0.4f, 1f, 0.5f);         // soft blue
    public Color electricColor = new Color(0.3f, 0.7f, 1f, 0.8f);   // electric blue

    [Header("Juice")]
    public bool screenShake = true;
    public float shakeAmount = 0.1f;
    public Camera mainCam;

    public void Fire()
    {
        StartCoroutine(FireSequence());
    }

    IEnumerator FireSequence()
    {
        GameObject beam = new GameObject("LaserBeam");

        // --- Create 4 layers for that chunky energy look ---
        LineRenderer glow = CreateLine(beam, "Glow", glowWidth, glowColor, true);
        LineRenderer core = CreateLine(beam, "Core", coreWidth, coreColor, true);
        LineRenderer edgeL = CreateLine(beam, "EdgeL", coreWidth * 0.3f, electricColor, false);
        LineRenderer edgeR = CreateLine(beam, "EdgeR", coreWidth * 0.3f, electricColor, false);

        Vector3 start = firePoint.position;
        Vector3 dir = firePoint.up; // shoots along firePoint's up axis
        Vector3 end = start + dir * beamLength;

        // Optional muzzle flash
        GameObject flash = CreateMuzzleFlash(start);

        // ©¤©¤ PHASE 1: Beam shoots up instantly ©¤©¤
        float shootTime = 0.04f;
        float t = 0f;
        while (t < shootTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / shootTime);
            // Ease out for snappy feel
            p = 1f - (1f - p) * (1f - p);

            Vector3 tip = Vector3.Lerp(start, end, p);
            SetBeam(core, start, tip);
            SetBeam(glow, start, tip);
            SetNoisyBeam(edgeL, start, tip, electricNoise);
            SetNoisyBeam(edgeR, start, tip, electricNoise);
            yield return null;
        }

        if (screenShake) StartCoroutine(CameraShake());

        // ©¤©¤ PHASE 2: Hold with electric flicker ©¤©¤
        float holdTime = beamDuration * 0.5f;
        t = 0f;
        while (t < holdTime)
        {
            t += Time.deltaTime;

            // Pulse the width
            float pulse = 1f + Mathf.Sin(t * 60f) * 0.1f;
            float flicker = pulse + Random.Range(-0.05f, 0.05f);

            core.widthMultiplier = coreWidth * flicker;
            glow.widthMultiplier = glowWidth * flicker;

            // Regenerate electric edges every frame
            SetNoisyBeam(edgeL, start, end, electricNoise * 2.5f);
            SetNoisyBeam(edgeR, start, end, electricNoise * 2.5f);

            yield return null;
        }

        // ©¤©¤ PHASE 3: Fade out ©¤©¤
        float fadeTime = beamDuration * 0.5f;
        t = 0f;

        float startCore = core.widthMultiplier;
        float startGlow = glow.widthMultiplier;
        float startEdge = edgeL.widthMultiplier;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(t / fadeTime);
            // Ease in for smooth disappear
            float smooth = fade * fade;

            core.widthMultiplier = startCore * smooth;
            glow.widthMultiplier = startGlow * fade; // glow lingers a bit
            edgeL.widthMultiplier = startEdge * smooth;
            edgeR.widthMultiplier = startEdge * smooth;

            SetNoisyBeam(edgeL, start, end, electricNoise * fade);
            SetNoisyBeam(edgeR, start, end, electricNoise * fade);

            // Fade colors
            SetAlpha(core, coreColor.a * fade);
            SetAlpha(glow, glowColor.a * fade);

            yield return null;
        }

        Destroy(beam);
        Destroy(flash);
    }

    // ©¤©¤©¤ LINE CREATION ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    LineRenderer CreateLine(GameObject parent, string name, float width, Color color, bool smooth)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent.transform);

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.positionCount = segments;
        lr.widthMultiplier = width;
        lr.startColor = color;
        lr.endColor = new Color(color.r, color.g, color.b, color.a * 0.3f); // fade at tip
        lr.useWorldSpace = true;
        lr.sortingOrder = name == "Glow" ? 9 : 10;

        if (smooth)
        {
            lr.numCapVertices = 5;
            lr.numCornerVertices = 5;
        }

        // Additive material for that glowy look
        Material mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;

        // Width curve ¡ª thicker at base, tapers at tip
        lr.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1.2f),
            new Keyframe(0.1f, 1f),
            new Keyframe(0.9f, 0.8f),
            new Keyframe(1f, 0.2f)
        );

        return lr;
    }

    // ©¤©¤©¤ BEAM POSITIONING ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    void SetBeam(LineRenderer lr, Vector3 from, Vector3 to)
    {
        lr.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float p = (float)i / (segments - 1);
            lr.SetPosition(i, Vector3.Lerp(from, to, p));
        }
    }

    void SetNoisyBeam(LineRenderer lr, Vector3 from, Vector3 to, float noise)
    {
        lr.positionCount = segments;
        Vector3 right = Vector3.Cross((to - from).normalized, Vector3.forward);

        for (int i = 0; i < segments; i++)
        {
            float p = (float)i / (segments - 1);
            Vector3 pos = Vector3.Lerp(from, to, p);

            // No noise at very start/end, max in middle
            if (i > 1 && i < segments - 1)
            {
                float envelope = Mathf.Sin(p * Mathf.PI); // peaks in middle
                pos += right * Random.Range(-noise, noise) * envelope;
            }

            lr.SetPosition(i, pos);
        }
    }

    void SetAlpha(LineRenderer lr, float alpha)
    {
        Color s = lr.startColor;
        Color e = lr.endColor;
        lr.startColor = new Color(s.r, s.g, s.b, alpha);
        lr.endColor = new Color(e.r, e.g, e.b, alpha * 0.3f);
    }

    // ©¤©¤©¤ MUZZLE FLASH ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    GameObject CreateMuzzleFlash(Vector3 position)
    {
        GameObject flash = new GameObject("MuzzleFlash");
        flash.transform.position = position;

        SpriteRenderer sr = flash.AddComponent<SpriteRenderer>();
        sr.sprite = CreateGlowSprite();
        sr.color = coreColor;
        sr.sortingOrder = 11;
        flash.transform.localScale = Vector3.one * 1.5f;

        StartCoroutine(AnimateMuzzleFlash(flash));
        return flash;
    }

    IEnumerator AnimateMuzzleFlash(GameObject flash)
    {
        float duration = 0.3f;
        float t = 0f;
        SpriteRenderer sr = flash.GetComponent<SpriteRenderer>();

        while (t < duration && flash != null)
        {
            t += Time.deltaTime;
            float p = t / duration;

            float scale = Mathf.Lerp(1.5f, 0f, p);
            flash.transform.localScale = Vector3.one * scale;
            sr.color = new Color(coreColor.r, coreColor.g, coreColor.b, 1f - p);

            yield return null;
        }
    }

    Sprite CreateGlowSprite()
    {
        int res = 32;
        Texture2D tex = new Texture2D(res, res);
        float center = res / 2f;

        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - (dist / center));
                alpha *= alpha; // soft falloff
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
    }

    // ©¤©¤©¤ CAMERA SHAKE ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    IEnumerator CameraShake()
    {
        if (mainCam == null) mainCam = Camera.main;
        Vector3 originalPos = mainCam.transform.position;
        float duration = 0.15f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float strength = shakeAmount * (1f - t / duration);
            mainCam.transform.position = originalPos + (Vector3)Random.insideUnitCircle * strength;
            yield return null;
        }

        mainCam.transform.position = originalPos;
    }
}