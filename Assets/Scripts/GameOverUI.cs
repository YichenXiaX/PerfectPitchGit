using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class gameOverUi : MonoBehaviour
{
    [Header("Panel (disable by default in inspector)")]
    public GameObject gameOverPanel;

    [Header("Stats Text")]
    public TextMeshProUGUI finalLevelText;
    public TextMeshProUGUI totalRoundsText;
    public TextMeshProUGUI predictedText;
    public TextMeshProUGUI destroyedText;
    public TextMeshProUGUI hitText;
    public TextMeshProUGUI finalAuraText;

    [Header("Button")]
    public Button restartButton;

    void Start()
    {
        gameMenuTerminal.gameOver(gameOverPanel);
        gameOverPanel.SetActive(false);

        if (gameManager.instance != null)
            gameManager.instance.onGameOver += handleGameOver;

        restartButton.onClick.AddListener(onRestartClicked);
        if (gameManager.instance != null) gameManager.instance.onGameStarted += hideGameOver;
    }

    void OnDestroy()
    {
        if (gameManager.instance != null)
            gameManager.instance.onGameOver -= handleGameOver;
        if (gameManager.instance != null) gameManager.instance.onGameStarted -= hideGameOver;
    }

    private void hideGameOver() { gameOverPanel.SetActive(false); }

    private void handleGameOver(gameSessionData data)
    {
        if(gameManager.instance.isPractice) StartCoroutine(showPracticeGameOver(data));
        else showGameOver(data);
    }
    private System.Collections.IEnumerator showPracticeGameOver(gameSessionData data)
    {
        yield return new WaitForSeconds(1.25f);
        showGameOver(data);
        // The tutorial transmission explains this intentional death; normal run stats would overlap it.
        var stats=gameOverPanel.transform.Find("StatsCard");
        if(stats!=null)stats.gameObject.SetActive(false);
    }
    private void showGameOver(gameSessionData data)
    {
        gameOverPanel.SetActive(true);

        if (finalLevelText) finalLevelText.text = $"Level {data.finalLevel + 1}";
        if (totalRoundsText) totalRoundsText.text = $"Rounds: {data.totalRounds}";
        if (predictedText) predictedText.text = $"Predicted: {data.totalPredictions}";
        if (destroyedText) destroyedText.text = $"Destroyed: {data.totalDestroys}";
        if (hitText) hitText.text = $"Hit: {data.totalHits}";
        if (finalAuraText) finalAuraText.text = $"Aura: {gameManager.instance.aura:F0}";
    }

    private void onRestartClicked()
    {
        gameManager.instance.restartGame();
        if (gameManager.instance.isGameActive)
            gameOverPanel.SetActive(false);
    }
}
