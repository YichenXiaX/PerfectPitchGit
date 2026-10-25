using UnityEngine;

public class radioactiveDamage : MonoBehaviour
{
    [Header("Tuning ¡ª ~33 HP total per comet if player does nothing")]
    public float baseDamagePerSecond;
    public float maxDamageMultiplier;

    private Transform mothershipTransform;
    private float maxDistance;
    private bool isActive = false;

    public void initialize(Transform mothership)
    {
        mothershipTransform = mothership;
        maxDistance = Vector3.Distance(transform.position, mothership.position);
        isActive = true;
    }

    void Update()
    {
        if (!isActive || mothershipTransform == null) return;
        if (gameManager.instance.currentHealth <= 0f) return;

        float currentDistance = Vector3.Distance(transform.position, mothershipTransform.position);
        float proximity = 1f - Mathf.Clamp01(currentDistance / maxDistance); // 0 at spawn, 1 at mothership

        float multiplier = 1f + (maxDamageMultiplier - 1f) * (proximity * proximity);
        float damage = baseDamagePerSecond * multiplier * Time.deltaTime;

        gameManager.instance.currentHealth = Mathf.Max(0f, gameManager.instance.currentHealth - damage);
    }

    public void stopDamage()
    {
        isActive = false;
    }
}