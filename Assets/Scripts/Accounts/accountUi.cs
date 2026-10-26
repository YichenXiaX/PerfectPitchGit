using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class accountUi : MonoBehaviour
{
    public GameObject panel, loginPanel, dashboardPanel, compactBar;
    public TMP_InputField emailInput, passwordInput, codeInput;
    public TMP_Text statusText, progressText, saveText;
    public UnityEngine.UI.Button loginButton, signupButton, verifyButton, resendButton;
    public UnityEngine.UI.Button playButton, logoutButton, refreshButton, accountButton, closeButton, retryButton;
    private bool opened;
    private float previousTimeScale = 1;
    private cloudAccount account;

    private void Awake() { DontDestroyOnLoad(gameObject); }
    private void Start()
    {
        account = cloudAccount.instance;
        account.changed += render;
        loginButton.onClick.AddListener(() => { var password = passwordInput.text; passwordInput.text = ""; account.logIn(emailInput.text, password); });
        signupButton.onClick.AddListener(() => { var password = passwordInput.text; passwordInput.text = ""; account.signUp(emailInput.text, password); });
        verifyButton.onClick.AddListener(() => { var code = codeInput.text; codeInput.text = ""; account.verifyEmail(emailInput.text, code); });
        resendButton.onClick.AddListener(() => account.resendEmail(emailInput.text));
        refreshButton.onClick.AddListener(account.refreshProgress);
        retryButton.onClick.AddListener(account.retrySave);
        logoutButton.onClick.AddListener(account.logOut);
        accountButton.onClick.AddListener(open);
        closeButton.onClick.AddListener(close);
        playButton.onClick.AddListener(() => {
            if (account.busy || account.hasPendingSave) return;
            opened = false; Time.timeScale = 1;
            SceneManager.LoadScene(cloudAccount.playScene);
        });
        SceneManager.sceneLoaded += sceneLoaded;
        render();
    }
    private void sceneLoaded(Scene scene, LoadSceneMode mode) { opened = false; render(); }
    private void OnDestroy()
    {
        if (account != null) account.changed -= render;
        SceneManager.sceneLoaded -= sceneLoaded;
    }
    private void open()
    {
        if (opened) return;
        previousTimeScale = Time.timeScale; Time.timeScale = 0;
        opened = true;
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (source.isPlaying) source.Pause();
        render();
    }
    private void close()
    {
        if (!account.signedIn) return;
        opened = false; Time.timeScale = previousTimeScale;
        if (previousTimeScale > 0)
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) source.UnPause();
        render();
    }
    private void render()
    {
        if (account == null) return;
        bool inGame = SceneManager.GetActiveScene().name == cloudAccount.playScene;
        if (!account.signedIn && inGame && !opened) { previousTimeScale = Time.timeScale; Time.timeScale = 0; opened = true; }
        bool showing = !inGame || opened || !account.signedIn;
        panel.SetActive(showing);
        loginPanel.SetActive(!account.signedIn);
        dashboardPanel.SetActive(account.signedIn);
        compactBar.SetActive(inGame && !showing);
        closeButton.gameObject.SetActive(inGame && account.signedIn);
        logoutButton.gameObject.SetActive(account.signedIn);
        playButton.gameObject.SetActive(!inGame);
        statusText.text = account.message;
        saveText.text = account.saveMessage;
        var p = account.progress;
        progressText.text = "Welcome, " + account.profile.nickname + "\n\n" +
            "Best score     " + p.bestScore.ToString("N0") + "\n" +
            "Highest level     " + (p.sessionsPlayed == 0 ? "—" : (p.highestLevel + 1).ToString()) + "\n" +
            "Runs started     " + p.sessionsPlayed + "\n" +
            "Runs completed     " + p.sessionsCompleted +
            (inGame ? "\n\n" + account.saveMessage : "");
        foreach (var button in new[] { loginButton, signupButton, verifyButton, resendButton, logoutButton, refreshButton })
            button.interactable = !account.busy;
        emailInput.interactable = passwordInput.interactable = codeInput.interactable = !account.busy;
        closeButton.interactable = !account.busy;
        playButton.interactable = !account.busy && !account.hasPendingSave;
        retryButton.gameObject.SetActive(account.hasPendingSave);
        retryButton.interactable = !account.saving && !account.busy && account.signedIn;
        if (account.signedIn) { passwordInput.text = ""; codeInput.text = ""; emailInput.text = ""; }
    }
}



