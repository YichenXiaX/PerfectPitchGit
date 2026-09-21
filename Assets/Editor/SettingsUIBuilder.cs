using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class GameOverUIBuilderTool
{
    [MenuItem("Tools/Build Game Over UI")]
    public static void Build()
    {
        // ── Colors ───────────────────────────────────────────
        Color overlay = new Color(0.02f, 0.02f, 0.06f, 0.92f);
        Color cardBg = new Color(0.08f, 0.08f, 0.15f, 0.95f);
        Color border = new Color(0.3f, 0.5f, 1f, 0.4f);
        Color titleCol = new Color(0.85f, 0.9f, 1f);
        Color labelCol = new Color(0.55f, 0.6f, 0.75f);
        Color valueCol = new Color(0.9f, 0.95f, 1f);
        Color hlLabelCol = new Color(0.4f, 0.7f, 1f);
        Color hlValueCol = new Color(0.4f, 0.8f, 1f);
        Color divCol = new Color(0.3f, 0.4f, 0.6f, 0.4f);
        Color btnCol = new Color(0.2f, 0.5f, 1f);
        Color btnHover = new Color(0.3f, 0.6f, 1f);

        // ── Canvas ───────────────────────────────────────────
        GameObject root = new GameObject("GameOverCanvas");
        Canvas c = root.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;
        var cs = root.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080, 1920);
        root.AddComponent<GraphicRaycaster>();

        // ── Overlay ──────────────────────────────────────────
        GameObject panel = Img(root.transform, "GameOverPanel", overlay);
        Stretch(panel);

        // ── Title ────────────────────────────────────────────
        GameObject title = Txt(panel.transform, "Title", "GAME OVER", 72, titleCol, FontStyles.Bold, TextAlignmentOptions.Center);
        Place(title, 0, 340, 600, 100);

        // ── Card ─────────────────────────────────────────────
        GameObject card = Img(panel.transform, "StatsCard", cardBg);
        Place(card, 0, -10, 620, 480);
        var ol = card.AddComponent<Outline>();
        ol.effectColor = border;
        ol.effectDistance = new Vector2(2, 2);

        // ── Rows inside card ─────────────────────────────────
        float lx = -130f, rx = 130f, rw = 240f, rh = 50f;
        float y = 180f;

        // Level
        Txt(card.transform, "LevelLabel", "Level", 40, hlLabelCol, FontStyles.Normal, TextAlignmentOptions.Left);
        Place("LevelLabel", card, lx, y, rw, rh);
        GameObject levelVal = Txt(card.transform, "LevelValue", "—", 40, hlValueCol, FontStyles.Bold, TextAlignmentOptions.Right);
        Place(levelVal, rx, y, rw, rh);

        // Divider
        y -= 50f;
        GameObject d1 = Img(card.transform, "Div1", divCol);
        Place(d1, 0, y, 480, 2);

        // Rounds
        y -= 50f;
        Txt(card.transform, "RoundsLabel", "Rounds", 34, labelCol, FontStyles.Normal, TextAlignmentOptions.Left);
        Place("RoundsLabel", card, lx, y, rw, rh);
        GameObject roundsVal = Txt(card.transform, "RoundsValue", "—", 34, valueCol, FontStyles.Bold, TextAlignmentOptions.Right);
        Place(roundsVal, rx, y, rw, rh);

        // Predicted
        y -= 55f;
        Txt(card.transform, "PredictedLabel", "Predicted", 34, labelCol, FontStyles.Normal, TextAlignmentOptions.Left);
        Place("PredictedLabel", card, lx, y, rw, rh);
        GameObject predictedVal = Txt(card.transform, "PredictedValue", "—", 34, valueCol, FontStyles.Bold, TextAlignmentOptions.Right);
        Place(predictedVal, rx, y, rw, rh);

        // Destroyed
        y -= 55f;
        Txt(card.transform, "DestroyedLabel", "Destroyed", 34, labelCol, FontStyles.Normal, TextAlignmentOptions.Left);
        Place("DestroyedLabel", card, lx, y, rw, rh);
        GameObject destroyedVal = Txt(card.transform, "DestroyedValue", "—", 34, valueCol, FontStyles.Bold, TextAlignmentOptions.Right);
        Place(destroyedVal, rx, y, rw, rh);

        // Hit
        y -= 55f;
        Txt(card.transform, "HitLabel", "Hit", 34, labelCol, FontStyles.Normal, TextAlignmentOptions.Left);
        Place("HitLabel", card, lx, y, rw, rh);
        GameObject hitVal = Txt(card.transform, "HitValue", "—", 34, valueCol, FontStyles.Bold, TextAlignmentOptions.Right);
        Place(hitVal, rx, y, rw, rh);

        // Divider
        y -= 45f;
        GameObject d2 = Img(card.transform, "Div2", divCol);
        Place(d2, 0, y, 480, 2);

        // Aura
        y -= 50f;
        Txt(card.transform, "AuraLabel", "Aura", 40, hlLabelCol, FontStyles.Normal, TextAlignmentOptions.Left);
        Place("AuraLabel", card, lx, y, rw, rh);
        GameObject auraVal = Txt(card.transform, "AuraValue", "—", 40, hlValueCol, FontStyles.Bold, TextAlignmentOptions.Right);
        Place(auraVal, rx, y, rw, rh);

        // ── Restart button ───────────────────────────────────
        GameObject btnGO = Img(panel.transform, "RestartButton", btnCol);
        Place(btnGO, 0, -320, 400, 90);
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnGO.GetComponent<Image>();
        ColorBlock cb = btn.colors;
        cb.normalColor = btnCol;
        cb.highlightedColor = btnHover;
        cb.pressedColor = btnCol * 0.8f;
        cb.selectedColor = btnCol;
        btn.colors = cb;

        GameObject btnTxt = Txt(btnGO.transform, "BtnText", "RESTART", 38, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(btnTxt);

        // ── Finish ───────────────────────────────────────────
        panel.SetActive(false);
        Selection.activeGameObject = root;
        Undo.RegisterCreatedObjectUndo(root, "Build Game Over UI");
        Debug.Log("[Tools] Game Over UI built — wire GameOverUI fields in the inspector.");
    }

    // ═══════════ Helpers ═══════════════════════════════════════

    static GameObject Img(Transform p, string n, Color c)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = c;
        return go;
    }

    static GameObject Txt(Transform p, string n, string t, float s, Color c, FontStyles fs, TextAlignmentOptions a)
    {
        var go = new GameObject(n);
        go.transform.SetParent(p, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = t; tmp.fontSize = s; tmp.color = c;
        tmp.fontStyle = fs; tmp.alignment = a;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return go;
    }

    static void Place(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    static void Place(string name, GameObject parent, float x, float y, float w, float h)
    {
        Place(parent.transform.Find(name).gameObject, x, y, w, h);
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}