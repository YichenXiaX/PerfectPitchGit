using UnityEngine;

public class PlayerController : MonoBehaviour
{
    Camera mainCamera;

    float worldHeight;
    float worldWidth;
    private float unitWidth;

    // In your game manager or wherever you handle correct answers
    public CelebrationVFX celebrationVFX;
    public LaserBeamVFX laserVFX;
    public GameObject bulletPrefab;
    public Transform firePoint;
    public AudioSource correctLaserSound; //Correct laser sound effect

    public TeleportVFX teleportVFX;







    private Vector3 targetPosition;  // The current destination
    private Vector3 startPosition;  // Where the movement starts
    


    private int currentQuadrant = -1;
    private bool hasPredictedThisRound = false;

    void Start()
    {
        mainCamera = Camera.main;

        if (GameSettings.Instance == null)
        {
            Debug.LogError("GameSettings.Instance is null in PlayerController!");
        }
        else
        {
            Debug.Log("GameSettings.Instance is ready.");
        }

        // Calculate world dimensions
        worldHeight = mainCamera.orthographicSize * 2;
        worldWidth = worldHeight * mainCamera.aspect;
        unitWidth = worldWidth / 8;

        // Initially set targetPosition to the current position
        targetPosition = transform.position;


        // Listen for new rounds to reset prediction lock
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart += HandleRoundStart;
        }
    }

    void Update()
    {
        HandleInput();
        HandleShoot();
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            Vector3 tempPosition = transform.position;
            tempPosition.x = unitWidth * 3;
            transform.position = tempPosition;
            MoveShip(tempPosition);
            currentQuadrant = 3;
        }
        else if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            Vector3 tempPosition = transform.position;
            tempPosition.x = unitWidth * 1;
            transform.position = tempPosition;
            MoveShip(tempPosition);
            currentQuadrant = 2;
        }
        else if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            Vector3 tempPosition = transform.position;
            tempPosition.x = unitWidth * -1;
            transform.position = tempPosition;
            MoveShip(tempPosition);
            currentQuadrant = 1;
        }
        else if (Input.GetKeyDown(KeyCode.P))
        {
            Vector3 tempPosition = transform.position;
            tempPosition.x = unitWidth * -3;
            transform.position = tempPosition;
            MoveShip(tempPosition);
            currentQuadrant = 0;
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart -= HandleRoundStart;
        }
    }

    private void HandleRoundStart(int quadrant)
    {
        hasPredictedThisRound = false;
    }

    public void HandleShoot()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsGameActive)
                return;

            // During prediction phase ¡ª one chance only
            if (!hasPredictedThisRound && currentQuadrant >= 0)
            {
                hasPredictedThisRound = true;
                GameManager.Instance.OnPlayerPrediction(currentQuadrant);
                Debug.Log($"[Player] Predicted Q{currentQuadrant + 1}");
            }
            else
            {
                SimpleShot();
            }
        }
    }

    public void FancyShot()
    {
        correctLaserSound.Play(); //play sound effect
        OnCorrectPrediction();
    }

    public void OnCorrectPrediction()
    {
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(
            Camera.main, firePoint.position
        );

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            celebrationVFX.canvas.GetComponent<RectTransform>(),
            screenPos,
            celebrationVFX.canvas.worldCamera,
            out Vector2 canvasPos
        );

        celebrationVFX.Play(canvasPos);

        laserVFX.Fire();
    }

    void MoveShip(Vector3 newPosition)
    {
        teleportVFX.Teleport(newPosition);
    }


    public void SimpleShot()
    {
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
    }


    //getters & setters
    public bool GetHasPredictedThisRound()
    {
        return hasPredictedThisRound;
    }
    public void SetHasPredictedThisRound(bool predicted)
    {
        hasPredictedThisRound = predicted;
    }
}