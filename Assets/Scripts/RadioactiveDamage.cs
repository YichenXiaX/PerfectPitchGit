using UnityEngine;

public class RadioactiveDamage : MonoBehaviour
{
    [Header("Tuning ¡ª ~33 HP total per comet if player does nothing")]
    public float baseDamagePerSecond;
    public float maxDamageMultiplier;

    private Transform mothershipTransform;
    private float maxDistance;
    private bool isActive = false;

    public void Initialize(Transform mothership)
    {
        mothershipTransform = mothership;
        maxDistance = Vector3.Distance(transform.position, mothership.position);
        isActive = true;
    }

    void Update()
    {
        if (!isActive || mothershipTransform == null) return;
        if (GameManager.Instance.CurrentHealth <= 0f) return;

        float currentDistance = Vector3.Distance(transform.position, mothershipTransform.position);
        float proximity = 1f - Mathf.Clamp01(currentDistance / maxDistance); // 0 at spawn, 1 at mothership

        float multiplier = 1f + (maxDamageMultiplier - 1f) * (proximity * proximity);
        float damage = baseDamagePerSecond * multiplier * Time.deltaTime;

        GameManager.Instance.CurrentHealth = Mathf.Max(0f, GameManager.Instance.CurrentHealth - damage);
    }

    public void StopDamage()
    {
        isActive = false;
    }
}