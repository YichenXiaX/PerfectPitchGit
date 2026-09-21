using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public class AuraHUDBuilder : Editor
{
    [MenuItem("Tools/Build Aura HUD")]
    public static void Build()
    {
        GameObject old = GameObject.Find("AuraHUD_Canvas");
        if (old != null) Undo.DestroyObjectImmediate(old);

        // ─── Canvas ──────────────────────────────────────
        GameObject canvasGO = new GameObject("AuraHUD_Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ─── Panel (240 x 72, top-left corner) ──────────
        GameObject panel = CreateUI("AuraPanel", canvasGO.transform);
        RectTransform panelRT = SetRect(panel, 24, -24, 240, 72, AnchorPreset.TopLeft);

        Image panelBG = panel.AddComponent<Image>();
        panelBG.color = new Color(0.047f, 0.07f, 0.137f, 0.85f);

        // ─── Level Badge (left side, fills height) ───────
        GameObject badge = CreateUI("LevelBadge", panel.transform);
        RectTransform badgeRT = badge.GetComponent<RectTransform>();
        badgeRT.anchorMin = new Vector2(0, 0);
        badgeRT.anchorMax = new Vector2(0, 1);
        badgeRT.pivot = new Vector2(0, 0.5f);
        badgeRT.anchoredPosition = Vector2.zero;
        badgeRT.sizeDelta = new Vector2(56, 0); // 56 wide, stretch height

        Image badgeBG = badge.AddComponent<Image>();
        badgeBG.color = new Color(0.2f, 0.47f, 1f, 0.9f);

        // LV label (top half of badge)
        GameObject lvLabel = CreateTMP("LevelLabel", badge.transform, "LV",
            9, FontStyles.Bold, new Color(0.78f, 0.86f, 1f, 0.8f), TextAlignmentOptions.Center);
        RectTransform lvLabelRT = lvLabel.GetComponent<RectTransform>();
        lvLabelRT.anchorMin = new Vector2(0, 0.5f);
        lvLabelRT.anchorMax = new Vector2(1, 1f);
        lvLabelRT.offsetMin = Vector2.zero;
        lvLabelRT.offsetMax = Vector2.zero;

        // Level number (bottom half of badge)
        GameObject lvText = CreateTMP("LevelText", badge.transform, "1",
            26, FontStyles.Bold, new Color(0.88f, 0.92f, 1f), TextAlignmentOptions.Center);
        RectTransform lvTextRT = lvText.GetComponent<RectTransform>();
        lvTextRT.anchorMin = new Vector2(0, 0);
        lvTextRT.anchorMax = new Vector2(1, 0.55f);
        lvTextRT.offsetMin = Vector2.zero;
        lvTextRT.offsetMax = Vector2.zero;

        // ─── Right Section (everything right of badge) ───
        // AURA label
        GameObject auraLabel = CreateTMP("AuraLabel", panel.transform, "AURA",
            9, FontStyles.Bold, new Color(0.39f, 0.67f, 1f, 0.5f), TextAlignmentOptions.TopLeft);
        auraLabel.GetComponent<TextMeshProUGUI>().characterSpacing = 16;
        RectTransform auraLabelRT = auraLabel.GetComponent<RectTransform>();
        auraLabelRT.anchorMin = new Vector2(0, 1);
        auraLabelRT.anchorMax = new Vector2(1, 1);
        auraLabelRT.pivot = new Vector2(0, 1);
        auraLabelRT.anchoredPosition = new Vector2(66, -6);
        auraLabelRT.sizeDelta = new Vector2(-76, 14);

        // Aura Container (for punch animation)
        GameObject auraContainer = CreateUI("AuraContainer", panel.transform);
        RectTransform acRT = auraContainer.GetComponent<RectTransform>();
        acRT.anchorMin = new Vector2(0, 0);
        acRT.anchorMax = new Vector2(1, 1);
        acRT.pivot = new Vector2(0, 0.5f);
        acRT.offsetMin = new Vector2(66, 14);
        acRT.offsetMax = new Vector2(-10, -18);

        // Aura number (fills container)
        GameObject auraText = CreateTMP("AuraText", auraContainer.transform, "0",
            28, FontStyles.Bold, new Color(0.39f, 0.71f, 1f), TextAlignmentOptions.Left);
        Stretch(auraText);

        // ─── XP Bar ─────────────────────────────────────
        GameObject xpBg = CreateUI("XPBarBg", panel.transform);
        RectTransform xpBgRT = xpBg.GetComponent<RectTransform>();
        xpBgRT.anchorMin = new Vector2(0, 0);
        xpBgRT.anchorMax = new Vector2(1, 0);
        xpBgRT.pivot = new Vector2(0, 0);
        xpBgRT.anchoredPosition = new Vector2(62, 6);
        xpBgRT.sizeDelta = new Vector2(-72, 5);

        Image xpBgImg = xpBg.AddComponent<Image>();
        xpBgImg.color = new Color(0.08f, 0.12f, 0.24f, 0.9f);

        // XP Fill
        GameObject xpFill = CreateUI("XPBarFill", xpBg.transform);
        Stretch(xpFill);

        Image xpFillImg = xpFill.AddComponent<Image>();
        xpFillImg.color = new Color(0.24f, 0.55f, 1f, 1f);
        xpFillImg.type = Image.Type.Filled;
        xpFillImg.fillMethod = Image.FillMethod.Horizontal;
        xpFillImg.fillOrigin = 0;
        xpFillImg.fillAmount = 0.35f;

        // ─── Wire AuraHUD component ─────────────────────
        AuraHUD hud = canvasGO.AddComponent<AuraHUD>();
        hud.auraText = auraText.GetComponent<TextMeshProUGUI>();
        hud.auraLabel = auraLabel.GetComponent<TextMeshProUGUI>();
        hud.levelText = lvText.GetComponent<TextMeshProUGUI>();
        hud.levelLabel = lvLabel.GetComponent<TextMeshProUGUI>();
        hud.xpBarFill = xpFillImg;
        hud.auraContainer = acRT;
        hud.levelBadge = badgeRT;

        Selection.activeGameObject = canvasGO;
        Undo.RegisterCreatedObjectUndo(canvasGO, "Build Aura HUD");

        Debug.Log("<color=#64b4ff>✦ AuraHUD built — no layout groups, pure anchors.</color>");
    }

    // ─── HELPERS ──────────────────────────────────────────

    static GameObject CreateUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject CreateTMP(string name, Transform parent, string text,
        float size, FontStyles style, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;

        return go;
    }

    // Stretch to fill parent
    static void Stretch(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    enum AnchorPreset { TopLeft, TopRight }

    static RectTransform SetRect(GameObject go, float x, float y, float w, float h, AnchorPreset preset)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        switch (preset)
        {
            case AnchorPreset.TopLeft:
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                break;
            case AnchorPreset.TopRight:
                rt.anchorMin = new Vector2(1, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 1);
                break;
        }
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }
}