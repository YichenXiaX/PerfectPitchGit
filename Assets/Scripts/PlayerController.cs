using UnityEngine;

public class PlayerController : MonoBehaviour
{
    Camera mainCamera;

    float worldHeight;
    float worldWidth;
    private float unitWidth;

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

        // Handle movement if currently moving
        if (isMoving)
        {
            UpdateMovement();
        }
    }

    void HandleInput()
    {
        // Check for key inputs and set a new target position
        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            StartMoving(new Vector3(unitWidth * 3, transform.position.y, transform.position.z));
        }
        else if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            StartMoving(new Vector3(unitWidth * 1, transform.position.y, transform.position.z));
        }
        else if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            StartMoving(new Vector3(unitWidth * -1, transform.position.y, transform.position.z));
        }
        else if (Input.GetKeyDown(KeyCode.P))
        {
            StartMoving(new Vector3(unitWidth * -3, transform.position.y, transform.position.z));
        }
    }

    void StartMoving(Vector3 newTargetPosition)
    {
        // Interrupt the current movement and start a new one
        startPosition = transform.position;   // Update the startPosition to the current position
        targetPosition = newTargetPosition;  // Set the new target position
        moveTimer = 0f;                      // Reset the timer
        isMoving = true;                     // Make sure we're moving
    }

    void UpdateMovement()
    {
        // Increment the timer
        moveTimer += Time.deltaTime;

        // Calculate the interpolation factor
        float t = moveTimer / moveDuration;

        // Smooth interpolation for natural movement
        t = Mathf.SmoothStep(0, 1, t);

        // Lerp to the target position
        transform.position = Vector3.Lerp(startPosition, targetPosition, t);

        // Stop moving once the movement is complete
        if (moveTimer >= moveDuration)
        {
            isMoving = false;
        }
    }
}