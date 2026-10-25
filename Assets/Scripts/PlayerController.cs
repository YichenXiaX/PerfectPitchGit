using UnityEngine;

public class playerController : MonoBehaviour
{
    Camera mainCamera;

    float worldHeight;
    float worldWidth;
    private float unitWidth;

    // In your game manager or wherever you handle correct answers
    [UnityEngine.Serialization.FormerlySerializedAs("celebrationVFX")]
    public celebrationVfx celebrationVfx;
    [UnityEngine.Serialization.FormerlySerializedAs("laserVFX")]
    public laserBeamVfx laserVfx;
    public GameObject bulletPrefab;
    public GameObject failedPredictionBulletPrefab; //for the single failed prediction shot
    public Transform firePoint;
    public AudioSource correctLaserSound; //Correct laser sound effect
    public AudioSource normalLaserSound; //Normal laser sound effect

    [UnityEngine.Serialization.FormerlySerializedAs("teleportVFX")]
    public teleportVfx teleportVfx;




    private Vector3 targetPosition;  // The current destination
    private Vector3 startPosition;  // Where the movement starts
    


    private int currentQuadrant = -1;
    private bool hasPredictedThisRound = false;

    void Start()
    {

        mainCamera = Camera.main;

        if (gameSettings.instance == null)
        {
            Debug.LogError("gameSettings.instance is null in playerController!");
        }
        else
        {
            Debug.Log("gameSettings.instance is ready.");
        }

        // Calculate world dimensions
        worldHeight = mainCamera.orthographicSize * 2;
        worldWidth = worldHeight * mainCamera.aspect;
        unitWidth = worldWidth / 8;

        // Initially set targetPosition to the current position
        targetPosition = transform.position;


        // Listen for new rounds to reset prediction lock
        if (gameManager.instance != null)
        {
            gameManager.instance.onRoundStart += handleRoundStart;
        }
    }

    void Update()
    {
        handleInput();
        handleShoot();
    }

    void handleInput()
    {
        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            Vector3 tempPosition = transform.position;
            tempPosition.x = unitWidth * 3;
            transform.position = tempPosition;
            moveShip(tempPosition);
            currentQuadrant = 3;
        }
        else if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            Vector3 tempPosition = transform.position;
            tempPosition.x = unitWidth * 1;
            transform.position = tempPosition;
            moveShip(tempPosition);
            currentQuadrant = 2;
        }
        else if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            Vector3 tempPosition = transform.position;
            tempPosition.x = unitWidth * -1;
            transform.position = tempPosition;
            moveShip(tempPosition);
            currentQuadrant = 1;
        }
        else if (Input.GetKeyDown(KeyCode.P))
        {
            Vector3 tempPosition = transform.position;
            tempPosition.x = unitWidth * -3;
            transform.position = tempPosition;
            moveShip(tempPosition);
            currentQuadrant = 0;
        }
    }

    void OnDestroy()
    {
        if (gameManager.instance != null)
        {
            gameManager.instance.onRoundStart -= handleRoundStart;
        }
    }

    private void handleRoundStart(int quadrant)
    {
        hasPredictedThisRound = false;
    }

    private bool isSelectedQuadrantKeyHeld()
    {
        switch (currentQuadrant)
        {
            case 0: return Input.GetKey(KeyCode.P);
            case 1: return Input.GetKey(KeyCode.LeftBracket);
            case 2: return Input.GetKey(KeyCode.RightBracket);
            case 3: return Input.GetKey(KeyCode.Backslash);
            default: return false;
        }
    }

    public void handleShoot()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (gameManager.instance == null || !gameManager.instance.isGameActive)
                return;

            if (!isSelectedQuadrantKeyHeld())
                return;

            // During prediction phase ¡ª one chance only
            if (!hasPredictedThisRound && currentQuadrant >= 0)
            {
                hasPredictedThisRound = true;
                gameManager.instance.onPlayerPrediction(currentQuadrant);
                Debug.Log($"[Player] Predicted Q{currentQuadrant + 1}");
            }
            else
            {
                simpleShot();
                // --- Second-guess check ---
                if (gameManager.instance.isSecondGuessWindow)
                {
                    gameManager.instance.onSecondGuess(currentQuadrant);
                }
            }
        }
    }

    public void fancyShot()
    {
        correctLaserSound.Play(); //play sound effect
        onCorrectPrediction();
    }

    public void onCorrectPrediction()
    {
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(
            Camera.main, firePoint.position
        );

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            celebrationVfx.canvas.GetComponent<RectTransform>(),
            screenPos,
            celebrationVfx.canvas.worldCamera,
            out Vector2 canvasPos
        );

        celebrationVfx.play(canvasPos);

        laserVfx.fire();
    }

    void moveShip(Vector3 newPosition)
    {
        teleportVfx.teleport(newPosition);
    }


    public void failedPredictionShot() //calls special bullet prefab
    {
        GameObject failedPredictionBullet = Instantiate(failedPredictionBulletPrefab, firePoint.position, firePoint.rotation);
        normalLaserSound.Play();
    }

    public void simpleShot()
    {
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        normalLaserSound.Play();
    }


    //getters & setters
    public bool getHasPredictedThisRound()
    {
        return hasPredictedThisRound;
    }
    public void setHasPredictedThisRound(bool predicted)
    {
        hasPredictedThisRound = predicted;
    }

    public int getPlayerQuadrant()
    {
        return currentQuadrant;
    }
}