using UnityEngine;
using UnityEngine.UI;

/*
public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance { get; private set; }

    [Header("Panels")]
    public GameObject startOverlay;
    public GameObject settingsPanel;

    [Header("Buttons")]
    public Button gearButton;
    public Button playButton;
    public Button closeSettingsButton;

    [Header("References")]
    public SettingsUI settingsUI;

    private bool gameStarted;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        startOverlay.SetActive(true);
        settingsPanel.SetActive(false);

        gearButton.onClick.AddListener(ToggleSettings);
        playButton.onClick.AddListener(OnPlay);
        closeSettingsButton.onClick.AddListener(CloseSettings);
    }

    private void ToggleSettings()
    {
        if (settingsPanel.activeSelf)
            CloseSettings();
        else
            OpenSettings();
    }

    private void OpenSettings()
    {
        settingsUI.Open();
        settingsPanel.SetActive(true);

        if (gameStarted)
            Time.timeScale = 0f;
    }

    private void CloseSettings()
    {
        settingsUI.ApplySettings();
        settingsPanel.SetActive(false);

        if (gameStarted)
            Time.timeScale = 1f;
    }

    private void OnPlay()
    {
        if (settingsPanel.activeSelf)
            settingsUI.ApplySettings();

        settingsPanel.SetActive(false);
        startOverlay.SetActive(false);
        gameStarted = true;
    }
}

*/
