using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;

public class settingsUi : MonoBehaviour
{
    public GameObject startOverlay;
    public GameObject settingPanel;
    public AudioSource audioSource;
    public gameManager gameManager;
    [UnityEngine.Serialization.FormerlySerializedAs("auraHUD")]
    public auraHud auraHud;

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
    public Slider predictionWindowSlider;
    public TextMeshProUGUI predictionWindowValue; // gap after sequence ends before comet spawns
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

    private gameSettings settings;
    private bool initialized;

    // ©¤©¤ init ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void initialize()
    {
        if (initialized) return;
        settings = gameSettings.instance;
        configureSliders();
        wireUpListeners();
        populateLevelDropdown();
        initialized = true;
    }

    public void open()
    {
        initialize();
        loadSettingsIntoUi();
        Time.timeScale = 0f;
        audioSource.Pause();
    }

    /// <summary>Call from the X button's OnClick.</summary>
    public void close()
    {
        initialize();
        applySettings();
        Time.timeScale = 1f;
        auraHud.refreshFromSettings();
        settingPanel.SetActive(false);
        audioSource.UnPause();
    }

    /// <summary>Call from the Gear button's OnClick.</summary>
    public void show()
    {
        Debug.Log("clicked");
        settingPanel.SetActive(true);
        open();
    }

    /// <summary>Call from the Start Game button's OnClick.</summary>
    public void startGame()
    {
        initialize();
        applySettings();
        startOverlay.SetActive(false);

        gameManager.instance.startGame();
    }

    public void applySettings()
    {
        bool isCustom = customToggle.isOn;
        settings.isCustomMode = isCustom;

        settings.playerName = playerNameInput.text;
        settings.leftHanded = leftHandedToggle.isOn;
        settings.maxHealth = (int)maxHealthSlider.value;

        if(settings.maxHealth < gameManager.getCurrentHealth())
        {
            gameManager.setCurrentHealth(settings.maxHealth);
        }

        //settings.cometSpawnDelay = cometSpawnDelaySlider.value;

        if (isCustom)
        {
            settings.customPreset = new gameSettings.levelPreset
            {
                name = "Custom",
                notesPerSequence = (int)notesPerSequenceSlider.value,
                noteDuration = noteDurationSlider.value,
                pauseBetweenNotes = pauseBetweenNotesSlider.value,
                predictionWindow = predictionWindowSlider.value,
                cometSpawnDelay = cometSpawnDelaySlider.value,
                cometSpeed = cometSpeedSlider.value,
                pauseBetweenRounds = pauseBetweenRoundsSlider.value,
                consecutiveCorrectToAdvance = (int)consecutiveCorrectSlider.value,
                octaveShifts = getSelectedOctaveShifts()
            };
            settings.level = 0;
        }
        else
        {
            settings.level = levelDropdown.value;
        }

        //auraHud.refreshFromSettings();
    }

    // ©¤©¤ slider ranges ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void configureSliders()
    {
        setSlider(notesPerSequenceSlider, 1, 100, true);
        setSlider(noteDurationSlider, 0.1f, 3f);
        setSlider(pauseBetweenNotesSlider, 0f, 1f);
        setSlider(predictionWindowSlider, 3f, 15f);
        setSlider(cometSpeedSlider, 0.5f, 10f);
        setSlider(cometSpawnDelaySlider, 0f, 3f);
        setSlider(pauseBetweenRoundsSlider, 2f, 10f);
        setSlider(consecutiveCorrectSlider, 1, 100, true);
        setSlider(maxHealthSlider, 1, 300, true);
    }

    private void setSlider(Slider s, float min, float max, bool whole = false)
    {
        s.minValue = min;
        s.maxValue = max;
        s.wholeNumbers = whole;
    }

    // ©¤©¤ listeners ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void wireUpListeners()
    {
        presetToggle.onValueChanged.AddListener(onModeChanged);
        levelDropdown.onValueChanged.AddListener(onLevelSelected);

        presetToggle.onValueChanged.AddListener(onModeChanged);
        customToggle.onValueChanged.AddListener(onCustomToggleChanged);

        notesPerSequenceSlider.onValueChanged.AddListener(v =>
            notesPerSequenceValue.text = v.ToString("F0"));
        noteDurationSlider.onValueChanged.AddListener(v =>
            noteDurationValue.text = v.ToString("F2") + "s");
        pauseBetweenNotesSlider.onValueChanged.AddListener(v =>
            pauseBetweenNotesValue.text = v.ToString("F2") + "s");
        predictionWindowSlider.onValueChanged.AddListener(v =>
            predictionWindowValue.text = v.ToString("F1") + "s");
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
    private void onCustomToggleChanged(bool customIsOn)
    {
        presetToggle.SetIsOnWithoutNotify(!customIsOn);
        onModeChanged(!customIsOn);
    }

    // ©¤©¤ dropdown ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void populateLevelDropdown()
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
    private void onModeChanged(bool presetIsOn)
    {
        customToggle.SetIsOnWithoutNotify(!presetIsOn);

        bool isCustom = !presetIsOn;
        levelDropdownRow.SetActive(!isCustom);

        setGroupLocked(sequenceGroup, isCustom);
        setGroupLocked(timingGroup, isCustom);
        setGroupLocked(playerGroup, isCustom);
        setGroupLocked(octaveGroup, isCustom);
        setGroupLocked(progressionGroup); //always locks progression

        if (!isCustom && settings.levelPresets.Length > 0)
            loadPresetIntoUi(settings.levelPresets[levelDropdown.value]);
    }

    private void setGroupLocked(CanvasGroup group, bool unlocked)
    {
        group.interactable = unlocked;
        group.alpha = unlocked ? 1f : 0.3f;
    }

    private void setGroupLocked(CanvasGroup group) //overloading, always locks progression
    {
        group.interactable = false;
        group.alpha = 0.3f;
    }

    // ©¤©¤ level selected ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void onLevelSelected(int index)
    {
        if (presetToggle.isOn && index < settings.levelPresets.Length)
            loadPresetIntoUi(settings.levelPresets[index]);
    }

    // ©¤©¤ load values into UI ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void loadSettingsIntoUi()
    {
        presetToggle.onValueChanged.RemoveListener(onModeChanged);

        bool isCustom = settings.isCustomMode;
        presetToggle.isOn = !isCustom;
        customToggle.isOn = isCustom;

        levelDropdownRow.SetActive(!isCustom);
        setGroupLocked(sequenceGroup, isCustom);
        setGroupLocked(timingGroup, isCustom);
        setGroupLocked(playerGroup, isCustom);
        setGroupLocked(octaveGroup, isCustom);
        setGroupLocked(progressionGroup); //always locks progression

        if (isCustom)
        {
            loadPresetIntoUi(settings.customPreset);
        }
        else if (settings.levelPresets.Length > 0)
        {
            levelDropdown.value = settings.level;
            loadPresetIntoUi(settings.levelPresets[settings.level]);
        }

        playerNameInput.text = settings.playerName;
        leftHandedToggle.isOn = settings.leftHanded;
        maxHealthSlider.value = settings.maxHealth;
        //cometSpawnDelaySlider.value = settings.cometSpawnDelay;

        presetToggle.onValueChanged.AddListener(onModeChanged);
    }

    private void loadPresetIntoUi(gameSettings.levelPreset preset)
    {
        notesPerSequenceSlider.value = preset.notesPerSequence;
        noteDurationSlider.value = preset.noteDuration;
        pauseBetweenNotesSlider.value = preset.pauseBetweenNotes;
        predictionWindowSlider.value = preset.predictionWindow;
        cometSpawnDelaySlider.value = preset.cometSpawnDelay;
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
    private int[] getSelectedOctaveShifts()
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