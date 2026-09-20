using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SettingsUIBuilder
{
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  PALETTE
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    static readonly Color BG_PANEL = new Color(0.12f, 0.12f, 0.16f, 1f);
    static readonly Color BG_HEADER = new Color(0.08f, 0.08f, 0.11f, 1f);
    static readonly Color BG_SECTION = new Color(0.16f, 0.16f, 0.20f, 1f);
    static readonly Color BG_ROW = new Color(0.20f, 0.20f, 0.25f, 1f);
    static readonly Color BG_INPUT = new Color(0.14f, 0.14f, 0.18f, 1f);
    static readonly Color BG_TOGGLE = new Color(0.22f, 0.22f, 0.28f, 1f);
    static readonly Color BG_SLIDER = new Color(0.15f, 0.15f, 0.20f, 1f);
    static readonly Color FILL_SLIDER = new Color(0.30f, 0.55f, 0.90f, 1f);
    static readonly Color BG_BUTTON = new Color(0.25f, 0.30f, 0.50f, 1f);
    static readonly Color BG_CLOSE = new Color(0.55f, 0.20f, 0.20f, 1f);
    static readonly Color BG_OVERLAY = new Color(0f, 0f, 0f, 0.85f);
    static readonly Color ACCENT = new Color(0.30f, 0.55f, 0.90f, 1f);
    static readonly Color TEXT_WHITE = Color.white;
    static readonly Color TEXT_DIM = new Color(0.65f, 0.65f, 0.65f, 1f);

    const float ROW_H = 36f;
    const float LABEL_W = 160f;
    const float VALUE_W = 55f;
    const float TOGGLE_SZ = 24f;

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  ENTRY POINT
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    [MenuItem("Tools/Build Settings UI")]
    public static void Build()
    {
        SafeDestroy("Canvas");
        SafeDestroy("EventSystem");

        var canvas = MakeCanvas();
        var root = canvas.transform;
        MakeEventSystem();

        BuildGearButton(root);
        BuildStartOverlay(root);
        BuildSettingsPanel(root);

        // GameUIManager_GO  (empty holder for your runtime script)
        UI("GameUIManager_GO", root);

        Debug.Log("[SettingsUIBuilder] Build complete.");
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  TOP-LEVEL BUILDERS
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    static void BuildGearButton(Transform root)
    {
        var go = MakeButton(root, "GearButton", "=", 50, 50);
        var rt = RT(go);
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-10, -10);
    }

    static void BuildStartOverlay(Transform root)
    {
        var overlay = MakePanel(root, "StartOverlay", BG_OVERLAY);
        Stretch(overlay);

        var title = MakeText(overlay.transform, "Title", "Game Title",
            48, FontStyles.Bold, TextAlignmentOptions.Center);
        AnchorStretch(title, 0f, 0.55f, 1f, 0.8f);

        var sub = MakeText(overlay.transform, "Subtitle", "Press Play to Start",
            22, FontStyles.Italic, TextAlignmentOptions.Center);
        sub.GetComponent<TextMeshProUGUI>().color = TEXT_DIM;
        AnchorStretch(sub, 0f, 0.43f, 1f, 0.55f);

        var play = MakeButton(overlay.transform, "PlayButton", "PLAY", 220, 60);
        play.GetComponent<Image>().color = ACCENT;
        var prt = RT(play);
        prt.anchorMin = new Vector2(0.5f, 0.25f);
        prt.anchorMax = new Vector2(0.5f, 0.25f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
    }

    static void BuildSettingsPanel(Transform root)
    {
        var panel = MakePanel(root, "SettingsPanel", BG_PANEL);
        var prt = RT(panel);
        prt.anchorMin = new Vector2(0.1f, 0.03f);
        prt.anchorMax = new Vector2(0.9f, 0.97f);
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        // ©¤©¤ Header ©¤©¤
        var header = UI("Header", panel.transform);
        header.AddComponent<Image>().color = BG_HEADER;
        var hrt = RT(header);
        hrt.anchorMin = new Vector2(0, 1);
        hrt.anchorMax = new Vector2(1, 1);
        hrt.pivot = new Vector2(0.5f, 1);
        hrt.sizeDelta = new Vector2(0, 50);

        var stitle = MakeText(header.transform, "SettingsTitle", "Settings",
            24, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        var strt = RT(stitle);
        strt.anchorMin = Vector2.zero;
        strt.anchorMax = Vector2.one;
        strt.offsetMin = new Vector2(15, 0);
        strt.offsetMax = new Vector2(-55, 0);

        var close = MakeButton(header.transform, "CloseButton", "X", 40, 40);
        close.GetComponent<Image>().color = BG_CLOSE;
        var crt = RT(close);
        crt.anchorMin = new Vector2(1, 0.5f);
        crt.anchorMax = new Vector2(1, 0.5f);
        crt.pivot = new Vector2(1, 0.5f);
        crt.anchoredPosition = new Vector2(-5, 0);

        // ©¤©¤ ScrollView ©¤©¤
        var sv = MakeScrollView(panel.transform, "ScrollView");
        var svrt = RT(sv);
        svrt.anchorMin = Vector2.zero;
        svrt.anchorMax = Vector2.one;
        svrt.offsetMin = Vector2.zero;
        svrt.offsetMax = new Vector2(0, -50);

        var content = sv.GetComponent<ScrollRect>().content.gameObject;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.spacing = 12;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        content.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        Transform ct = content.transform;

        BuildModeSection(ct);
        BuildPlayerSection(ct);
        BuildSequenceSection(ct);
        BuildOctaveSection(ct);
        BuildTimingSection(ct);
        BuildProgressionSection(ct);

        panel.SetActive(false);
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  SECTION BUILDERS
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    // ©¤©¤©¤ ModeSection ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    static void BuildModeSection(Transform parent)
    {
        var sec = MakeSection(parent, "ModeSection", false);
        var t = sec.transform;

        MakeSectionLabel(t, "ModeLabel", "Mode");
        MakeToggleRow(t, "PresetToggle", "Preset Mode", true);
        MakeToggleRow(t, "CustomToggle", "Custom Mode", false);

        var ddRow = MakeRow(t, "LevelDropdownRow");
        MakeRowLabel(ddRow.transform, "LevelLabel", "Level");
        MakeDropdown(ddRow.transform, "LevelDropdown",
            new[] { "Easy", "Medium", "Hard", "Expert" });
    }

    // ©¤©¤©¤ PlayerSection ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    static void BuildPlayerSection(Transform parent)
    {
        var sec = MakeSection(parent, "PlayerSection", true);
        var t = sec.transform;

        MakeSectionLabel(t, "SectionLabel", "Player");

        var nameRow = MakeRow(t, "NameRow");
        MakeRowLabel(nameRow.transform, "NameLabel", "Name");
        MakeInputField(nameRow.transform, "NameInput", "Enter name...");

        var lhRow = MakeRow(t, "LeftHandedRow");
        MakeRowLabel(lhRow.transform, "LHLabel", "Left Handed");
        MakeRowToggle(lhRow.transform, "LHToggle", false);

        MakeSliderRow(t, "HealthRow", "HealthLabel", "Health",
            "HealthSlider", "HealthValue", 1, 10, 5, true);
    }

    // ©¤©¤©¤ SequenceSection ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    static void BuildSequenceSection(Transform parent)
    {
        var sec = MakeSection(parent, "SequenceSection", true);
        var t = sec.transform;

        MakeSectionLabel(t, "SectionLabel", "Sequence");
        MakeSliderRow(t, "NotesRow", "Label", "Notes", "Slider", "Value", 1, 20, 4, true);
        MakeSliderRow(t, "DurationRow", "Label", "Duration", "Slider", "Value", 0.5f, 10, 2f, false);
        MakeSliderRow(t, "PauseRow", "Label", "Pause", "Slider", "Value", 0, 5, 1f, false);
    }

    // ©¤©¤©¤ OctaveSection ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    static void BuildOctaveSection(Transform parent)
    {
        var sec = MakeSection(parent, "OctaveSection", true);
        var t = sec.transform;

        MakeSectionLabel(t, "SectionLabel", "Octave Shifts");

        // Horizontal row for all toggles
        var row = UI("OctaveToggles", t);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(5, 5, 4, 4);
        h.spacing = 4;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;
        h.childControlWidth = true;
        h.childControlHeight = true;
        row.AddComponent<LayoutElement>().preferredHeight = 54;

        for (int i = -5; i <= 5; i++)
        {
            string sign = i > 0 ? "+" : "";
            string lbl = $"{sign}{i}";
            MakeCompactToggle(row.transform, $"Toggle {lbl}", lbl, i == 0);
        }
    }

    static GameObject MakeCompactToggle(Transform parent, string name,
    string label, bool isOn)
    {
        var go = UI(name, parent);
        var v = go.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(2, 2, 2, 2);
        v.spacing = 2;
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;
        v.childControlWidth = false;
        v.childControlHeight = true;

        // Checkbox background
        var bg = UI("Background", go.transform);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = BG_TOGGLE;
        var bgLE = bg.AddComponent<LayoutElement>();
        bgLE.preferredWidth = TOGGLE_SZ;
        bgLE.preferredHeight = TOGGLE_SZ;

        // Checkmark
        var cm = UI("Checkmark", bg.transform);
        Stretch(cm);
        RT(cm).offsetMin = new Vector2(4, 4);
        RT(cm).offsetMax = new Vector2(-4, -4);
        var cmImg = cm.AddComponent<Image>();
        cmImg.color = ACCENT;

        // Label underneath
        var lbl = MakeText(go.transform, "Label", label, 12,
            FontStyles.Normal, TextAlignmentOptions.Center);
        lbl.AddComponent<LayoutElement>().preferredHeight = 16;

        // Toggle component
        var tog = go.AddComponent<Toggle>();
        tog.isOn = isOn;
        tog.graphic = cmImg;
        tog.targetGraphic = bgImg;
        return go;
    }

    // ©¤©¤©¤ TimingSection ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    static void BuildTimingSection(Transform parent)
    {
        var sec = MakeSection(parent, "TimingSection", true);
        var t = sec.transform;

        MakeSectionLabel(t, "SectionLabel", "Timing");
        MakeSliderRow(t, "PreSpawnRow", "Label", "Pre-Spawn", "Slider", "Value", 0, 5, 1f, false);
        MakeSliderRow(t, "ResponseRow", "Label", "Response", "Slider", "Value", 0.1f, 5, 1.5f, false);
        MakeSliderRow(t, "CometSpeedRow", "Label", "Comet Speed", "Slider", "Value", 0.5f, 10, 3f, false);
        MakeSliderRow(t, "SpawnDelayRow", "Label", "Spawn Delay", "Slider", "Value", 0, 3, 0.5f, false);
        MakeSliderRow(t, "PauseBtwRndsRow", "Label", "Pause Btw Rnds", "Slider", "Value", 0, 10, 2f, false);
    }

    // ©¤©¤©¤ ProgressionSection ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    static void BuildProgressionSection(Transform parent)
    {
        var sec = MakeSection(parent, "ProgressionSection", true);
        var t = sec.transform;

        MakeSectionLabel(t, "SectionLabel", "Progression");
        MakeSliderRow(t, "ConsecutiveRow", "Label", "Consecutive",
            "Slider", "Value", 1, 20, 3, true);
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  CORE HELPERS  (no double-RectTransform, ever)
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    /// <summary>
    /// The ONE factory for every UI GameObject.
    /// Always born with a RectTransform ¡ª never add one manually.
    /// </summary>
    static GameObject UI(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static RectTransform RT(GameObject go) => go.GetComponent<RectTransform>();

    static void Stretch(GameObject go)
    {
        var rt = RT(go);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void AnchorStretch(GameObject go, float xMin, float yMin, float xMax, float yMax)
    {
        var rt = RT(go);
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SafeDestroy(string name)
    {
        var obj = GameObject.Find(name);
        if (obj != null) Object.DestroyImmediate(obj);
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    //  WIDGET FACTORIES
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    static GameObject MakeCanvas()
    {
        var go = new GameObject("Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        return go;
    }

    static void MakeEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null) return;
        new GameObject("EventSystem",
            typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // ©¤©¤©¤©¤©¤ Panel ©¤©¤©¤©¤©¤

    static GameObject MakePanel(Transform parent, string name, Color col)
    {
        var go = UI(name, parent);
        go.AddComponent<Image>().color = col;
        return go;
    }

    // ©¤©¤©¤©¤©¤ Text ©¤©¤©¤©¤©¤

    static GameObject MakeText(Transform parent, string name, string text,
        float size, FontStyles style = FontStyles.Normal,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = UI(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = TEXT_WHITE;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return go;
    }

    // ©¤©¤©¤©¤©¤ Button ©¤©¤©¤©¤©¤

    static GameObject MakeButton(Transform parent, string name,
        string text, float w, float h)
    {
        var go = UI(name, parent);
        go.AddComponent<Image>().color = BG_BUTTON;
        go.AddComponent<Button>();
        RT(go).sizeDelta = new Vector2(w, h);

        var lbl = UI("Text", go.transform);
        Stretch(lbl);
        var tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = TEXT_WHITE;
        return go;
    }

    // ©¤©¤©¤©¤©¤ ScrollView ©¤©¤©¤©¤©¤

    static GameObject MakeScrollView(Transform parent, string name)
    {
        var go = UI(name, parent);
        go.AddComponent<Image>().color = Color.clear;

        var vp = UI("Viewport", go.transform);
        Stretch(vp);
        vp.AddComponent<Image>().color = Color.white;
        vp.AddComponent<Mask>().showMaskGraphic = false;

        var ct = UI("Content", vp.transform);
        var crt = RT(ct);
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(0.5f, 1);
        crt.sizeDelta = Vector2.zero;

        var sr = go.AddComponent<ScrollRect>();
        sr.content = crt;
        sr.viewport = RT(vp);
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.scrollSensitivity = 30;
        return go;
    }

    // ©¤©¤©¤©¤©¤ Section ©¤©¤©¤©¤©¤

    static GameObject MakeSection(Transform parent, string name, bool canvasGroup)
    {
        var go = UI(name, parent);
        go.AddComponent<Image>().color = BG_SECTION;
        if (canvasGroup) go.AddComponent<CanvasGroup>();

        var v = go.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(10, 10, 5, 10);
        v.spacing = 6;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childControlWidth = true;
        v.childControlHeight = true;
        return go;
    }

    static GameObject MakeSectionLabel(Transform parent, string name, string text)
    {
        var go = MakeText(parent, name, text, 18, FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        go.AddComponent<LayoutElement>().preferredHeight = 28;
        return go;
    }

    // ©¤©¤©¤©¤©¤ Row ©¤©¤©¤©¤©¤

    static GameObject MakeRow(Transform parent, string name)
    {
        var go = UI(name, parent);
        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(5, 5, 2, 2);
        h.spacing = 8;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childControlWidth = true;
        h.childControlHeight = true;
        go.AddComponent<LayoutElement>().preferredHeight = ROW_H;
        return go;
    }

    static GameObject MakeRowLabel(Transform parent, string name, string text)
    {
        var go = MakeText(parent, name, text, 16, FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        go.AddComponent<LayoutElement>().preferredWidth = LABEL_W;
        return go;
    }

    // ©¤©¤©¤©¤©¤ Toggle (full row with label) ©¤©¤©¤©¤©¤

    static GameObject MakeToggleRow(Transform parent, string name,
        string label, bool isOn)
    {
        var go = UI(name, parent);
        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(5, 5, 2, 2);
        h.spacing = 10;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childControlWidth = true;
        h.childControlHeight = true;
        go.AddComponent<LayoutElement>().preferredHeight = ROW_H;

        // Background
        var bg = UI("Background", go.transform);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = BG_TOGGLE;
        var bgLE = bg.AddComponent<LayoutElement>();
        bgLE.preferredWidth = TOGGLE_SZ;
        bgLE.preferredHeight = TOGGLE_SZ;

        // Checkmark
        var cm = UI("Checkmark", bg.transform);
        Stretch(cm);
        RT(cm).offsetMin = new Vector2(4, 4);
        RT(cm).offsetMax = new Vector2(-4, -4);
        var cmImg = cm.AddComponent<Image>();
        cmImg.color = ACCENT;

        // Label
        var lbl = MakeText(go.transform, "Label", label, 16,
            FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        lbl.AddComponent<LayoutElement>().flexibleWidth = 1;

        // Toggle component
        var tog = go.AddComponent<Toggle>();
        tog.isOn = isOn;
        tog.graphic = cmImg;
        tog.targetGraphic = bgImg;
        return go;
    }

    // ©¤©¤©¤©¤©¤ Toggle (small, inside a row) ©¤©¤©¤©¤©¤

    static GameObject MakeRowToggle(Transform parent, string name, bool isOn)
    {
        var go = UI(name, parent);
        var bgImg = go.AddComponent<Image>();
        bgImg.color = BG_TOGGLE;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = TOGGLE_SZ;
        le.preferredHeight = TOGGLE_SZ;

        var cm = UI("Checkmark", go.transform);
        Stretch(cm);
        RT(cm).offsetMin = new Vector2(4, 4);
        RT(cm).offsetMax = new Vector2(-4, -4);
        var cmImg = cm.AddComponent<Image>();
        cmImg.color = ACCENT;

        var tog = go.AddComponent<Toggle>();
        tog.isOn = isOn;
        tog.graphic = cmImg;
        tog.targetGraphic = bgImg;
        return go;
    }

    // ©¤©¤©¤©¤©¤ Slider Row  (Label / Slider / Value) ©¤©¤©¤©¤©¤

    static void MakeSliderRow(Transform parent,
        string rowName, string labelName, string labelText,
        string sliderName, string valueName,
        float min, float max, float val, bool wholeNumbers)
    {
        var row = MakeRow(parent, rowName);
        var t = row.transform;

        MakeRowLabel(t, labelName, labelText);
        MakeSlider(t, sliderName, min, max, val, wholeNumbers);

        string display = wholeNumbers
            ? Mathf.RoundToInt(val).ToString()
            : val.ToString("F1");
        var vGo = MakeText(t, valueName, display, 16,
            FontStyles.Normal, TextAlignmentOptions.Center);
        vGo.AddComponent<LayoutElement>().preferredWidth = VALUE_W;
    }

    // ©¤©¤©¤©¤©¤ Slider ©¤©¤©¤©¤©¤

    static GameObject MakeSlider(Transform parent, string name,
        float min, float max, float val, bool wholeNumbers)
    {
        var go = UI(name, parent);
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        le.preferredHeight = 20;

        var bg = UI("Background", go.transform);
        Stretch(bg);
        bg.AddComponent<Image>().color = BG_SLIDER;

        var fillArea = UI("Fill Area", go.transform);
        Stretch(fillArea);
        RT(fillArea).offsetMin = new Vector2(5, 5);
        RT(fillArea).offsetMax = new Vector2(-5, -5);

        var fill = UI("Fill", fillArea.transform);
        Stretch(fill);
        fill.AddComponent<Image>().color = FILL_SLIDER;

        var handleArea = UI("Handle Slide Area", go.transform);
        Stretch(handleArea);
        RT(handleArea).offsetMin = new Vector2(10, 0);
        RT(handleArea).offsetMax = new Vector2(-10, 0);

        var handle = UI("Handle", handleArea.transform);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;
        RT(handle).sizeDelta = new Vector2(16, 0);

        var s = go.AddComponent<Slider>();
        s.fillRect = RT(fill);
        s.handleRect = RT(handle);
        s.targetGraphic = handleImg;
        s.minValue = min;
        s.maxValue = max;
        s.wholeNumbers = wholeNumbers;
        s.value = val;
        s.direction = Slider.Direction.LeftToRight;
        return go;
    }

    // ©¤©¤©¤©¤©¤ Input Field ©¤©¤©¤©¤©¤

    static GameObject MakeInputField(Transform parent, string name, string placeholder)
    {
        var go = UI(name, parent);
        go.AddComponent<Image>().color = BG_INPUT;
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        le.preferredHeight = ROW_H;

        var area = UI("Text Area", go.transform);
        Stretch(area);
        RT(area).offsetMin = new Vector2(10, 2);
        RT(area).offsetMax = new Vector2(-10, -2);
        area.AddComponent<RectMask2D>();

        var ph = UI("Placeholder", area.transform);
        Stretch(ph);
        var phTMP = ph.AddComponent<TextMeshProUGUI>();
        phTMP.text = placeholder;
        phTMP.fontSize = 16;
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.color = TEXT_DIM;
        phTMP.alignment = TextAlignmentOptions.MidlineLeft;
        phTMP.enableWordWrapping = false;

        var txt = UI("Text", area.transform);
        Stretch(txt);
        var txtTMP = txt.AddComponent<TextMeshProUGUI>();
        txtTMP.text = "";
        txtTMP.fontSize = 16;
        txtTMP.color = TEXT_WHITE;
        txtTMP.alignment = TextAlignmentOptions.MidlineLeft;
        txtTMP.enableWordWrapping = false;

        var input = go.AddComponent<TMP_InputField>();
        input.textViewport = RT(area);
        input.textComponent = txtTMP;
        input.placeholder = phTMP;
        input.fontAsset = txtTMP.font;
        input.pointSize = 16;
        return go;
    }

    // ©¤©¤©¤©¤©¤ Dropdown ©¤©¤©¤©¤©¤

    static GameObject MakeDropdown(Transform parent, string name, string[] options)
    {
        var go = UI(name, parent);
        var img = go.AddComponent<Image>();
        img.color = BG_INPUT;
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        le.preferredHeight = ROW_H;

        // Caption
        var cap = UI("Label", go.transform);
        Stretch(cap);
        RT(cap).offsetMin = new Vector2(10, 0);
        RT(cap).offsetMax = new Vector2(-30, 0);
        var capTMP = cap.AddComponent<TextMeshProUGUI>();
        capTMP.fontSize = 16;
        capTMP.color = TEXT_WHITE;
        capTMP.alignment = TextAlignmentOptions.MidlineLeft;

        // Arrow
        var arrow = UI("Arrow", go.transform);
        var art = RT(arrow);
        art.anchorMin = new Vector2(1, 0);
        art.anchorMax = new Vector2(1, 1);
        art.pivot = new Vector2(1, 0.5f);
        art.sizeDelta = new Vector2(25, 0);
        arrow.AddComponent<Image>().color = Color.grey;

        // Template (hidden)
        var tmpl = UI("Template", go.transform);
        var tmplRT = RT(tmpl);
        tmplRT.anchorMin = new Vector2(0, 0);
        tmplRT.anchorMax = new Vector2(1, 0);
        tmplRT.pivot = new Vector2(0.5f, 1);
        tmplRT.sizeDelta = new Vector2(0, 150);
        tmpl.AddComponent<Image>().color = BG_SECTION;

        var vp = UI("Viewport", tmpl.transform);
        Stretch(vp);
        vp.AddComponent<Image>().color = Color.white;
        vp.AddComponent<Mask>().showMaskGraphic = false;

        var ct = UI("Content", vp.transform);
        var ctRT = RT(ct);
        ctRT.anchorMin = new Vector2(0, 1);
        ctRT.anchorMax = new Vector2(1, 1);
        ctRT.pivot = new Vector2(0.5f, 1);
        ctRT.sizeDelta = new Vector2(0, 28);

        var sr = tmpl.AddComponent<ScrollRect>();
        sr.content = ctRT;
        sr.viewport = RT(vp);
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;

        // Item prototype
        var item = UI("Item", ct.transform);
        var irt = RT(item);
        irt.anchorMin = new Vector2(0, 0.5f);
        irt.anchorMax = new Vector2(1, 0.5f);
        irt.sizeDelta = new Vector2(0, 28);

        var iBG = UI("Item Background", item.transform);
        Stretch(iBG);
        var iBGImg = iBG.AddComponent<Image>();
        iBGImg.color = BG_ROW;

        var iCM = UI("Item Checkmark", item.transform);
        var icrt = RT(iCM);
        icrt.anchorMin = new Vector2(0, 0.5f);
        icrt.anchorMax = new Vector2(0, 0.5f);
        icrt.sizeDelta = new Vector2(20, 20);
        icrt.anchoredPosition = new Vector2(10, 0);
        var iCMImg = iCM.AddComponent<Image>();
        iCMImg.color = ACCENT;

        var iLbl = UI("Item Label", item.transform);
        Stretch(iLbl);
        RT(iLbl).offsetMin = new Vector2(35, 0);
        var iLblTMP = iLbl.AddComponent<TextMeshProUGUI>();
        iLblTMP.fontSize = 16;
        iLblTMP.color = TEXT_WHITE;
        iLblTMP.alignment = TextAlignmentOptions.MidlineLeft;

        var iTog = item.AddComponent<Toggle>();
        iTog.graphic = iCMImg;
        iTog.targetGraphic = iBGImg;

        // TMP_Dropdown
        var dd = go.AddComponent<TMP_Dropdown>();
        dd.template = tmplRT;
        dd.captionText = capTMP;
        dd.itemText = iLblTMP;
        dd.targetGraphic = img;
        dd.options.Clear();
        foreach (var o in options)
            dd.options.Add(new TMP_Dropdown.OptionData(o));
        dd.value = 0;
        dd.RefreshShownValue();

        tmpl.SetActive(false);
        return go;
    }
}