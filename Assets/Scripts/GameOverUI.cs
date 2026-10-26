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
        gameOverPanel.SetActive(false);

        if (gameManager.instance != null)
            gameManager.instance.onGameOver += showGameOver;

        restartButton.onClick.AddListener(onRestartClicked);
        if (gameManager.instance != null) gameManager.instance.onGameStarted += hideGameOver;
    }

    void OnDestroy()
    {
        if (gameManager.instance != null)
            gameManager.instance.onGameOver -= showGameOver;
        if (gameManager.instance != null) gameManager.instance.onGameStarted -= hideGameOver;
    }

    private void hideGameOver() { gameOverPanel.SetActive(false); }

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
