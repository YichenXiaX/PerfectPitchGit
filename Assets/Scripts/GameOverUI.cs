using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
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

        if (GameManager.Instance != null)
            GameManager.Instance.OnGameOver += ShowGameOver;

        restartButton.onClick.AddListener(OnRestartClicked);
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameOver -= ShowGameOver;
    }

    private void ShowGameOver(GameSessionData data)
    {
        gameOverPanel.SetActive(true);

        if (finalLevelText) finalLevelText.text = $"Level {data.finalLevel + 1}";
        if (totalRoundsText) totalRoundsText.text = $"Rounds: {data.totalRounds}";
        if (predictedText) predictedText.text = $"Predicted: {data.totalPredictions}";
        if (destroyedText) destroyedText.text = $"Destroyed: {data.totalDestroys}";
        if (hitText) hitText.text = $"Hit: {data.totalHits}";
        if (finalAuraText) finalAuraText.text = $"Aura: {GameManager.Instance.Aura:F0}";
    }

    private void OnRestartClicked()
    {
        gameOverPanel.SetActive(false);
        GameManager.Instance.RestartGame();
    }
}