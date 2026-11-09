using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// Presentation only. gameManager still owns the run, and cloudAccount still owns
// its authenticated/idempotent session request. No session exists during the movie.
public class launchSequence : MonoBehaviour
{
    public enum phase { initializing, ready, launching, connecting, failed, playing, ended }
    public phase state { get; private set; } = phase.initializing;
    [Tooltip("Duration of the approved wide-to-gameplay camera move, in seconds.")]
    [Min(.2f)] public float cameraMoveSeconds = 2.2f;
    [Min(0)] public float chargeSeconds = .42f;
    [Range(0, .3f)] public float launchVolume = .16f;
    public bool transitioning => state == phase.launching || state == phase.connecting;
    public float animationTime { get; private set; }
    private gameManager manager;
    private settingsUi settings;
    private Camera view;
    private Vector3 closePosition, widePosition;
    private float closeSize, wideSize;
    private Quaternion closeRotation;
    private starfieldBackground stars;
    private heliosEngineGlow engines;
    private GameObject overlay, blocker;
    private RectTransform welcome;
    private CanvasGroup welcomeGroup;
    private UnityEngine.UI.Button startButton;
    private TMP_Text status, metadata, briefing;
    private terminalTransmission transmission;
    private launchEdgeGlow edge;
    private AudioSource launchAudio;
    private AudioClip launchClip;
    private Action<Action> connect;
    private Action begin;
    private bool serverReady, quick, focused = true, audioPaused;
    private float canvasWidth, connectionWait;

    public static void create(GameObject root, UnityEngine.UI.Button button)
    {
        var sequence = root.transform.parent.gameObject.AddComponent<launchSequence>();
        sequence.overlay = root; sequence.startButton = button; sequence.build();
    }
    private void build()
    {
        welcome = signalTerminal.node(overlay.transform, "flightBriefing");
        welcome.anchorMin = welcome.anchorMax = new Vector2(.5f, 1);
        welcome.pivot = new Vector2(.5f, 1); welcome.anchoredPosition = new Vector2(0, -24);
        welcome.sizeDelta = new Vector2(620, 158);
        welcomeGroup = welcome.gameObject.AddComponent<CanvasGroup>(); welcomeGroup.alpha = 0;
        metadata = signalTerminal.text(welcome, "transmissionSource", "HELIOS / FLIGHT CONTROL", 11, 18, true);
        metadata.color = signalTerminal.secondary; terminalPanel.place(metadata.rectTransform, 0, 0, 0, 18);
        briefing = signalTerminal.text(welcome, "transmission", "", 20, 58, true);
        briefing.color = new Color(.57f, .9f, 1); terminalPanel.place(briefing.rectTransform, 0, 27, 0, 62);
        transmission = briefing.gameObject.AddComponent<terminalTransmission>();
        transmission.initialize(briefing, "Welcome back, pilot.\nColony supplies secured. Awaiting your command.");
        transmission.paused = true;
        startButton.transform.SetParent(welcome, false); startButton.gameObject.SetActive(true);
        var buttonRect = (RectTransform)startButton.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0, 1); buttonRect.pivot = new Vector2(0, 1);
        buttonRect.anchoredPosition = new Vector2(0, -103); buttonRect.sizeDelta = new Vector2(184, 42);
        signalTerminal.styleButton(startButton, signalTerminal.actionStyle.primary);
        startButton.GetComponentInChildren<TMP_Text>().text = "Start run"; startButton.interactable = false;

        status = signalTerminal.text(overlay.transform, "launchStatus", "", 20, 100, true);
        var sr = status.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(.5f, .65f);
        sr.pivot = new Vector2(.5f, .5f); sr.sizeDelta = new Vector2(640, 110); sr.anchoredPosition = Vector2.zero;
        status.alignment = TextAlignmentOptions.Center; status.color = signalTerminal.accent;

        var block = signalTerminal.node(transform, "launchInputBlock"); signalTerminal.fill(block);
        var blockCanvas = block.gameObject.AddComponent<Canvas>(); blockCanvas.overrideSorting = true; blockCanvas.sortingOrder = 300;
        block.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>(); signalTerminal.image(block, Color.clear, true);
        blocker = block.gameObject; blocker.SetActive(false);
        var pulse = signalTerminal.node(transform, "slipstreamEdgeLight"); signalTerminal.fill(pulse);
        var effectCanvas = pulse.gameObject.AddComponent<Canvas>(); effectCanvas.overrideSorting = true; effectCanvas.sortingOrder = -1;
        edge = pulse.gameObject.AddComponent<launchEdgeGlow>(); edge.raycastTarget = false; edge.color = Color.clear;
    }
    private IEnumerator Start()
    {
        // Let playerController.Start cache the original gameplay lane geometry first.
        yield return null;
        manager = gameManager.instance; view = Camera.main; settings = FindFirstObjectByType<settingsUi>();
        if (manager == null || manager.isPractice || view == null) { enabled = false; yield break; }
        closePosition = view.transform.position; closeSize = view.orthographicSize; closeRotation = view.transform.rotation;
        var hull = manager.mothership.GetComponent<SpriteRenderer>();
        widePosition = new Vector3(hull.bounds.center.x, hull.bounds.center.y + .9f, closePosition.z);
        fitWide(hull.bounds);
        stars = manager.starfieldEffect.GetComponent<starfieldBackground>();
        float widestAspect = Mathf.Max(2.4f, view.aspect);
        float largestSize = Mathf.Max(wideSize, (hull.bounds.extents.x + 2.5f) / 1.2f);
        float halfWidth = largestSize * widestAspect + 3;
        float bottom = Mathf.Min(widePosition.y - largestSize, closePosition.y - closeSize) - 4;
        float top = Mathf.Max(widePosition.y + largestSize, closePosition.y + closeSize) + 4;
        stars.prepare(Rect.MinMaxRect(-halfWidth, bottom, halfWidth, top));
        manager.starfieldEffect.SetActive(true); stars.setMotion(.35f, 0, true);
        engines = manager.mothership.AddComponent<heliosEngineGlow>(); engines.initialize(hull);
        launchAudio = gameObject.AddComponent<AudioSource>(); launchAudio.playOnAwake = false; launchAudio.spatialBlend = 0;
        launchAudio.outputAudioMixerGroup = manager.playerController.normalLaserSound.outputAudioMixerGroup;
        launchAudio.volume = launchVolume;
        manager.onGameStarted += enteredGame; manager.onGameOver += endedGame;
        pose(0); state = phase.ready; resize();
    }
    private bool blocked => !focused || accountUi.isBlocking || (settings != null && settings.settingPanel.activeInHierarchy);
    private void Update()
    {
        if (state == phase.initializing || manager == null) return;
        if (state == phase.playing || state == phase.ended) return;
        resize();
        bool paused = blocked;
        transmission.paused = paused || state != phase.ready;
        stars.presentationPaused = paused; engines.paused = paused;
        if (launchAudio.isPlaying && paused) { launchAudio.Pause(); audioPaused = true; }
        else if (audioPaused && !paused) { launchAudio.UnPause(); audioPaused = false; }
        blocker.SetActive(transitioning && !paused);
        if (state == phase.ready || state == phase.failed)
        {
            welcomeGroup.alpha = paused ? 0 : 1;
            startButton.interactable = !paused && cloudAccount.instance != null && !cloudAccount.instance.busy;
            if (state == phase.ready) pose(0);
            return;
        }
        if (paused) return;
        if (state == phase.launching)
        {
            animationTime += Mathf.Min(Time.unscaledDeltaTime, .1f);
            drawLaunch();
            float duration = quick ? .35f : chargeSeconds + cameraMoveSeconds + .18f;
            if (animationTime >= duration)
            {
                settle(); state = phase.connecting; connectionWait = 0; status.text = "";
                connect(() => serverReady = true);
            }
        }
        else if (state == phase.connecting)
        {
            connectionWait += Time.unscaledDeltaTime;
            if (serverReady)
            {
                // Effects have ended and the camera is exact before the first note or timer.
                settle(); begin();
                if (!manager.isGameActive) fail("Audio is not ready. Check the audio settings, then retry.");
            }
            else if (!cloudAccount.instance.busy)
                fail(string.IsNullOrEmpty(cloudAccount.instance.message) ? "Could not prepare your run. Please retry." : cloudAccount.instance.message);
            else if (connectionWait > .6f)
            {
                status.text = "CONNECTING...";
                status.color = new Color(.66f, .91f, 1, Mathf.Clamp01((connectionWait - .6f) / .2f));
            }
        }
    }
    public void requestLaunch(Action<Action> requestSession, Action startRun)
    {
        if (state == phase.initializing || transitioning || state == phase.playing || blocked) return;
        quick = state == phase.ended || state == phase.failed;
        connect = requestSession; begin = startRun; serverReady = false; animationTime = 0;
        state = phase.launching; manager.playerController.clearHeldInput(true);
        healthBar.instance.setVisible(false);
        if (quick)
        {
            var results = FindFirstObjectByType<gameOverUi>();
            if (results != null) results.gameOverPanel.SetActive(false);
            overlay.SetActive(true);
        }
        status.font = signalTerminal.monoFont; status.fontSize = 20;
        placeStatus(false);
        startButton.interactable = false; blocker.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        manager.starfieldEffect.SetActive(true); stars.presentationPaused = false;
        status.text = quick ? "PREPARING NEXT RUN" : "SLIPSTREAM DRIVE / CHARGING";
        if (!quick)
        {
            if (launchClip == null) launchClip = makeLaunchSound();
            launchAudio.clip = launchClip; launchAudio.volume = launchVolume; launchAudio.Play();
        }
        drawLaunch();
    }
    private void drawLaunch()
    {
        welcomeGroup.alpha = quick ? 0 : 1 - Mathf.Clamp01(animationTime / .22f);
        if (quick) { pose(1); stars.setMotion(1, 0, true); return; }
        float charge = Mathf.Clamp01(animationTime / Mathf.Max(.01f, chargeSeconds));
        float t = Mathf.Clamp01((animationTime - chargeSeconds) / cameraMoveSeconds);
        pose(t);
        float burst = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .92f));
        float kick = Mathf.Exp(-Mathf.Pow((t - .16f) / .12f, 2));
        if (t > 0 && t < 1)
            view.transform.position += new Vector3(Mathf.Sin(animationTime * 69), Mathf.Sin(animationTime * 83), 0) * (.022f * kick);
        engines.setPower(Mathf.Lerp(.2f, 1, charge));
        stars.setMotion(Mathf.Lerp(.35f, 1, charge) + burst * 8, burst * .9f, true);
        edge.color = new Color(.12f, .7f, 1, kick * .28f);
        status.text = t > 0 ? "ENTERING SLIPSTREAM" : "SLIPSTREAM DRIVE / CHARGING";
        float reveal = Mathf.Clamp01((animationTime - .14f) / .16f);
        status.color = new Color(.66f, .91f, 1, reveal * (1 - Mathf.SmoothStep(0, 1, (t - .65f) / .25f)));
    }
    private void settle()
    {
        pose(1); edge.color = Color.clear; engines.setPower(.35f);
        stars.setMotion(1, 0, true); launchAudio.Stop(); audioPaused = false;
        status.color = signalTerminal.accent;
    }
    private void enteredGame()
    {
        settle(); state = phase.playing; blocker.SetActive(false); status.text = "";
        stars.presentationPaused = false; stars.setMotion(1, 0, false);
        manager.playerController.clearHeldInput(true);
    }
    private void endedGame(gameSessionData data) { state = phase.ended; }
    private void fail(string reason)
    {
        settle(); state = phase.failed; serverReady = false; blocker.SetActive(false);
        overlay.SetActive(true); welcomeGroup.alpha = 1;
        metadata.text = "HELIOS / LAUNCH ON HOLD";
        status.text = reason; status.font = signalTerminal.bodyFont; status.fontSize = 18;
        placeStatus(true);
        startButton.GetComponentInChildren<TMP_Text>().text = "Retry launch";
        startButton.interactable = true;
    }
    private void fitWide(Bounds bounds)
    {
        wideSize = Mathf.Max(closeSize, bounds.extents.y + 2.5f, (bounds.extents.x + 2.5f) / view.aspect);
        bool narrow = ((RectTransform)transform).rect.width < 1150;
        if (narrow) wideSize = Mathf.Max(wideSize, 11);
        widePosition.y = bounds.center.y + .9f + (narrow ? 1.2f : 0);
    }
    private void resize()
    {
        float width = ((RectTransform)transform).rect.width;
        if (Mathf.Abs(width - canvasWidth) < .5f) return;
        canvasWidth = width;
        // Keep the narrow briefing between the untouched HUD and Account button.
        bool narrow = width < 1150;
        welcome.sizeDelta = new Vector2(narrow ? Mathf.Max(320, width - 540) : 620, 168);
        welcome.anchoredPosition = new Vector2(0, -24);
        briefing.fontSize = narrow ? 18 : 20;
        terminalPanel.place(briefing.rectTransform, 0, 27, 0, narrow ? 76 : 62);
        ((RectTransform)startButton.transform).anchoredPosition = new Vector2(0, narrow ? -116 : -103);
        placeStatus(state == phase.failed);
        if (state == phase.ready) fitWide(manager.mothership.GetComponent<SpriteRenderer>().bounds);
    }
    private void placeStatus(bool error)
    {
        var rect = status.rectTransform;
        float width = ((RectTransform)transform).rect.width;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, error ? .65f : 1);
        rect.pivot = new Vector2(.5f, error ? .5f : 1);
        rect.anchoredPosition = new Vector2(0, error ? 0 : -24);
        rect.sizeDelta = new Vector2(error ? Mathf.Min(640, width - 64) : width < 1150 ? Mathf.Max(320, width - 540) : 620, 100);
        if (!error) status.fontSize = width < 1150 ? 18 : 20;
    }
    private void pose(float t)
    {
        float eased = t < .5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
        view.transform.position = t >= 1 ? closePosition : Vector3.Lerp(widePosition, closePosition, eased);
        view.orthographicSize = t >= 1 ? closeSize : Mathf.Exp(Mathf.Lerp(Mathf.Log(wideSize), Mathf.Log(closeSize), eased));
        view.transform.rotation = closeRotation;
    }
    private AudioClip makeLaunchSound()
    {
        // Non-musical filtered noise: no extra pitch cues and no sound under the piano trial.
        const int rate = 22050; float duration = chargeSeconds + cameraMoveSeconds - .15f;
        var samples = new float[Mathf.CeilToInt(rate * duration)]; var random = new System.Random(892);
        float low = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate; float noise = (float)random.NextDouble() * 2 - 1;
            low = Mathf.Lerp(low, noise, .025f + .09f * Mathf.Clamp01(t / .7f));
            float envelope = Mathf.SmoothStep(0, 1, t / .4f) * (1 - Mathf.SmoothStep(0, 1, (t - .75f) / (duration - .75f)));
            samples[i] = low * envelope * .65f;
        }
        var clip = AudioClip.Create("heliosLaunch", samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
    }
    private void OnApplicationFocus(bool value)
    {
#if !UNITY_EDITOR
        focused = value;
#endif
    }
    private void OnDestroy()
    {
        if (manager != null) { manager.onGameStarted -= enteredGame; manager.onGameOver -= endedGame; }
        if (view != null && state != phase.initializing) { view.transform.position = closePosition; view.orthographicSize = closeSize; view.transform.rotation = closeRotation; }
        if (launchAudio != null) launchAudio.Stop(); if (launchClip != null) Destroy(launchClip);
        if (engines != null) Destroy(engines);
    }
}
