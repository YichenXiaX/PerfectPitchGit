using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class gameManager : MonoBehaviour
{
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void logDiagnostic(string message) { Debug.Log(message); }

    public static gameManager instance { get; private set; }

    [Header("References")]
    public frequencyManager frequencyManager;
    public playerController playerController;
    [UnityEngine.Serialization.FormerlySerializedAs("OverlayObj")]
    public GameObject overlayObj;
    public GameObject starfieldEffect;
    public GameObject mothership;
    public toneCloudPlayer toneCloud;   // assign in Inspector
    public float cloudDuration = 2f;    // how long the cloud plays between loops
    //public auraHud auraHud;
    //auraHud hud = FindObjectOfType<auraHud>();

    // --- Aura tuning constants ---
    private const float predictionAuraK = 5000f;
    private const float cometDestroyAuraK = 500f;
    private const float wrongPredictionPenalty = 2000f;
    private const float missedShotPenalty = 300f;
    private const float cometHitPenalty = 2000f;


    // --- Public read-only state ---
    [UnityEngine.Serialization.FormerlySerializedAs("CurrentHealth")]
    public float currentHealth;
    public float aura { get; private set; }
    public int consecutiveCorrectPredictions { get; private set; }
    public bool isGameActive { get; private set; }

    // --- Second-guess tracking ---
    public bool isSecondGuessWindow { get; private set; }
    private int secondGuessQuadrant = -1;
    private bool secondGuessRecorded = false;

    // --- Events for other systems (comet spawner, UI, effects) ---
    public event Action<int> onCometSpawn;            // passes quadrant index
    public event Action<int> onPredictionSuccess;     // passes quadrant index
    public event Action onHealthChanged;
    public event Action<float> onAuraChanged;
    public event Action<float> onXpChanged;
    public event Action onGameStarted;
    public event Action<gameSessionData> onGameOver;
    public event Action onLevelAdvanced;
    public event Action<int> onRoundStart;            // passes quadrant index

    // --- Internal round state ---
    public enum roundStateType { prediction, cometActive, resolved }  // state manager
    public roundStateType roundState;

    private List<int> recentQuadrants = new List<int>();
    private int currentQuadrant;
    private int predictedQuadrant;
    private float roundStartTime;
    private int roundIndex;
    private float cometSpawnTime;
    //private float predictionDecisionTime = -1f;
    private float predictionClickTime = -1f;

    private gameSessionData sessionData;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }


    private void Start()
    {
        
    }

    private void Update()
    {
        checkHealth();
    }

    //kick off the game
    public void startGame()
    {
        if (isGameActive || cloudAccount.instance == null || cloudAccount.instance.busy) return;
        if (!frequencyManager.tryPrepareLoudness()) return;
        cloudAccount.instance.requestStart(beginGame);
    }

    private void beginGame()
    {
        if (isGameActive) return;
        if (!frequencyManager.tryPrepareLoudness()) return;

        Time.timeScale = 1f;
        var settings = gameSettings.instance;
        currentHealth = settings.maxHealth;
        //healthBar.instance.initialize(currentHealth);

        healthBar.instance.initialize(settings.maxHealth);

        consecutiveCorrectPredictions = 0;
        roundIndex = 0;
        recentQuadrants.Clear();
        aura = 0f;
        isGameActive = true;
        overlayObj.SetActive(false);
        starfieldEffect.SetActive(true);

        sessionData = new gameSessionData
        {
            playerName = settings.playerName,
            sessionId = cloudAccount.instance.activeSessionId,
            sessionStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            results = new List<sequenceResult>()
        };

        logDiagnostic($"[gameManager] Game started �� Player: {settings.playerName}, Level: {settings.level}");
        onGameStarted?.Invoke();
        StartCoroutine(gameLoop());
    }

    // ------------------------------------------------------------------
    //  Core game loop
    // ------------------------------------------------------------------
    private IEnumerator gameLoop()
    {
        while (currentHealth > 0)
        {
            if (!frequencyManager.tryPrepareLoudness())
            {
                endGame();
                yield break;
            }
            var preset = gameSettings.instance.getCurrentPreset();

            // 1 �� Pick a quadrant (no 3+ identical in a row)
            currentQuadrant = pickQuadrant();
            recentQuadrants.Add(currentQuadrant);
            if (recentQuadrants.Count > 2)
                recentQuadrants.RemoveAt(0);

            // 2 �� Reset round
            roundState = roundStateType.prediction;
            predictedQuadrant = -1;
            roundStartTime = Time.time;
            predictionClickTime = -1f;
            cometSpawnTime = -1f;
            isSecondGuessWindow = false;
            secondGuessQuadrant = -1;
            secondGuessRecorded = false;


            logDiagnostic($"[Round {roundIndex + 1}] Q{currentQuadrant + 1} | Level {gameSettings.instance.level}");
            onRoundStart?.Invoke(currentQuadrant);

            // 3 �� Play note sequence
            frequencyManager.playNoteSequence(currentQuadrant);

            // 4 �� Prediction window = sequence length + preSpawnDelay
            //float sequenceDuration = preset.notesPerSequence
            //                         * (preset.noteDuration + preset.pauseBetweenNotes);
            //float predictionWindow = sequenceDuration + preset.preSpawnDelay;

            float predictionWindow = preset.predictionWindow;

            float elapsed = 0f;
            while (elapsed < predictionWindow && roundState == roundStateType.prediction)
            {
                elapsed += Time.deltaTime;
                yield return null;  // �� pauses here, waits one frame, then loops again
            }

            // Correct prediction already resolved the round
            if (roundState == roundStateType.resolved)
            {
                frequencyManager.stopSequence();

                toneCloud.playToneCloud(cloudDuration);
                yield return new WaitForSeconds(cloudDuration + preset.pauseBetweenRounds);

                roundIndex++;
                continue;
            }

            // 5 �� No (correct) prediction �� spawn comet
            if (roundState == roundStateType.prediction)
            {
                // Timed out with no input at all
                roundState = roundStateType.cometActive;
                //frequencyManager.stopSequence();
                consecutiveCorrectPredictions = 0;  
                updateXpProgress(); //�� reset bar when player didn't even try
                logDiagnostic($"[Round {roundIndex + 1}] No prediction �� spawning comet");
                spawnComet(currentQuadrant);
                //onCometSpawn?.Invoke(currentQuadrant);
            }
            // If roundState is already cometActive, a wrong prediction triggered the spawn



            while (roundState == roundStateType.cometActive)
            {
                yield return null;
            }

            if (currentHealth <= 0)
            {
                yield break;
            }

            toneCloud.playToneCloud(cloudDuration);
            yield return new WaitForSeconds(cloudDuration + preset.pauseBetweenRounds);
            roundIndex++;
        }
    }

    // ------------------------------------------------------------------
    //  Player input hooks
    // ------------------------------------------------------------------


    // The player picks a quadrant before the comet spawns.
    // Called extenally
    public void onPlayerPrediction(int quadrant)
    {
        if (roundState != roundStateType.prediction) return;

        float decisionTime = Time.time - roundStartTime;
        predictedQuadrant = quadrant;

        if (quadrant == currentQuadrant) //correct
        {
            predictionClickTime = decisionTime;
            recordResult(sequenceOutcome.predicted, predictionClickTime, decisionTime); //log time
            consecutiveCorrectPredictions++;
            roundState = roundStateType.resolved;


            // --- Aura: big speed-based gain ---
            float auraGain = predictionAuraK / Mathf.Max(decisionTime, 0.01f);
            aura += auraGain;
            onAuraChanged?.Invoke(auraGain);
            logDiagnostic($"[Aura] Correct prediction in {decisionTime:F2}s �� +{auraGain:F0} | Aura: {aura:F0}");

            //auraHud.spawnFloatingText(50);

            updateXpProgress();

            playerController.fancyShot();

            //hud.AddAura(5000);

            logDiagnostic($"[Round {roundIndex + 1}] CORRECT prediction Q{quadrant + 1} " +
                      $"in {decisionTime:F2}s �� Streak: {consecutiveCorrectPredictions}");
            onPredictionSuccess?.Invoke(currentQuadrant);
            checkLevelAdvancement();
        }
        else //incorrect
        {
            predictionClickTime = decisionTime;
            consecutiveCorrectPredictions = 0;
            roundState = roundStateType.cometActive;

            // --- Aura
            //aura -= wrongPredictionPenalty;
            //onAuraChanged?.Invoke(-wrongPredictionPenalty);
            //logDiagnostic($"[Aura] Wrong prediction �� -{wrongPredictionPenalty} | Aura: {aura:F0}");


            playerController.failedPredictionShot();
            //frequencyManager.stopSequence();

            updateXpProgress();

            logDiagnostic($"[Round {roundIndex + 1}] WRONG �� guessed Q{quadrant + 1}, " +
                      $"actual Q{currentQuadrant + 1} �� spawning comet");


            StartCoroutine(delayedSpawnComet(currentQuadrant));
        }
    }



    /// The player destroyed the comet with backup ammo.
    public void onCometDestroyedByPlayer()
    {
        if (roundState != roundStateType.cometActive) return;

        float resolvedTime = Time.time - roundStartTime;
        recordResult(sequenceOutcome.destroyed, predictionClickTime, resolvedTime); //log time

        frequencyManager.stopSequence(); //stops the sequence


        // --- Aura: small speed-based gain from comet spawn ---
        float cometReactionTime = Time.time - cometSpawnTime;
        float auraGain = cometDestroyAuraK / Mathf.Max(cometReactionTime, 0.01f);
        aura += auraGain;
        onAuraChanged?.Invoke(auraGain);
        logDiagnostic($"[Aura] Comet destroyed in {cometReactionTime:F2}s �� +{auraGain:F0} | Aura: {aura:F0}");



        consecutiveCorrectPredictions = 0;
        roundState = roundStateType.resolved; //change state

        logDiagnostic($"[Round {roundIndex + 1}] Comet destroyed (backup) in {resolvedTime:F2}s");
    }


    public void onCometHitShip()
    {
        if (roundState != roundStateType.cometActive) return;

        float resolvedTime = Time.time - roundStartTime;
        recordResult(sequenceOutcome.hit, predictionClickTime, resolvedTime); //log time
        consecutiveCorrectPredictions = 0;
        currentHealth -= 100;

        // --- Aura: big fixed penalty ---
        aura -= cometHitPenalty;
        onAuraChanged?.Invoke(-cometHitPenalty);
        logDiagnostic($"[Aura] Comet hit ship �� -{cometHitPenalty} | Aura: {aura:F0}");

        roundState = roundStateType.resolved; //change state

        logDiagnostic($"[Round {roundIndex + 1}] COMET HIT �� Health: {currentHealth}/{gameSettings.instance.maxHealth}");
        onHealthChanged?.Invoke();

        if (currentHealth <= 0)
        {
            frequencyManager.stopSequence();
            endGame();
        }
    }

    public void onPlayerFailedPrediction() //called only by failedPredictionBullet
    {
        aura -= wrongPredictionPenalty;
        onAuraChanged?.Invoke(-wrongPredictionPenalty);
        logDiagnostic($"[Aura] Missed shot �� -{missedShotPenalty} | Aura: {aura:F0}");
    }


    public void onPlayerMissedShot() //called by regular bullets (not the failed prediction one)
    {



        //case 1: pass the catcher, correct quadrant, comet still alive


        //case 2: don't pass catcher, actual miss

            //case i: wrong prediction -> wrongPredictionPenalty

            //case ii: miss comet -> missedShotPenalty


        if(roundState == roundStateType.cometActive && currentQuadrant == playerController.getPlayerQuadrant())
        {
            return;  //could still hit the comet, so ignore the hit
        }
        else //apply missed shot penalty
        {
            aura -= missedShotPenalty;
            onAuraChanged?.Invoke(-missedShotPenalty);
            consecutiveCorrectPredictions = 0;
            updateXpProgress();
            logDiagnostic($"[Aura] Missed shot �� -{missedShotPenalty} | Aura: {aura:F0}");
        }





        //if (roundState != roundStateType.cometActive) return;
        //if (roundState == roundStateType.prediction) return;


    }

    // ------------------------------------------------------------------
    //  Internals
    // ------------------------------------------------------------------

    private int pickQuadrant()
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


    //private IEnumerator delayedSpawnComet(int quadrant) //after player misses
    //{
    //    yield return new WaitForSeconds(gameSettings.instance.cometSpawnDelay); //in this delay, we check if players make meaningful second guesses
    //    spawnComet(quadrant); //comet spawns
    //}

    private IEnumerator delayedSpawnComet(int quadrant)
    {
        // Open the second-guess window
        isSecondGuessWindow = true;
        secondGuessQuadrant = -1;
        secondGuessRecorded = false;

        yield return new WaitForSeconds(gameSettings.instance.getCurrentPreset().cometSpawnDelay);

        // Close the window
        isSecondGuessWindow = false;

        spawnComet(quadrant);
    }


    public void onSecondGuess(int quadrant)
    {
        if (!isSecondGuessWindow || secondGuessRecorded) return;

        secondGuessQuadrant = quadrant;
        secondGuessRecorded = true;

        bool correct = (quadrant == currentQuadrant);

        logDiagnostic($"[Round {roundIndex + 1}] Second guess: Q{quadrant + 1} �� " +
                  $"{(correct ? "CORRECT" : "WRONG")} (actual Q{currentQuadrant + 1})");

        // TODO: record secondGuessQuadrant and correct into your sequenceResult / session data
        // e.g. you could add fields to sequenceResult like:
        //   public int secondGuessQuadrant;
        //   public bool secondGuessCorrect;
        // and write them in recordResult or at comet resolution time
    }


    private void spawnComet(int quadrant)
    {
        playerController.setHasPredictedThisRound(true); //the first ammo after appearing is the regular

        cometSpawnTime = Time.time; //log down comet spawning time for scoring

        float worldHeight = Camera.main.orthographicSize * 2;
        float worldWidth = worldHeight * Camera.main.aspect;
        float unitWidth = worldWidth / 8;

        // Same x mapping as playerController: quadrant 0 = -3, 1 = -1, 2 = 1, 3 = 3
        float x = unitWidth * (-3 + quadrant * 2);

        Vector3 spawnPos = new Vector3(x, gameSettings.instance.cometSpawnY, 0f);
        var cometInstance = Instantiate(gameSettings.instance.cometPrefabs[quadrant], spawnPos, Quaternion.identity);

        var radioDamage = cometInstance.GetComponent<radioactiveDamage>();
        if (radioDamage != null)
        {
            radioDamage.initialize(mothership.transform); // pass the mothership, not the player
        }
    }

    private void recordResult(sequenceOutcome outcome, float predictionTime, float resolvedTime)
    {
        var result = new sequenceResult
        {
            roundIndex = this.roundIndex,
            correctQuadrant = currentQuadrant,
            predictedQuadrant = predictedQuadrant,
            secondGuessQuadrant = this.secondGuessQuadrant,
            level = gameSettings.instance.level,
            predictionTime = predictionTime,   // CHANGED �� was decisionTime
            resolvedTime = resolvedTime,       // ADDED
            // Keep saved session labels compatible with existing records.
            outcome = outcome switch
            {
                sequenceOutcome.predicted => "Predicted",
                sequenceOutcome.destroyed => "Destroyed",
                sequenceOutcome.hit => "Hit",
                _ => outcome.ToString()
            },
            audioLoudness = frequencyManager.createLoudnessRecord(),
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        sessionData.results.Add(result);

        string predLabel = result.predictedQuadrant >= 0
            ? $"Q{result.predictedQuadrant + 1}"
            : "None";

        logDiagnostic($"[Data] Round {result.roundIndex + 1} | " +
                  $"Correct: Q{result.correctQuadrant + 1} | " +
                  $"Predicted: {predLabel} | " +
                  $"Level: {result.level} | " +
                  $"Outcome: {result.outcome} | " +
                  $"PredTime: {result.predictionTime:F2}s | " +
                  $"ResolvedTime: {result.resolvedTime:F2}s");
    }

    //advance level
    private void checkLevelAdvancement()
    {
        if (!gameSettings.instance.isCustomMode) { //only advance if not in custom mode
            var preset = gameSettings.instance.getCurrentPreset();

            if (consecutiveCorrectPredictions >= preset.consecutiveCorrectToAdvance)
            {
                gameSettings.instance.level++;
                consecutiveCorrectPredictions = 0;
                logDiagnostic($"[gameManager] LEVEL UP �� {gameSettings.instance.level}");
                onLevelAdvanced?.Invoke();
                onXpChanged?.Invoke(0f);
            }
        }
    }
    //update progress
    private void updateXpProgress()
    {
        logDiagnostic($"[XP] updateXpProgress called. isCustomMode={gameSettings.instance.isCustomMode}");

        if (gameSettings.instance.isCustomMode) return;

        var preset = gameSettings.instance.getCurrentPreset();
        float progress = (float)consecutiveCorrectPredictions / preset.consecutiveCorrectToAdvance;
        logDiagnostic($"[XP] Streak: {consecutiveCorrectPredictions}/{preset.consecutiveCorrectToAdvance} = {progress:F2}");
        onXpChanged?.Invoke(Mathf.Clamp01(progress));
    }

    private void stopRoundActivity()
    {
        StopAllCoroutines();

        if (frequencyManager != null)
        {
            // Stop any note fades as well as the sequence itself.
            frequencyManager.StopAllCoroutines();
            frequencyManager.stopSequence();
        }

        if (toneCloud != null)
            toneCloud.stopCloud();
    }

    private void checkHealth()
    {
        if (isGameActive && currentHealth <= 0)
            endGame();
    }

    private void endGame()
    {
        if (!isGameActive) return;

        isGameActive = false;
        isSecondGuessWindow = false;
        roundState = roundStateType.resolved;
        stopRoundActivity();
        Time.timeScale = 0f;
        sessionData.finalLevel = gameSettings.instance.level + 1;
        sessionData.totalRounds = sessionData.results.Count;
        starfieldEffect.SetActive(false);

        int predicted = 0, destroyed = 0, hit = 0;
        int secondGuessAttempts = 0, secondGuessCorrect = 0;

        foreach (var r in sessionData.results)
        {
            if (r.outcome == "Predicted") predicted++;
            else if (r.outcome == "Destroyed") destroyed++;
            else if (r.outcome == "Hit") hit++;

            // TODO: check r.secondGuessQuadrant or however you store it
            // A value of -1 means no second guess was made
            if (r.secondGuessQuadrant >= 0)
            {
                secondGuessAttempts++;
                if (r.secondGuessQuadrant == r.correctQuadrant) secondGuessCorrect++;
            }
        }

        sessionData.totalPredictions = predicted;
        sessionData.totalDestroys = destroyed;
        sessionData.totalHits = hit;

        float secondGuessRate = secondGuessAttempts > 0
            ? (float)secondGuessCorrect / secondGuessAttempts * 100f
            : 0f;

        sessionData.secondGuessSuccessRate = secondGuessRate;

        logDiagnostic("=== GAME OVER ===");
        logDiagnostic($"Player: {sessionData.playerName} | Final Level: {sessionData.finalLevel}");
        logDiagnostic($"Rounds: {sessionData.totalRounds} | " +
                  $"Predicted: {predicted} | Destroyed: {destroyed} | Hit: {hit}");
        logDiagnostic($"Second Guesses: {secondGuessCorrect}/{secondGuessAttempts} ({secondGuessRate:F1}%)");

        onGameOver?.Invoke(sessionData);
    }

    public void restartGame()
    {
        if (!frequencyManager.tryPrepareLoudness()) return;
        stopRoundActivity();

        // Deactivate old hazards before deferred destruction so they cannot
        // damage the new session or resolve its first round.
        foreach (var go in GameObject.FindGameObjectsWithTag("Comet"))
        {
            go.SetActive(false);
            Destroy(go);
        }

        foreach (var bullet in FindObjectsByType<bulletBehavior>(FindObjectsSortMode.None))
        {
            bullet.gameObject.SetActive(false);
            Destroy(bullet.gameObject);
        }

        foreach (var bullet in FindObjectsByType<failedPredictionBullet>(FindObjectsSortMode.None))
        {
            bullet.gameObject.SetActive(false);
            Destroy(bullet.gameObject);
        }

        // --- Reset gameSettings level ---
        if (!gameSettings.instance.isCustomMode)
            gameSettings.instance.level = 0;

        // --- Reset all internal state ---
        currentHealth = gameSettings.instance.maxHealth;
        //healthBar.instance.initialize(currentHealth);
        aura = 0f;
        consecutiveCorrectPredictions = 0;
        roundIndex = 0;
        roundState = roundStateType.prediction;
        predictedQuadrant = -1;
        predictionClickTime = -1f;
        cometSpawnTime = -1f;
        recentQuadrants.Clear();
        isGameActive = false;
        isSecondGuessWindow = false;
        secondGuessQuadrant = -1;
        secondGuessRecorded = false;

        // --- Notify UI to reset ---
        onHealthChanged?.Invoke();
        onAuraChanged?.Invoke(0f);       // delta 0, HUD reads aura (which is 0)
        onXpChanged?.Invoke(0f);         // bar to 0
        onLevelAdvanced?.Invoke();       // HUD refreshes level display

        // --- Go ---
        startGame();
    }


    //getter & setter
    public float getCurrentHealth()
    {
        return currentHealth;
    }
    public void setCurrentHealth(int newHealth)
    {
        currentHealth = newHealth;
    }

    public roundStateType getRoundState()
    {
        return roundState;
    }


    /// <summary>
    /// Grab session data any time (e.g. for Firebase upload).
    /// </summary>
    public gameSessionData getSessionData() => sessionData;
}

