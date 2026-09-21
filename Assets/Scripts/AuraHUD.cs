using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AuraHUD : MonoBehaviour
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
    float currentXPPercent = 0f;
    float displayedXP = 0f;
    Vector3 originalAuraScale;
    Vector3 originalBadgeScale;
    Color originalAuraTextColor;

    void Start()
    {
        Debug.Log($"[AuraHUD] xpBarFill is {(xpBarFill == null ? "NULL" : "assigned")}");

        if (auraContainer != null)
            originalAuraScale = auraContainer.localScale;

        if (levelBadge != null)
            originalBadgeScale = levelBadge.localScale;

        if (auraText != null)
        {
            originalAuraTextColor = auraText.color;
            auraText.text = "0";
        }

        if (GameSettings.Instance != null)
            SetLevel(GameSettings.Instance.level);
        else
            SetLevel(0);

        // Subscribe to the delta event
        if (GameManager.Instance != null) {
            GameManager.Instance.OnAuraChanged += HandleAuraChanged;
            GameManager.Instance.OnLevelAdvanced += HandleLevelUp;
            GameManager.Instance.OnXPChanged += HandleXPChanged;
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnAuraChanged -= HandleAuraChanged;
            GameManager.Instance.OnLevelAdvanced -= HandleLevelUp;
            GameManager.Instance.OnXPChanged -= HandleXPChanged;
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
        if (Mathf.Abs(displayedXP - currentXPPercent) > 0.001f)
        {
            displayedXP = Mathf.Lerp(displayedXP, currentXPPercent, Time.deltaTime * countUpSpeed);
            if (xpBarFill != null)
                xpBarFill.fillAmount = displayedXP;
        }
    }


    private void HandleLevelUp()
    {
        SetLevel(GameSettings.Instance.level);
    }

    private void HandleXPChanged(float progress)
    {
        Debug.Log($"[AuraHUD] HandleXPChanged received: {progress:F2}");
        SetXPPercent(progress);
    }

    // ------------------------------------------------------------------
    //  Called by GameManager via OnAuraChanged (now passes delta)
    // ------------------------------------------------------------------
    private void HandleAuraChanged(float delta)
    {
        targetAura = GameManager.Instance.Aura;

        // Punch scale on the aura container
        StopCoroutine(nameof(PunchAuraScale));
        StartCoroutine(PunchAuraScale());

        // Flash the aura text color based on gain/loss
        StopCoroutine(nameof(FlashAuraText));
        Color flash = delta > 0 ? gainFlashColor : lossFlashColor;
        StartCoroutine(FlashAuraText(flash));

        // Floating "+500" or "−2000"
        SpawnFloatingText(delta);
    }

    // ------------------------------------------------------------------
    //  Public methods for level / XP updates (called however you like)
    // ------------------------------------------------------------------
    public void SetLevel(int level)
    {
        if (level != currentLevel)
        {
            currentLevel = level;
            if (levelText != null) levelText.text = (level + 1).ToString();

            StopCoroutine(nameof(PunchBadgeScale));
            StartCoroutine(PunchBadgeScale());
        }
    }

    public void SetXPPercent(float percent)
    {
        currentXPPercent = Mathf.Clamp01(percent);
    }

    // ------------------------------------------------------------------
    //  Animations
    // ------------------------------------------------------------------
    private IEnumerator PunchAuraScale()
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

    private IEnumerator PunchBadgeScale()
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

    private IEnumerator FlashAuraText(Color flash)
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

    private void SpawnFloatingText(float delta)
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

        StartCoroutine(AnimateFloatingText(go, cg));
    }

    private IEnumerator AnimateFloatingText(GameObject go, CanvasGroup cg)
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

    public void RefreshFromSettings()
    {
        if (GameSettings.Instance != null)
        {
            currentLevel = -1;  // force update

            if (GameSettings.Instance.isCustomMode)
            {
                if (levelText != null) levelText.text = "X";
                StartCoroutine(PunchBadgeScale());
            }
            else
            {
                SetLevel(GameSettings.Instance.level);
            }
        }
    }
}