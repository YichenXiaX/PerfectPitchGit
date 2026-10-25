using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class auraHud : MonoBehaviour
{
    [Header("UI References (assign in inspector)")]
    public TextMeshProUGUI auraText;
    public TextMeshProUGUI auraLabel;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI levelLabel;
    public Image xpBarFill;
    public RectTransform auraContainer;
    public RectTransform levelBadge;

    [Header("Floating Text")]
    public GameObject floatingTextPrefab;   // a TMP_Text + CanvasGroup on a RectTransform
    public Transform floatingTextParent;    // parent canvas or panel

    [Header("Colors")]
    public Color auraColor = new Color(0.4f, 0.7f, 1f);
    public Color levelColor = new Color(0.85f, 0.9f, 1f);
    public Color xpBarColor = new Color(0.3f, 0.6f, 1f);
    public Color flashColor = Color.white;
    public Color gainFlashColor = new Color(0.2f, 1f, 0.4f);   // green
    public Color lossFlashColor = new Color(1f, 0.2f, 0.2f);   // red

    [Header("Animation")]
    public float countUpSpeed = 8f;
    public float punchScale = 1.25f;
    public float punchDuration = 0.15f;
    public float flashDuration = 0.35f;
    public float floatingTextRiseDistance = 60f;
    public float floatingTextDuration = 1f;

    int displayedAura = 0;
    float targetAura = 0;
    int currentLevel = -1;
    float currentXpPercent = 0f;
    float displayedXp = 0f;
    Vector3 originalAuraScale;
    Vector3 originalBadgeScale;
    Color originalAuraTextColor;

    void Start()
    {
        Debug.Log($"[auraHud] xpBarFill is {(xpBarFill == null ? "NULL" : "assigned")}");

        if (auraContainer != null)
            originalAuraScale = auraContainer.localScale;

        if (levelBadge != null)
            originalBadgeScale = levelBadge.localScale;

        if (auraText != null)
        {
            originalAuraTextColor = auraText.color;
            auraText.text = "0";
        }

        if (gameSettings.instance != null)
            setLevel(gameSettings.instance.level);
        else
            setLevel(0);

        // Subscribe to the delta event
        if (gameManager.instance != null) {
            gameManager.instance.onAuraChanged += handleAuraChanged;
            gameManager.instance.onLevelAdvanced += handleLevelUp;
            gameManager.instance.onXpChanged += handleXpChanged;
        }
    }

    void OnDestroy()
    {
        if (gameManager.instance != null)
        {
            gameManager.instance.onAuraChanged -= handleAuraChanged;
            gameManager.instance.onLevelAdvanced -= handleLevelUp;
            gameManager.instance.onXpChanged -= handleXpChanged;
        }
    }

    void Update()
    {
        // Smooth number roll toward target
        if (displayedAura != Mathf.RoundToInt(targetAura))
        {
            float next = Mathf.MoveTowards(displayedAura, targetAura,
                         Time.deltaTime * countUpSpeed * Mathf.Max(100f, Mathf.Abs(targetAura - displayedAura)));
            displayedAura = Mathf.RoundToInt(next);

            if (auraText != null)
                auraText.text = displayedAura.ToString();
        }

        // Smooth XP bar fill
        if (Mathf.Abs(displayedXp - currentXpPercent) > 0.001f)
        {
            displayedXp = Mathf.Lerp(displayedXp, currentXpPercent, Time.deltaTime * countUpSpeed);
            if (xpBarFill != null)
                xpBarFill.fillAmount = displayedXp;
        }
    }


    private void handleLevelUp()
    {
        setLevel(gameSettings.instance.level);
    }

    private void handleXpChanged(float progress)
    {
        Debug.Log($"[auraHud] handleXpChanged received: {progress:F2}");
        setXpPercent(progress);
    }

    // ------------------------------------------------------------------
    //  Called by gameManager via onAuraChanged (now passes delta)
    // ------------------------------------------------------------------
    private void handleAuraChanged(float delta)
    {
        targetAura = gameManager.instance.aura;

        // Punch scale on the aura container
        StopCoroutine(nameof(punchAuraScale));
        StartCoroutine(punchAuraScale());

        // Flash the aura text color based on gain/loss
        StopCoroutine(nameof(flashAuraText));
        Color flash = delta > 0 ? gainFlashColor : lossFlashColor;
        StartCoroutine(flashAuraText(flash));

        // Floating "+500" or "−2000"
        spawnFloatingText(delta);
    }

    // ------------------------------------------------------------------
    //  Public methods for level / XP updates (called however you like)
    // ------------------------------------------------------------------
    public void setLevel(int level)
    {
        if (level != currentLevel)
        {
            currentLevel = level;
            if (levelText != null) levelText.text = (level + 1).ToString();

            StopCoroutine(nameof(punchBadgeScale));
            StartCoroutine(punchBadgeScale());
        }
    }

    public void setXpPercent(float percent)
    {
        currentXpPercent = Mathf.Clamp01(percent);
    }

    // ------------------------------------------------------------------
    //  Animations
    // ------------------------------------------------------------------
    private IEnumerator punchAuraScale()
    {
        if (auraContainer == null) yield break;

        Vector3 target = originalAuraScale * punchScale;
        float half = punchDuration * 0.5f;

        // Scale up
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            auraContainer.localScale = Vector3.Lerp(originalAuraScale, target, t / half);
            yield return null;
        }

        // Scale back down
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            auraContainer.localScale = Vector3.Lerp(target, originalAuraScale, t / half);
            yield return null;
        }

        auraContainer.localScale = originalAuraScale;
    }

    private IEnumerator punchBadgeScale()
    {
        if (levelBadge == null) yield break;

        Vector3 target = originalBadgeScale * punchScale;
        float half = punchDuration * 0.5f;

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            levelBadge.localScale = Vector3.Lerp(originalBadgeScale, target, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            levelBadge.localScale = Vector3.Lerp(target, originalBadgeScale, t / half);
            yield return null;
        }

        levelBadge.localScale = originalBadgeScale;
    }

    private IEnumerator flashAuraText(Color flash)
    {
        if (auraText == null) yield break;

        auraText.color = flash;
        float t = 0f;
        while (t < flashDuration)
        {
            t += Time.deltaTime;
            auraText.color = Color.Lerp(flash, originalAuraTextColor, t / flashDuration);
            yield return null;
        }
        auraText.color = originalAuraTextColor;
    }

    private void spawnFloatingText(float delta)
    {
        if (floatingTextPrefab == null || floatingTextParent == null) return;

        GameObject go = Instantiate(floatingTextPrefab, floatingTextParent);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        var cg = go.GetComponent<CanvasGroup>();

        if (tmp != null)
        {
            tmp.text = delta > 0 ? $"+{delta:F0}" : $"{delta:F0}";
            tmp.color = delta > 0 ? gainFlashColor : lossFlashColor;
        }

        StartCoroutine(animateFloatingText(go, cg));
    }

    private IEnumerator animateFloatingText(GameObject go, CanvasGroup cg)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        Vector2 startPos = rt.anchoredPosition;
        float t = 0f;

        while (t < floatingTextDuration)
        {
            t += Time.deltaTime;
            float progress = t / floatingTextDuration;

            rt.anchoredPosition = startPos + Vector2.up * floatingTextRiseDistance * progress;

            if (cg != null)
                cg.alpha = 1f - progress;

            yield return null;
        }

        Destroy(go);
    }

    public void refreshFromSettings()
    {
        if (gameSettings.instance != null)
        {
            currentLevel = -1;  // force update

            if (gameSettings.instance.isCustomMode)
            {
                if (levelText != null) levelText.text = "X";
                StartCoroutine(punchBadgeScale());
            }
            else
            {
                setLevel(gameSettings.instance.level);
            }
        }
    }
}