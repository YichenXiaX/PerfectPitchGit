using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public FrequencyManager frequencyManager;

    public AudioSource correctLaserSound; //Correct laser sound effect


    // --- Public read-only state ---
    public int CurrentHealth { get; private set; }
    public int ConsecutiveCorrectPredictions { get; private set; }
    public bool IsGameActive { get; private set; }

    // --- Events for other systems (comet spawner, UI, effects) ---
    public event Action<int> OnCometSpawn;            // passes quadrant index
    public event Action<int> OnPredictionSuccess;     // passes quadrant index
    public event Action OnHealthChanged;
    public event Action<GameSessionData> OnGameOver;
    public event Action OnLevelAdvanced;
    public event Action<int> OnRoundStart;            // passes quadrant index

    // --- Internal round state ---
    private enum RoundState { Prediction, CometActive, Resolved }  // state manager
    private RoundState roundState;

    private List<int> recentQuadrants = new List<int>();
    private int currentQuadrant;
    private int predictedQuadrant;
    private float roundStartTime;
    private int roundIndex;
    //private float predictionDecisionTime = -1f;
    private float predictionClickTime = -1f;

    private GameSessionData sessionData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }


    private void Start()
    {
        //StartGame();
    }

    //kick off the game
    public void StartGame()
    {
        

        var settings = GameSettings.Instance;
        CurrentHealth = settings.maxHealth;
        ConsecutiveCorrectPredictions = 0;
        roundIndex = 0;
        recentQuadrants.Clear();
        IsGameActive = true;

        sessionData = new GameSessionData
        {
            playerName = settings.playerName,
            sessionId = Guid.NewGuid().ToString(),
            sessionStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            results = new List<SequenceResult>()
        };

        Debug.Log($"[GameManager] Game started ！ Player: {settings.playerName}, Level: {settings.level}");
        StartCoroutine(GameLoop());
    }

    // ------------------------------------------------------------------
    //  Core game loop
    // ------------------------------------------------------------------
    private IEnumerator GameLoop()
    {
        while (CurrentHealth > 0)
        {
            var preset = GameSettings.Instance.GetCurrentPreset();

            // 1 ！ Pick a quadrant (no 3+ identical in a row)
            currentQuadrant = PickQuadrant();
            recentQuadrants.Add(currentQuadrant);
            if (recentQuadrants.Count > 2)
                recentQuadrants.RemoveAt(0);

            // 2 ！ Reset round
            roundState = RoundState.Prediction;
            predictedQuadrant = -1;
            roundStartTime = Time.time;
            predictionClickTime = -1f;


            Debug.Log($"[Round {roundIndex + 1}] Q{currentQuadrant + 1} | Level {GameSettings.Instance.level}");
            OnRoundStart?.Invoke(currentQuadrant);

            // 3 ！ Play note sequence
            frequencyManager.PlayNoteSequence(currentQuadrant);

            // 4 ！ Prediction window = sequence length + preSpawnDelay
            float sequenceDuration = preset.notesPerSequence
                                     * (preset.noteDuration + preset.pauseBetweenNotes);
            float predictionWindow = sequenceDuration + preset.preSpawnDelay;

            float elapsed = 0f;
            while (elapsed < predictionWindow && roundState == RoundState.Prediction)
            {
                elapsed += Time.deltaTime;
                yield return null;  // ○ pauses here, waits one frame, then loops again
            }

            // Correct prediction already resolved the round
            if (roundState == RoundState.Resolved)
            {
                frequencyManager.StopSequence();
                correctLaserSound.Play(); //play sound effect
                yield return new WaitForSeconds(preset.pauseBetweenRounds);
                roundIndex++;
                continue;
            }

            // 5 ！ No (correct) prediction ！ spawn comet
            if (roundState == RoundState.Prediction)
            {
                // Timed out with no input at all
                roundState = RoundState.CometActive;
                frequencyManager.StopSequence();
                Debug.Log($"[Round {roundIndex + 1}] No prediction ！ spawning comet");
                SpawnComet(currentQuadrant);
                //OnCometSpawn?.Invoke(currentQuadrant);
            }
            // If roundState is already CometActive, a wrong prediction triggered the spawn

            /*
            // 6 ！ Comet phase ！ wait for destruction or impact
            float cometElapsed = 0f;
            while (cometElapsed < preset.cometTravelTime && roundState == RoundState.CometActive)
            {
                cometElapsed += Time.deltaTime;
                yield return null;
            }

            // 7 ！ Comet reached the mothership
            if (roundState == RoundState.CometActive)
            {
                //float decisionTime = Time.time - roundStartTime;
                //RecordResult(SequenceOutcome.Hit, decisionTime);
                float resolvedTime = Time.time - roundStartTime; // CHANGED ！ was decisionTime
                RecordResult(SequenceOutcome.Hit, predictionClickTime, resolvedTime); // CHANGED ！ added two time params
                ConsecutiveCorrectPredictions = 0;
                CurrentHealth--;

                Debug.Log($"[Round {roundIndex + 1}] COMET HIT ！ Health: {CurrentHealth}/{GameSettings.Instance.maxHealth}");
                OnHealthChanged?.Invoke();

                if (CurrentHealth <= 0)
                {
                    EndGame();
                    yield break;
                }
            }
            */

            while (roundState == RoundState.CometActive)
            {
                yield return null;
            }

            if (CurrentHealth <= 0)
            {
                yield break;
            }

            yield return new WaitForSeconds(preset.pauseBetweenRounds);
            roundIndex++;
        }
    }

    // ------------------------------------------------------------------
    //  Player input hooks
    // ------------------------------------------------------------------


    // The player picks a quadrant before the comet spawns.
    // Called extenally
    public void OnPlayerPrediction(int quadrant)
    {
        if (roundState != RoundState.Prediction) return;

        float decisionTime = Time.time - roundStartTime;
        predictedQuadrant = quadrant;

        if (quadrant == currentQuadrant) //correct
        {
            predictionClickTime = decisionTime; // ADDED
            RecordResult(SequenceOutcome.Predicted, predictionClickTime, decisionTime); //log time
            ConsecutiveCorrectPredictions++;
            roundState = RoundState.Resolved;

            Debug.Log($"[Round {roundIndex + 1}] CORRECT prediction Q{quadrant + 1} " +
                      $"in {decisionTime:F2}s ！ Streak: {ConsecutiveCorrectPredictions}");
            OnPredictionSuccess?.Invoke(currentQuadrant);
            CheckLevelAdvancement();
        }
        else //incorrect
        {
            predictionClickTime = decisionTime; // ADDED
            ConsecutiveCorrectPredictions = 0;
            roundState = RoundState.CometActive;

            Debug.Log($"[Round {roundIndex + 1}] WRONG ！ guessed Q{quadrant + 1}, " +
                      $"actual Q{currentQuadrant + 1} ！ spawning comet");


            StartCoroutine(DelayedSpawnComet(currentQuadrant));
        }
    }


    /// The player destroyed the comet with backup ammo.
    public void OnCometDestroyedByPlayer()
    {
        if (roundState != RoundState.CometActive) return;

        float resolvedTime = Time.time - roundStartTime;
        RecordResult(SequenceOutcome.Destroyed, predictionClickTime, resolvedTime); //log time

        ConsecutiveCorrectPredictions = 0;
        roundState = RoundState.Resolved;

        Debug.Log($"[Round {roundIndex + 1}] Comet destroyed (backup) in {resolvedTime:F2}s");
    }


    public void OnCometHitShip()
    {
        if (roundState != RoundState.CometActive) return;

        float resolvedTime = Time.time - roundStartTime;
        RecordResult(SequenceOutcome.Hit, predictionClickTime, resolvedTime); //log time
        ConsecutiveCorrectPredictions = 0;
        CurrentHealth--;
        roundState = RoundState.Resolved;

        Debug.Log($"[Round {roundIndex + 1}] COMET HIT ！ Health: {CurrentHealth}/{GameSettings.Instance.maxHealth}");
        OnHealthChanged?.Invoke();

        if (CurrentHealth <= 0)
        {
            EndGame();
        }
    }

    // ------------------------------------------------------------------
    //  Internals
    // ------------------------------------------------------------------

    private int PickQuadrant()
    {
        int picked;
        int attempts = 0;

        do
        {
            picked = UnityEngine.Random.Range(0, 4);
            attempts++;
        }
        while (recentQuadrants.Count >= 2
               && recentQuadrants[recentQuadrants.Count - 1] == picked
               && recentQuadrants[recentQuadrants.Count - 2] == picked
               && attempts < 20);

        return picked;
    }


    private IEnumerator DelayedSpawnComet(int quadrant)
    {
        yield return new WaitForSeconds(GameSettings.Instance.cometSpawnDelay);
        SpawnComet(quadrant);
    }

    private void SpawnComet(int quadrant)
    {
        float worldHeight = Camera.main.orthographicSize * 2;
        float worldWidth = worldHeight * Camera.main.aspect;
        float unitWidth = worldWidth / 8;

        // Same x mapping as PlayerController: quadrant 0 = -3, 1 = -1, 2 = 1, 3 = 3
        float x = unitWidth * (-3 + quadrant * 2);

        Vector3 spawnPos = new Vector3(x, GameSettings.Instance.cometSpawnY, 0f);
        Instantiate(GameSettings.Instance.cometPrefabs[quadrant], spawnPos, Quaternion.identity);
    }

    private void RecordResult(SequenceOutcome outcome, float predictionTime, float resolvedTime)
    {
        var result = new SequenceResult
        {
            roundIndex = this.roundIndex,
            correctQuadrant = currentQuadrant,
            predictedQuadrant = predictedQuadrant,
            level = GameSettings.Instance.level,
            //decisionTime = decisionTime,
            predictionTime = predictionTime,   // CHANGED ！ was decisionTime
            resolvedTime = resolvedTime,       // ADDED
            outcome = outcome.ToString(),
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        sessionData.results.Add(result);

        string predLabel = result.predictedQuadrant >= 0
            ? $"Q{result.predictedQuadrant + 1}"
            : "None";

        Debug.Log($"[Data] Round {result.roundIndex + 1} | " +
                  $"Correct: Q{result.correctQuadrant + 1} | " +
                  $"Predicted: {predLabel} | " +
                  $"Level: {result.level} | " +
                  $"Outcome: {result.outcome} | " +
                  $"PredTime: {result.predictionTime:F2}s | " +
                  $"ResolvedTime: {result.resolvedTime:F2}s");
    }

    //advance level
    private void CheckLevelAdvancement()
    {
        if (!GameSettings.Instance.isCustomMode) { //only advance if not in custom mode
            var preset = GameSettings.Instance.GetCurrentPreset();

            if (ConsecutiveCorrectPredictions >= preset.consecutiveCorrectToAdvance)
            {
                GameSettings.Instance.level++;
                ConsecutiveCorrectPredictions = 0;
                Debug.Log($"[GameManager] LEVEL UP ★ {GameSettings.Instance.level}");
                OnLevelAdvanced?.Invoke();
            }
        }
    }

    private void EndGame()
    {
        IsGameActive = false;
        sessionData.finalLevel = GameSettings.Instance.level;
        sessionData.totalRounds = sessionData.results.Count;

        int predicted = 0, destroyed = 0, hit = 0;
        foreach (var r in sessionData.results)
        {
            if (r.outcome == SequenceOutcome.Predicted.ToString()) predicted++;
            else if (r.outcome == SequenceOutcome.Destroyed.ToString()) destroyed++;
            else if (r.outcome == SequenceOutcome.Hit.ToString()) hit++;
        }
        sessionData.totalPredictions = predicted;
        sessionData.totalDestroys = destroyed;
        sessionData.totalHits = hit;

        Debug.Log("=== GAME OVER ===");
        Debug.Log($"Player: {sessionData.playerName} | Final Level: {sessionData.finalLevel}");
        Debug.Log($"Rounds: {sessionData.totalRounds} | " +
                  $"Predicted: {predicted} | Destroyed: {destroyed} | Hit: {hit}");

        OnGameOver?.Invoke(sessionData);
    }

    /// <summary>
    /// Grab session data any time (e.g. for Firebase upload).
    /// </summary>
    public GameSessionData GetSessionData() => sessionData;
}