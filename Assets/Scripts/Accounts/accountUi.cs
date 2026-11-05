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
    public static bool isBlocking { get; private set; }
    private bool opened;
    private float previousTimeScale = 1;
    private cloudAccount account;
    private readonly System.Collections.Generic.List<AudioSource> pausedSources = new System.Collections.Generic.List<AudioSource>();

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
            if (SceneManager.GetActiveScene().name == cloudAccount.tutorialScene) SceneManager.LoadScene(cloudAccount.accountScene);
            else account.enterPlay();
        });
        SceneManager.sceneLoaded += sceneLoaded;
        if(GetComponent<accountTerminal>()==null)gameObject.AddComponent<accountTerminal>();
        render();
    }
    private void sceneLoaded(Scene scene, LoadSceneMode mode) { pausedSources.Clear(); opened = false; render(); }
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
        gameManager.instance?.playerController.clearHeldInput(false);
        pausedSources.Clear();
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (source.isPlaying) { pausedSources.Add(source); source.Pause(); }
        render();
    }
    private void close()
    {
        if (!account.signedIn) return;
        opened = false; Time.timeScale = previousTimeScale;
        gameManager.instance?.playerController.clearHeldInput(false);
        if (previousTimeScale > 0)
            foreach (var source in pausedSources) if(source!=null) source.UnPause();
        pausedSources.Clear();
        render();
    }
    private void render()
    {
        if (account == null) return;
        bool inGame = SceneManager.GetActiveScene().name == cloudAccount.playScene || SceneManager.GetActiveScene().name == cloudAccount.tutorialScene;
        if (!account.signedIn && inGame && !opened) { previousTimeScale = Time.timeScale; Time.timeScale = 0; opened = true; }
        bool showing = !inGame || opened || !account.signedIn;
        isBlocking = showing;
        panel.SetActive(showing);
        loginPanel.SetActive(!account.signedIn);
        dashboardPanel.SetActive(account.signedIn);
        compactBar.SetActive(inGame && !showing);
        closeButton.gameObject.SetActive(inGame && account.signedIn);
        logoutButton.gameObject.SetActive(account.signedIn);
        playButton.gameObject.SetActive(account.signedIn && (!inGame || SceneManager.GetActiveScene().name == cloudAccount.tutorialScene));
        closeButton.GetComponentInChildren<TMP_Text>().text = SceneManager.GetActiveScene().name == cloudAccount.tutorialScene ? "Return to training" : "Return to game";
        playButton.GetComponentInChildren<TMP_Text>().text = SceneManager.GetActiveScene().name == cloudAccount.tutorialScene ? "Exit training" : (account.tutorialComplete ? "Enter game" : "Start flight training");
        statusText.text = account.message;
        saveText.text = SceneManager.GetActiveScene().name == cloudAccount.tutorialScene ? "TRAINING / scores and research recording off" : account.saveMessage;
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
