using UnityEngine;

public class MothershipMovement : MonoBehaviour
{
    [Header("Floating")]
    [SerializeField] float floatAmountX = 0.05f;
    [SerializeField] float floatAmountY = 0.08f;
    [SerializeField] float floatSpeedX = 0.7f;
    [SerializeField] float floatSpeedY = 1.0f;

    [Header("Hit Reaction")]
    [SerializeField] float hitShakeIntensity = 0.15f;
    [SerializeField] float hitShakeDuration = 0.4f;
    [SerializeField] float hitShakeFrequency = 25f;

    Vector3 originalPos;
    float shakeTimer;
    float currentShakeIntensity;

    void Start()
    {
        originalPos = transform.position;
    }

    void Update()
    {
        // --- Idle floating ---
        float offsetX = Mathf.Sin(Time.time * floatSpeedX) * floatAmountX;
        float offsetY = Mathf.Sin(Time.time * floatSpeedY) * floatAmountY
                       + Mathf.Sin(Time.time * floatSpeedY * 1.6f) * (floatAmountY * 0.3f); // layered for organic feel

        Vector3 floatPos = originalPos + new Vector3(offsetX, offsetY, 0f);

        // --- Hit shake (decays over time) ---
        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            currentShakeIntensity = Mathf.Lerp(0f, hitShakeIntensity, shakeTimer / hitShakeDuration);

            shakeOffset = new Vector3(
                Mathf.PerlinNoise(Time.time * hitShakeFrequency, 0f) * 2f - 1f,
                Mathf.PerlinNoise(0f, Time.time * hitShakeFrequency) * 2f - 1f,
                0f
            ) * currentShakeIntensity;
        }

        transform.position = floatPos + shakeOffset;
    }

    /// <summary>
    /// Call this from OnTriggerEnter2D when a comet hits.
    /// </summary>
    public void Hit()
    {
        shakeTimer = hitShakeDuration;
    }
}
