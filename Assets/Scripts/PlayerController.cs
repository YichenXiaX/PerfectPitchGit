using UnityEngine;

public class PlayerController : MonoBehaviour
{
    Camera mainCamera;

    float worldHeight;
    float worldWidth;
    private float unitWidth;

    public GameObject bulletPrefab;
    public Transform firePoint;

    private Vector3 targetPosition;  // The current destination
    private Vector3 startPosition;  // Where the movement starts
    private float moveDuration = 0.01f; // Time it should take to complete a move
    private float moveTimer = 0f;   // Keeps track of elapsed time for the current movement
    private bool isMoving = false; // Whether the player is moving

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
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            // change only the X axis
            Vector3 tempPosition = transform.position;
            tempPosition.x = unitWidth * 3;
            transform.position = tempPosition;
        }
        else if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            Vector3 tempPosition = transform.position;
            tempPosition.x = unitWidth * 1;
            transform.position = tempPosition;
        }
        else if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            Vector3 tempPosition = transform.position;
            tempPosition.x = unitWidth * -1;
            transform.position = tempPosition;
        }
        else if (Input.GetKeyDown(KeyCode.P))
        {
            Vector3 tempPosition = transform.position;
            tempPosition.x = unitWidth * -3;
            transform.position = tempPosition;
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        }
    }
}