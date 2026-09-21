using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public FrequencyManager frequencyManager;
    public PlayerController playerController;
    public GameObject OverlayObj;
    public GameObject starfieldEffect;
    //public AuraHUD auraHUD;
    //AuraHUD hud = FindObjectOfType<AuraHUD>();

    // --- Aura tuning constants ---
    private const float PREDICTION_AURA_K = 5000f;
    private const float COMET_DESTROY_AURA_K = 500f;
    private const float WRONG_PREDICTION_PENALTY = 2000f;
    private const float MISSED_SHOT_PENALTY = 300f;
    private const float COMET_HIT_PENALTY = 2000f;


    // --- Public read-only state ---
    public int CurrentHealth { get; private set; }
    public float Aura { get; private set; }
    public int ConsecutiveCorrectPredictions { get; private set; }
    public bool IsGameActive { get; private set; }

    // --- Events for other systems (comet spawner, UI, effects) ---
    public event Action<int> OnCometSpawn;            // passes quadrant index
    public event Action<int> OnPredictionSuccess;     // passes quadrant index
    public event Action OnHealthChanged;
    public event Action<float> OnAuraChanged;
    public event Action<float> OnXPChanged;
    public event Action<GameSessionData> OnGameOver;
    public event Action OnLevelAdvanced;
    public event Action<int> OnRoundStart;            // passes quadrant index

    // --- Internal round state ---
    public enum RoundState { Prediction, CometActive, Resolved }  // state manager
    public RoundState roundState;

    private List<int> recentQuadrants = new List<int>();
    private int currentQuadrant;
    private int predictedQuadrant;
    private float roundStartTime;
    private int roundIndex;
    private float cometSpawnTime;
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
        
    }

    //kick off the game
    public void StartGame()
    {
        var settings = GameSettings.Instance;
        CurrentHealth = settings.maxHealth;
        ConsecutiveCorrectPredictions = 0;
        roundIndex = 0;
        recentQuadrants.Clear();
        Aura = 0f;
        IsGameActive = true;
        OverlayObj.SetActive(false);
        starfieldEffect.SetActive(true);

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
            cometSpawnTime = -1f;


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
                ConsecutiveCorrectPredictions = 0;  
                UpdateXPProgress(); //○ reset bar when player didn't even try
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
            predictionClickTime = decisionTime;
            RecordResult(SequenceOutcome.Predicted, predictionClickTime, decisionTime); //log time
            ConsecutiveCorrectPredictions++;
            roundState = RoundState.Resolved;


            // --- Aura: big speed-based gain ---
            float auraGain = PREDICTION_AURA_K / Mathf.Max(decisionTime, 0.01f);
            Aura += auraGain;
            OnAuraChanged?.Invoke(auraGain);
            Debug.Log($"[Aura] Correct prediction in {decisionTime:F2}s ！ +{auraGain:F0} | Aura: {Aura:F0}");

            //auraHUD.SpawnFloatingText(50);

            UpdateXPProgress();

            playerController.FancyShot();

            //hud.AddAura(5000);

            Debug.Log($"[Round {roundIndex + 1}] CORRECT prediction Q{quadrant + 1} " +
                      $"in {decisionTime:F2}s ！ Streak: {ConsecutiveCorrectPredictions}");
            OnPredictionSuccess?.Invoke(currentQuadrant);
            CheckLevelAdvancement();
        }
        else //incorrect
        {
            predictionClickTime = decisionTime;
            ConsecutiveCorrectPredictions = 0;
            roundState = RoundState.CometActive;

            // --- Aura
            //Aura -= WRONG_PREDICTION_PENALTY;
            //OnAuraChanged?.Invoke(-WRONG_PREDICTION_PENALTY);
            //Debug.Log($"[Aura] Wrong prediction ！ -{WRONG_PREDICTION_PENALTY} | Aura: {Aura:F0}");


            playerController.FailedPredictionShot();
            //frequencyManager.StopSequence();

            UpdateXPProgress();

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

        frequencyManager.StopSequence(); //stops the sequence


        // --- Aura: small speed-based gain from comet spawn ---
        float cometReactionTime = Time.time - cometSpawnTime;
        float auraGain = COMET_DESTROY_AURA_K / Mathf.Max(cometReactionTime, 0.01f);
        Aura += auraGain;
        OnAuraChanged?.Invoke(auraGain);
        Debug.Log($"[Aura] Comet destroyed in {cometReactionTime:F2}s ！ +{auraGain:F0} | Aura: {Aura:F0}");



        ConsecutiveCorrectPredictions = 0;
        roundState = RoundState.Resolved; //change state

        Debug.Log($"[Round {roundIndex + 1}] Comet destroyed (backup) in {resolvedTime:F2}s");
    }


    public void OnCometHitShip()
    {
        if (roundState != RoundState.CometActive) return;

        float resolvedTime = Time.time - roundStartTime;
        RecordResult(SequenceOutcome.Hit, predictionClickTime, resolvedTime); //log time
        ConsecutiveCorrectPredictions = 0;
        CurrentHealth--;

        // --- Aura: big fixed penalty ---
        Aura -= COMET_HIT_PENALTY;
        OnAuraChanged?.Invoke(-COMET_HIT_PENALTY);
        Debug.Log($"[Aura] Comet hit ship ！ -{COMET_HIT_PENALTY} | Aura: {Aura:F0}");

        roundState = RoundState.Resolved; //change state

        Debug.Log($"[Round {roundIndex + 1}] COMET HIT ！ Health: {CurrentHealth}/{GameSettings.Instance.maxHealth}");
        OnHealthChanged?.Invoke();

        if (CurrentHealth <= 0)
        {
            EndGame();
        }
    }

    public void OnPlayerFailedPrediction() //called only by FailedPredictionBullet
    {
        Aura -= WRONG_PREDICTION_PENALTY;
        OnAuraChanged?.Invoke(-WRONG_PREDICTION_PENALTY);
        Debug.Log($"[Aura] Missed shot ！ -{MISSED_SHOT_PENALTY} | Aura: {Aura:F0}");
    }


    public void OnPlayerMissedShot() //called by regular bullets (not the failed prediction one)
    {



        //case 1: pass the catcher, correct quadrant, comet still alive


        //case 2: don't pass catcher, actual miss

            //case i: wrong prediction -> WRONG_PREDICTION_PENALTY

            //case ii: miss comet -> MISSED_SHOT_PENALTY


        if(roundState == RoundState.CometActive && currentQuadrant == playerController.GetPlayerQuadrant())
        {
            return;  //could still hit the comet, so ignore the hit
        }
        else //apply missed shot penalty
        {
            Aura -= MISSED_SHOT_PENALTY;
            OnAuraChanged?.Invoke(-MISSED_SHOT_PENALTY);
            ConsecutiveCorrectPredictions = 0;
            UpdateXPProgress();
            Debug.Log($"[Aura] Missed shot ！ -{MISSED_SHOT_PENALTY} | Aura: {Aura:F0}");
        }





        //if (roundState != RoundState.CometActive) return;
        //if (roundState == RoundState.Prediction) return;


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


    private IEnumerator DelayedSpawnComet(int quadrant) //after player misses
    {
        yield return new WaitForSeconds(GameSettings.Instance.cometSpawnDelay);
        SpawnComet(quadrant);
    }

    private void SpawnComet(int quadrant)
    {
        playerController.SetHasPredictedThisRound(true); //the first ammo after appearing is the regular

        cometSpawnTime = Time.time; //log down comet spawning time for scoring

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
                OnXPChanged?.Invoke(0f);
            }
        }
    }
    //update progress
    private void UpdateXPProgress()
    {
        Debug.Log($"[XP] UpdateXPProgress called. isCustomMode={GameSettings.Instance.isCustomMode}");

        if (GameSettings.Instance.isCustomMode) return;

        var preset = GameSettings.Instance.GetCurrentPreset();
        float progress = (float)ConsecutiveCorrectPredictions / preset.consecutiveCorrectToAdvance;
        Debug.Log($"[XP] Streak: {ConsecutiveCorrectPredictions}/{preset.consecutiveCorrectToAdvance} = {progress:F2}");
        OnXPChanged?.Invoke(Mathf.Clamp01(progress));
    }

    private void EndGame()
    {
        IsGameActive = false;
        sessionData.finalLevel = GameSettings.Instance.level;
        sessionData.totalRounds = sessionData.results.Count;
        starfieldEffect.SetActive(false);

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

    public void RestartGame()
    {
        // --- Stop everything running ---
        StopAllCoroutines();

        if (frequencyManager != null)
            frequencyManager.StopSequence();

        // --- Destroy any active comets ---
        // Tag your comet prefabs as "Comet" in the inspector for this to work
        foreach (var go in GameObject.FindGameObjectsWithTag("Comet"))
            Destroy(go);

        // --- Reset GameSettings level ---
        if (!GameSettings.Instance.isCustomMode)
            GameSettings.Instance.level = 0;

        // --- Reset all internal state ---
        CurrentHealth = GameSettings.Instance.maxHealth;
        Aura = 0f;
        ConsecutiveCorrectPredictions = 0;
        roundIndex = 0;
        roundState = RoundState.Prediction;
        predictedQuadrant = -1;
        predictionClickTime = -1f;
        cometSpawnTime = -1f;
        recentQuadrants.Clear();
        IsGameActive = false;

        // --- Notify UI to reset ---
        OnHealthChanged?.Invoke();
        OnAuraChanged?.Invoke(0f);       // delta 0, HUD reads Aura (which is 0)
        OnXPChanged?.Invoke(0f);         // bar to 0
        OnLevelAdvanced?.Invoke();       // HUD refreshes level display

        // --- Go ---
        StartGame();
    }


    //getter & setter
    public int GetCurrentHealth()
    {
        return CurrentHealth;
    }
    public void SetCurrentHealth(int newHealth)
    {
        CurrentHealth = newHealth;
    }

    public RoundState GetRoundState()
    {
        return roundState;
    }


    /// <summary>
    /// Grab session data any time (e.g. for Firebase upload).
    /// </summary>
    public GameSessionData GetSessionData() => sessionData;
}