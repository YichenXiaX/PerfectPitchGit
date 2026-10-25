using UnityEngine;
using UnityEngine.UI;

/*
public class gameUiManager : MonoBehaviour
{
    public static gameUiManager instance { get; private set; }

    [Header("Panels")]
    public GameObject startOverlay;
    public GameObject settingsPanel;

    [Header("Buttons")]
    public Button gearButton;
    public Button playButton;
    public Button closeSettingsButton;

    [Header("References")]
    public settingsUi settingsUi;

    private bool gameStarted;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        startOverlay.SetActive(true);
        settingsPanel.SetActive(false);

        gearButton.onClick.AddListener(toggleSettings);
        playButton.onClick.AddListener(onPlay);
        closeSettingsButton.onClick.AddListener(closeSettings);
    }

    private void toggleSettings()
    {
        if (settingsPanel.activeSelf)
            closeSettings();
        else
            openSettings();
    }

    private void openSettings()
    {
        settingsUi.open();
        settingsPanel.SetActive(true);

        if (gameStarted)
            Time.timeScale = 0f;
    }

    private void closeSettings()
    {
        settingsUi.applySettings();
        settingsPanel.SetActive(false);

        if (gameStarted)
            Time.timeScale = 1f;
    }

    private void onPlay()
    {
        if (settingsPanel.activeSelf)
            settingsUi.applySettings();

        settingsPanel.SetActive(false);
        startOverlay.SetActive(false);
        gameStarted = true;
    }
}

*/
