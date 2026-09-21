using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TeleportVFX : MonoBehaviour
{
    [Header("References")]
    public Transform ship;
    public SpriteRenderer shipSprite;

    [Header("Colors")]
    public Color coreColor = new Color(0.85f, 0.95f, 1f, 1f);
    public Color boltColor = new Color(0.3f, 0.6f, 1f, 0.9f);
    public Color flashColor = new Color(0.5f, 0.8f, 1f, 0.6f);

    [Header("Settings")]
    public int boltCount = 6;
    public float boltLength = 1.5f;
    public float boltWidth = 0.06f;
    public int boltSegments = 8;
    public float boltNoise = 0.3f;
    public float effectDuration = 0.35f;

    public void Teleport(Vector3 targetPos)
    {
        StartCoroutine(TeleportSequence(targetPos));
    }

    IEnumerator TeleportSequence(Vector3 targetPos)
    {
        Vector3 originPos = ship.position;

        // ©¤©¤ PHASE 1: Charge up at origin (quick) ©¤©¤
        GameObject originFX = CreateElectricBurst(originPos);
        GameObject ghost = CreateGhost(originPos);

        // Flash the ship white
        Color originalColor = shipSprite.color;
        shipSprite.color = coreColor;

        yield return new WaitForSeconds(0.03f);

        // ©¤©¤ PHASE 2: Instant move ©¤©¤
        ship.position = targetPos;

        // ©¤©¤ PHASE 3: Electric burst at destination ©¤©¤
        GameObject destFX = CreateElectricBurst(targetPos);
        GameObject destFlash = CreateFlashCircle(targetPos);

        // Ship flickers in
        StartCoroutine(ShipFlicker(originalColor));

        // ©¤©¤ PHASE 4: Connecting arc between origin and dest (optional cool touch) ©¤©¤
        GameObject arc = null;
        float dist = Vector3.Distance(originPos, targetPos);
        if (dist > 0.5f && dist < 15f)
        {
            arc = CreateConnectingArc(originPos, targetPos);
        }

        // ©¤©¤ PHASE 5: Animate everything ©¤©¤
        float t = 0f;
        List<LineRenderer> originBolts = GetBolts(originFX);
        List<LineRenderer> destBolts = GetBolts(destFX);
        LineRenderer arcLine = arc?.GetComponentInChildren<LineRenderer>();

        while (t < effectDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / effectDuration);
            float fade = 1f - p;
            float snap = fade * fade; // fast falloff

            // Regenerate bolts with new noise each frame
            RegenerateBolts(originBolts, originPos, snap);
            RegenerateBolts(destBolts, targetPos, snap);

            // Shrink bolt widths
            SetBoltWidths(originBolts, boltWidth * snap);
            SetBoltWidths(destBolts, boltWidth * snap);

            // Fade connecting arc
            if (arcLine != null)
            {
                RegenerateArc(arcLine, originPos, targetPos, boltNoise * fade);
                arcLine.widthMultiplier = boltWidth * 1.5f * snap;
                SetLineAlpha(arcLine, fade);
            }

            // Fade ghost
            if (ghost != null)
            {
                SpriteRenderer sr = ghost.GetComponent<SpriteRenderer>();
                Color c = sr.color;
                sr.color = new Color(c.r, c.g, c.b, 0.5f * snap);
                ghost.transform.localScale = Vector3.one * (1f + p * 0.3f);
            }

            // Fade flash circle
            if (destFlash != null)
            {
                SpriteRenderer sr = destFlash.GetComponent<SpriteRenderer>();
                sr.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * snap);
                destFlash.transform.localScale = Vector3.one * (0.5f + p * 2f);
            }

            yield return null;
        }

        Destroy(originFX);
        Destroy(destFX);
        Destroy(destFlash);
        Destroy(ghost);
        if (arc != null) Destroy(arc);
    }

    // ©¤©¤©¤ ELECTRIC BURST ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    GameObject CreateElectricBurst(Vector3 pos)
    {
        GameObject burst = new GameObject("ElectricBurst");
        burst.transform.position = pos;

        for (int i = 0; i < boltCount; i++)
        {
            float angle = (360f / boltCount) * i + Random.Range(-20f, 20f);
            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.up;

            GameObject boltObj = new GameObject($"Bolt_{i}");
            boltObj.transform.SetParent(burst.transform);

            LineRenderer lr = boltObj.AddComponent<LineRenderer>();
            lr.positionCount = boltSegments;
            lr.widthMultiplier = boltWidth;
            lr.startColor = coreColor;
            lr.endColor = new Color(boltColor.r, boltColor.g, boltColor.b, 0.1f);
            lr.useWorldSpace = true;
            lr.sortingOrder = 12;
            lr.numCapVertices = 3;

            Material mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;

            lr.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.5f, 0.6f),
                new Keyframe(1f, 0.1f)
            );

            // Store direction in the name for regeneration
            boltObj.name = $"Bolt_{angle}";
            SetBoltPositions(lr, pos, dir);
        }

        return burst;
    }

    void SetBoltPositions(LineRenderer lr, Vector3 origin, Vector3 dir)
    {
        Vector3 right = Vector3.Cross(dir, Vector3.forward).normalized;

        for (int i = 0; i < boltSegments; i++)
        {
            float p = (float)i / (boltSegments - 1);
            Vector3 pos = origin + dir * (boltLength * p);

            if (i > 0 && i < boltSegments - 1)
            {
                // Jagged lightning offsets
                float jag = boltNoise * Mathf.Sin(p * Mathf.PI);
                pos += right * Random.Range(-jag, jag);
            }

            lr.SetPosition(i, pos);
        }
    }

    void RegenerateBolts(List<LineRenderer> bolts, Vector3 center, float scale)
    {
        foreach (var lr in bolts)
        {
            // Parse angle from name
            string name = lr.gameObject.name;
            float angle = float.Parse(name.Split('_')[1]);
            // Add jitter to angle each frame
            float jitteredAngle = angle + Random.Range(-10f, 10f);
            Vector3 dir = Quaternion.Euler(0, 0, jitteredAngle) * Vector3.up;

            Vector3 right = Vector3.Cross(dir, Vector3.forward).normalized;

            for (int i = 0; i < lr.positionCount; i++)
            {
                float p = (float)i / (lr.positionCount - 1);
                Vector3 pos = center + dir * (boltLength * p * scale);

                if (i > 0 && i < lr.positionCount - 1)
                {
                    float jag = boltNoise * scale * Mathf.Sin(p * Mathf.PI);
                    pos += right * Random.Range(-jag, jag);
                }

                lr.SetPosition(i, pos);
            }
        }
    }

    void SetBoltWidths(List<LineRenderer> bolts, float width)
    {
        foreach (var lr in bolts)
            lr.widthMultiplier = width;
    }

    // ©¤©¤©¤ CONNECTING ARC ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    GameObject CreateConnectingArc(Vector3 from, Vector3 to)
    {
        GameObject arcObj = new GameObject("ConnectingArc");

        GameObject lineObj = new GameObject("ArcLine");
        lineObj.transform.SetParent(arcObj.transform);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = 12;
        lr.widthMultiplier = boltWidth * 1.5f;
        lr.startColor = boltColor;
        lr.endColor = boltColor;
        lr.useWorldSpace = true;
        lr.sortingOrder = 11;
        lr.numCapVertices = 3;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;

        RegenerateArc(lr, from, to, boltNoise);
        return arcObj;
    }

    void RegenerateArc(LineRenderer lr, Vector3 from, Vector3 to, float noise)
    {
        Vector3 dir = (to - from).normalized;
        Vector3 right = Vector3.Cross(dir, Vector3.forward).normalized;
        float dist = Vector3.Distance(from, to);

        for (int i = 0; i < lr.positionCount; i++)
        {
            float p = (float)i / (lr.positionCount - 1);
            Vector3 pos = Vector3.Lerp(from, to, p);

            if (i > 0 && i < lr.positionCount - 1)
            {
                float envelope = Mathf.Sin(p * Mathf.PI);
                pos += right * Random.Range(-noise, noise) * envelope * dist * 0.3f;
            }

            lr.SetPosition(i, pos);
        }
    }

    // ©¤©¤©¤ GHOST (afterimage at old position) ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    GameObject CreateGhost(Vector3 pos)
    {
        if (shipSprite == null) return null;

        GameObject ghost = new GameObject("Ghost");
        ghost.transform.position = pos;
        ghost.transform.rotation = ship.rotation;
        ghost.transform.localScale = ship.localScale;

        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
        sr.sprite = shipSprite.sprite;
        sr.color = new Color(boltColor.r, boltColor.g, boltColor.b, 0.5f);
        sr.sortingOrder = shipSprite.sortingOrder - 1;

        return ghost;
    }

    // ©¤©¤©¤ FLASH CIRCLE ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    GameObject CreateFlashCircle(Vector3 pos)
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
                alpha *= alpha;
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();

        GameObject flash = new GameObject("FlashCircle");
        flash.transform.position = pos;

        SpriteRenderer sr = flash.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 16f);
        sr.color = flashColor;
        sr.sortingOrder = 11;
        flash.transform.localScale = Vector3.one * 0.5f;

        return flash;
    }

    // ©¤©¤©¤ SHIP FLICKER ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    IEnumerator ShipFlicker(Color originalColor)
    {
        int flicks = 5;
        float flickDuration = 0.12f;

        for (int i = 0; i < flicks; i++)
        {
            float t = (float)i / flicks;
            // Alternate between white-blue and original
            shipSprite.color = (i % 2 == 0) ? coreColor : originalColor;

            // Flickers get slower as they settle
            yield return new WaitForSeconds(flickDuration / flicks * (i + 1) * 0.5f);
        }

        shipSprite.color = originalColor;
    }

    // ©¤©¤©¤ HELPERS ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    List<LineRenderer> GetBolts(GameObject parent)
    {
        List<LineRenderer> bolts = new List<LineRenderer>();
        if (parent == null) return bolts;
        foreach (Transform child in parent.transform)
        {
            LineRenderer lr = child.GetComponent<LineRenderer>();
            if (lr != null) bolts.Add(lr);
        }
        return bolts;
    }

    void SetLineAlpha(LineRenderer lr, float alpha)
    {
        Color s = lr.startColor;
        Color e = lr.endColor;
        lr.startColor = new Color(s.r, s.g, s.b, alpha);
        lr.endColor = new Color(e.r, e.g, e.b, alpha * 0.3f);
    }
}