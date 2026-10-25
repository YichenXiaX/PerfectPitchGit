using UnityEngine;
using UnityEngine.UI;

public class healthBar : MonoBehaviour
{
    public static healthBar instance { get; private set; }

    [Header("UI References")]
    public Image fillImage;
    public Image backgroundImage;
    public Image glowImage;

    [Header("Colors")]
    public Color barColor = new Color(0f, 1f, 0.8f, 1f);
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.2f, 0.9f);

    private float maxHealth;
    private float displayedFill;
    private float glowPulseTimer;

    void Awake()
    {
        instance = this;
    }

    public void initialize(float max)
    {
        maxHealth = max;
        displayedFill = 1f;
        if (fillImage != null) fillImage.fillAmount = 1f;
        Debug.Log($"[healthBar] initialize called. maxHealth={maxHealth}, fillImage={fillImage}");
    }

    void Update()
    {
        if (maxHealth <= 0f) return;

        float targetFill = Mathf.Clamp01(gameManager.instance.currentHealth / maxHealth);
        displayedFill = Mathf.Lerp(displayedFill, targetFill, Time.deltaTime * 8f);

        if (fillImage != null)
        {
            fillImage.fillAmount = displayedFill;
            fillImage.color = barColor;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }

        if (glowImage != null)
        {
            if (targetFill < 0.3f && targetFill > 0f)
            {
                glowPulseTimer += Time.deltaTime * (4f + (1f - targetFill) * 6f);
                float alpha = Mathf.Abs(Mathf.Sin(glowPulseTimer)) * 0.5f;
                glowImage.color = new Color(1f, 0.2f, 0.2f, alpha);
                glowImage.enabled = true;
            }
            else
            {
                glowImage.enabled = false;
                glowPulseTimer = 0f;
            }
        }
    }
}