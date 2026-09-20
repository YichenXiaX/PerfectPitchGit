using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsUI : MonoBehaviour
{
    public GameObject startOverlay;
    public GameObject settingPanel;

    [Header("Mode")]
    public Toggle presetToggle;
    public Toggle customToggle;
    public TMP_Dropdown levelDropdown;
    public GameObject levelDropdownRow;

    [Header("Player")]
    public TMP_InputField playerNameInput;
    public Toggle leftHandedToggle;
    public Slider maxHealthSlider;
    public TextMeshProUGUI maxHealthValue;

    [Header("Sequence")]
    public Slider notesPerSequenceSlider;
    public TextMeshProUGUI notesPerSequenceValue;
    public Slider noteDurationSlider;
    public TextMeshProUGUI noteDurationValue;
    public Slider pauseBetweenNotesSlider;
    public TextMeshProUGUI pauseBetweenNotesValue;

    [Header("Octave Shifts")]
    public Toggle octaveNeg5;
    public Toggle octaveNeg4;
    public Toggle octaveNeg3;
    public Toggle octaveNeg2;
    public Toggle octaveNeg1;
    public Toggle octave0;
    public Toggle octavePos1;
    public Toggle octavePos2;
    public Toggle octavePos3;
    public Toggle octavePos4;
    public Toggle octavePos5;

    [Header("Timing")]
    public Slider preSpawnDelaySlider;
    public TextMeshProUGUI preSpawnDelayValue; // // gap after sequence ends before comet spawns
    public Slider cometSpeedSlider;
    public TextMeshProUGUI cometSpeedValue;
    public Slider cometSpawnDelaySlider;
    public TextMeshProUGUI cometSpawnDelayValue;
    public Slider pauseBetweenRoundsSlider;
    public TextMeshProUGUI pauseBetweenRoundsValue;

    [Header("Progression")]
    public Slider consecutiveCorrectSlider;
    public TextMeshProUGUI consecutiveCorrectValue;

    [Header("Lockable Groups")]
    public CanvasGroup sequenceGroup;
    public CanvasGroup timingGroup;
    public CanvasGroup playerGroup;
    public CanvasGroup progressionGroup;
    public CanvasGroup octaveGroup;

    private GameSettings settings;
    private bool initialized;

    // ©¤©¤ init ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void Initialize()
    {
        if (initialized) return;
        settings = GameSettings.Instance;
        ConfigureSliders();
        WireUpListeners();
        PopulateLevelDropdown();
        initialized = true;
    }

    public void Open()
    {
        Initialize();
        LoadSettingsIntoUI();
    }

    /// <summary>Call from the X button's OnClick.</summary>
    public void Close()
    {
        Initialize();
        ApplySettings();
        settingPanel.SetActive(false);
    }

    /// <summary>Call from the Gear button's OnClick.</summary>
    public void Show()
    {
        Debug.Log("clicked");
        settingPanel.SetActive(true);
        Open();
    }

    /// <summary>Call from the Start Game button's OnClick.</summary>
    public void StartGame()
    {
        Initialize();
        ApplySettings();
        startOverlay.SetActive(false);

        GameManager.Instance.StartGame();
    }

    public void ApplySettings()
    {
        bool isCustom = customToggle.isOn;
        settings.isCustomMode = isCustom;

        settings.playerName = playerNameInput.text;
        settings.leftHanded = leftHandedToggle.isOn;
        settings.maxHealth = (int)maxHealthSlider.value;
        settings.cometSpawnDelay = cometSpawnDelaySlider.value;

        if (isCustom)
        {
            settings.customPreset = new GameSettings.LevelPreset
            {
                name = "Custom",
                notesPerSequence = (int)notesPerSequenceSlider.value,
                noteDuration = noteDurationSlider.value,
                pauseBetweenNotes = pauseBetweenNotesSlider.value,
                preSpawnDelay = preSpawnDelaySlider.value,
                cometSpeed = cometSpeedSlider.value,
                pauseBetweenRounds = pauseBetweenRoundsSlider.value,
                consecutiveCorrectToAdvance = (int)consecutiveCorrectSlider.value,
                octaveShifts = GetSelectedOctaveShifts()
            };
            settings.level = 0;
        }
        else
        {
            settings.level = levelDropdown.value;
        }
    }

    // ©¤©¤ slider ranges ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void ConfigureSliders()
    {
        SetSlider(notesPerSequenceSlider, 1, 12, true);
        SetSlider(noteDurationSlider, 0.1f, 3f);
        SetSlider(pauseBetweenNotesSlider, 0f, 1f);
        SetSlider(preSpawnDelaySlider, 0f, 5f);
        SetSlider(cometSpeedSlider, 0.5f, 10f);
        SetSlider(cometSpawnDelaySlider, 0f, 3f);
        SetSlider(pauseBetweenRoundsSlider, 0.5f, 10f);
        SetSlider(consecutiveCorrectSlider, 1, 10, true);
        SetSlider(maxHealthSlider, 1, 10, true);
    }

    private void SetSlider(Slider s, float min, float max, bool whole = false)
    {
        s.minValue = min;
        s.maxValue = max;
        s.wholeNumbers = whole;
    }

    // ©¤©¤ listeners ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void WireUpListeners()
    {
        presetToggle.onValueChanged.AddListener(OnModeChanged);
        levelDropdown.onValueChanged.AddListener(OnLevelSelected);

        presetToggle.onValueChanged.AddListener(OnModeChanged);
        customToggle.onValueChanged.AddListener(OnCustomToggleChanged);

        notesPerSequenceSlider.onValueChanged.AddListener(v =>
            notesPerSequenceValue.text = v.ToString("F0"));
        noteDurationSlider.onValueChanged.AddListener(v =>
            noteDurationValue.text = v.ToString("F2") + "s");
        pauseBetweenNotesSlider.onValueChanged.AddListener(v =>
            pauseBetweenNotesValue.text = v.ToString("F2") + "s");
        preSpawnDelaySlider.onValueChanged.AddListener(v =>
            preSpawnDelayValue.text = v.ToString("F1") + "s");
        cometSpeedSlider.onValueChanged.AddListener(v =>
            cometSpeedValue.text = v.ToString("F1"));
        cometSpawnDelaySlider.onValueChanged.AddListener(v =>
            cometSpawnDelayValue.text = v.ToString("F1") + "s");
        pauseBetweenRoundsSlider.onValueChanged.AddListener(v =>
            pauseBetweenRoundsValue.text = v.ToString("F1") + "s");
        consecutiveCorrectSlider.onValueChanged.AddListener(v =>
            consecutiveCorrectValue.text = v.ToString("F0"));
        maxHealthSlider.onValueChanged.AddListener(v =>
            maxHealthValue.text = v.ToString("F0"));
    }

    // ©¤©¤ mode toggle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void OnCustomToggleChanged(bool customIsOn)
    {
        presetToggle.SetIsOnWithoutNotify(!customIsOn);
        OnModeChanged(!customIsOn);
    }

    // ©¤©¤ dropdown ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void PopulateLevelDropdown()
    {
        levelDropdown.ClearOptions();
        var options = new List<string>();
        for (int i = 0; i < settings.levelPresets.Length; i++)
        {
            string name = string.IsNullOrEmpty(settings.levelPresets[i].name)
                ? $"Level {i + 1}"
                : settings.levelPresets[i].name;
            options.Add(name);
        }
        levelDropdown.AddOptions(options);
    }

    // ©¤©¤ mode toggle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void OnModeChanged(bool presetIsOn)
    {
        customToggle.SetIsOnWithoutNotify(!presetIsOn);

        bool isCustom = !presetIsOn;
        levelDropdownRow.SetActive(!isCustom);

        SetGroupLocked(sequenceGroup, isCustom);
        SetGroupLocked(timingGroup, isCustom);
        SetGroupLocked(playerGroup, isCustom);
        SetGroupLocked(progressionGroup, isCustom);
        SetGroupLocked(octaveGroup, isCustom);

        if (!isCustom && settings.levelPresets.Length > 0)
            LoadPresetIntoUI(settings.levelPresets[levelDropdown.value]);
    }

    private void SetGroupLocked(CanvasGroup group, bool unlocked)
    {
        group.interactable = unlocked;
        group.alpha = unlocked ? 1f : 0.5f;
    }

    // ©¤©¤ level selected ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void OnLevelSelected(int index)
    {
        if (presetToggle.isOn && index < settings.levelPresets.Length)
            LoadPresetIntoUI(settings.levelPresets[index]);
    }

    // ©¤©¤ load values into UI ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void LoadSettingsIntoUI()
    {
        presetToggle.onValueChanged.RemoveListener(OnModeChanged);

        bool isCustom = settings.isCustomMode;
        presetToggle.isOn = !isCustom;
        customToggle.isOn = isCustom;

        levelDropdownRow.SetActive(!isCustom);
        SetGroupLocked(sequenceGroup, isCustom);
        SetGroupLocked(timingGroup, isCustom);
        SetGroupLocked(playerGroup, isCustom);
        SetGroupLocked(progressionGroup, isCustom);
        SetGroupLocked(octaveGroup, isCustom);

        if (isCustom)
        {
            LoadPresetIntoUI(settings.customPreset);
        }
        else if (settings.levelPresets.Length > 0)
        {
            levelDropdown.value = settings.level;
            LoadPresetIntoUI(settings.levelPresets[settings.level]);
        }

        playerNameInput.text = settings.playerName;
        leftHandedToggle.isOn = settings.leftHanded;
        maxHealthSlider.value = settings.maxHealth;
        cometSpawnDelaySlider.value = settings.cometSpawnDelay;

        presetToggle.onValueChanged.AddListener(OnModeChanged);
    }

    private void LoadPresetIntoUI(GameSettings.LevelPreset preset)
    {
        notesPerSequenceSlider.value = preset.notesPerSequence;
        noteDurationSlider.value = preset.noteDuration;
        pauseBetweenNotesSlider.value = preset.pauseBetweenNotes;
        preSpawnDelaySlider.value = preset.preSpawnDelay;
        cometSpeedSlider.value = preset.cometSpeed;
        pauseBetweenRoundsSlider.value = preset.pauseBetweenRounds;
        consecutiveCorrectSlider.value = preset.consecutiveCorrectToAdvance;

        var shifts = new HashSet<int>(preset.octaveShifts);
        octaveNeg5.isOn = shifts.Contains(-5);
        octaveNeg4.isOn = shifts.Contains(-4);
        octaveNeg3.isOn = shifts.Contains(-3);
        octaveNeg2.isOn = shifts.Contains(-2);
        octaveNeg1.isOn = shifts.Contains(-1);
        octave0.isOn = shifts.Contains(0);
        octavePos1.isOn = shifts.Contains(1);
        octavePos2.isOn = shifts.Contains(2);
        octavePos3.isOn = shifts.Contains(3);
        octavePos4.isOn = shifts.Contains(4);
        octavePos5.isOn = shifts.Contains(5);
    }

    // ©¤©¤ octave helper ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private int[] GetSelectedOctaveShifts()
    {
        var shifts = new List<int>();
        if (octaveNeg5.isOn) shifts.Add(-5);
        if (octaveNeg4.isOn) shifts.Add(-4);
        if (octaveNeg3.isOn) shifts.Add(-3);
        if (octaveNeg2.isOn) shifts.Add(-2);
        if (octaveNeg1.isOn) shifts.Add(-1);
        if (octave0.isOn) shifts.Add(0);
        if (octavePos1.isOn) shifts.Add(1);
        if (octavePos2.isOn) shifts.Add(2);
        if (octavePos3.isOn) shifts.Add(3);
        if (octavePos4.isOn) shifts.Add(4);
        if (octavePos5.isOn) shifts.Add(5);

        if (shifts.Count == 0) shifts.Add(0);
        return shifts.ToArray();
    }
}