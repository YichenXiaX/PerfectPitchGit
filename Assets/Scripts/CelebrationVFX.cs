using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CelebrationVFX : MonoBehaviour
{
    [Header("References")]
    public Canvas canvas; // your main UI canvas

    [Header("Colors")]
    public Color primaryColor = new Color(0f, 1f, 0.85f);   // cyan/teal
    public Color secondaryColor = new Color(0.9f, 0.6f, 1f); // soft purple
    public Color flashColor = new Color(1f, 1f, 1f, 0.3f);   // white flash

    [Header("Settings")]
    public int sparkCount = 30;
    public int noteCount = 6;
    public float burstForce = 400f;
    public float sparkLifetime = 0.8f;
    public float flashDuration = 0.15f;
    public float ringDuration = 0.5f;

    private GameObject flashPanel;

    void Start()
    {
        // Create persistent screen flash overlay
        flashPanel = new GameObject("ScreenFlash");
        flashPanel.transform.SetParent(canvas.transform, false);
        Image img = flashPanel.AddComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = false;
        RectTransform rt = flashPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        flashPanel.SetActive(false);
    }

    /// <summary>
    /// Call this when the player gets a correct prediction!
    /// Pass in the screen position (e.g. comet position or ship position)
    /// </summary>
    public void Play(Vector2 screenPosition)
    {
        StartCoroutine(ScreenFlash());
        StartCoroutine(SpawnSparks(screenPosition));
        StartCoroutine(SpawnExpandingRing(screenPosition));
        StartCoroutine(SpawnNotes(screenPosition));
    }

    // ─── SCREEN FLASH ───────────────────────────────────
    IEnumerator ScreenFlash()
    {
        flashPanel.SetActive(true);
        Image img = flashPanel.GetComponent<Image>();
        img.color = flashColor;

        float t = 0f;
        while (t < flashDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(flashColor.a, 0f, t / flashDuration);
            img.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
            yield return null;
        }

        img.color = Color.clear;
        flashPanel.SetActive(false);
    }

    // ─── SPARKS ──────────────────────────────────────────
    IEnumerator SpawnSparks(Vector2 origin)
    {
        for (int i = 0; i < sparkCount; i++)
        {
            GameObject spark = new GameObject("Spark");
            spark.transform.SetParent(canvas.transform, false);

            Image img = spark.AddComponent<Image>();
            img.raycastTarget = false;

            // Alternate colors with some randomness
            Color col = Color.Lerp(primaryColor, secondaryColor, Random.value);
            col.a = 1f;
            img.color = col;

            RectTransform rt = spark.GetComponent<RectTransform>();
            rt.anchoredPosition = origin;
            float size = Random.Range(4f, 12f);
            rt.sizeDelta = new Vector2(size, size);

            // Random direction
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float force = Random.Range(burstForce * 0.3f, burstForce);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * force;

            StartCoroutine(AnimateSpark(spark, rt, img, velocity));
        }
        yield return null;
    }

    IEnumerator AnimateSpark(GameObject spark, RectTransform rt, Image img, Vector2 velocity)
    {
        float t = 0f;
        float lifetime = sparkLifetime * Random.Range(0.7f, 1.3f);
        Vector2 startPos = rt.anchoredPosition;
        Color startColor = img.color;

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float progress = t / lifetime;

            // Move with deceleration + slight gravity
            velocity *= (1f - Time.deltaTime * 3f);
            velocity.y -= 200f * Time.deltaTime; // gravity
            rt.anchoredPosition += velocity * Time.deltaTime;

            // Shrink and fade
            float scale = Mathf.Lerp(1f, 0f, progress * progress);
            rt.localScale = Vector3.one * scale;
            img.color = new Color(startColor.r, startColor.g, startColor.b, 1f - progress);

            yield return null;
        }

        Destroy(spark);
    }

    // ─── EXPANDING RING ──────────────────────────────────
    IEnumerator SpawnExpandingRing(Vector2 origin)
    {
        GameObject ring = new GameObject("Ring");
        ring.transform.SetParent(canvas.transform, false);

        // Use an outline circle — we fake it with a rounded Image
        Image img = ring.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = primaryColor;

        // Make it a circle using Unity's built-in sprite
        img.sprite = MakeCircleSprite();
        img.type = Image.Type.Simple;
        img.preserveAspect = true;

        RectTransform rt = ring.GetComponent<RectTransform>();
        rt.anchoredPosition = origin;
        rt.sizeDelta = new Vector2(10f, 10f);

        // Add outline to make it ring-like
        Outline outline = ring.AddComponent<Outline>();
        outline.effectColor = secondaryColor;
        outline.effectDistance = new Vector2(3, 3);

        float t = 0f;
        while (t < ringDuration)
        {
            t += Time.deltaTime;
            float progress = t / ringDuration;

            // Expand
            float size = Mathf.Lerp(10f, 500f, Mathf.SmoothStep(0f, 1f, progress));
            rt.sizeDelta = new Vector2(size, size);

            // Fade out
            float alpha = Mathf.Lerp(0.8f, 0f, progress);
            img.color = new Color(primaryColor.r, primaryColor.g, primaryColor.b, alpha);

            yield return null;
        }

        Destroy(ring);
    }

    // ─── FLOATING MUSIC NOTES ────────────────────────────
    IEnumerator SpawnNotes(Vector2 origin)
    {
        string[] notes = { "♪", "♫", "♬", "♩", "🎵" };

        for (int i = 0; i < noteCount; i++)
        {
            GameObject note = new GameObject("Note");
            note.transform.SetParent(canvas.transform, false);

            Text txt = note.AddComponent<Text>();
            txt.text = notes[Random.Range(0, notes.Length)];
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = Random.Range(24, 48);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;

            Color col = Color.Lerp(primaryColor, secondaryColor, Random.value);
            txt.color = col;

            RectTransform rt = note.GetComponent<RectTransform>();
            rt.anchoredPosition = origin + Random.insideUnitCircle * 50f;
            rt.sizeDelta = new Vector2(60, 60);

            StartCoroutine(AnimateNote(note, rt, txt));

            yield return new WaitForSeconds(0.05f);
        }
    }

    IEnumerator AnimateNote(GameObject note, RectTransform rt, Text txt)
    {
        float lifetime = Random.Range(0.8f, 1.4f);
        float t = 0f;
        Vector2 startPos = rt.anchoredPosition;
        float driftX = Random.Range(-80f, 80f);
        Color startColor = txt.color;
        float rotDir = Random.Range(-1f, 1f);

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float progress = t / lifetime;

            // Float upward with slight drift
            rt.anchoredPosition = startPos + new Vector2(
                driftX * progress,
                150f * progress
            );

            // Wobble rotation
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 8f) * 20f * rotDir);

            // Scale up then down
            float scale = progress < 0.2f
                ? Mathf.Lerp(0f, 1.3f, progress / 0.2f)
                : Mathf.Lerp(1.3f, 0f, (progress - 0.2f) / 0.8f);
            rt.localScale = Vector3.one * scale;

            // Fade
            txt.color = new Color(startColor.r, startColor.g, startColor.b, 1f - progress);

            yield return null;
        }

        Destroy(note);
    }

    // ─── HELPER: SIMPLE CIRCLE SPRITE ────────────────────
    Sprite MakeCircleSprite()
    {
        int res = 64;
        Texture2D tex = new Texture2D(res, res);
        float center = res / 2f;
        float outerRadius = center;
        float innerRadius = center - 4f; // ring thickness

        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist < outerRadius && dist > innerRadius)
                    tex.SetPixel(x, y, Color.white);
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
    }
}