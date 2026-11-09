using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Presentation for account/consent only. Uses their existing camera, and owns no
// gameplay manager, input, audio, session, consent, or tutorial-completion state.
public class menuBackdrop : MonoBehaviour
{
    public const float entryWidth = 480;
    public static bool isWide(float width) => width >= 1150;
    public static float entryOffset(float width) => -width * .5f + Mathf.Clamp(width * .06f, 32, 96) + entryWidth * .5f;

    private menuBackdropAssets art;
    private GameObject world;
    private SpriteRenderer carrier, interceptor, atmosphere;
    private starfieldBackground stars;
    private heliosEngineGlow engines;
    private RectTransform decoration;
    private TMP_Text wordmark, caption, transmission;
    private terminalPanel entryPanel;
    private Camera view;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float originalSize;
    private Color originalBackground;
    private CameraClearFlags originalClearFlags;
    private bool originalOrthographic, consent, focused = true;
    private float clock;
    private Texture2D hazeTexture;
    private Sprite hazeSprite;
    private ParticleSystem.Particle[] savedStars;
    private int savedStarCount;
    private UnityEngine.UI.Image transition;
    private float transitionAlpha;

    private void Start()
    {
        art = Resources.Load<menuBackdropAssets>("Launch/menuBackdrop");
        if (art == null) { enabled = false; return; }
        world = new GameObject("menuBackdropWorld");
        DontDestroyOnLoad(world);
        var starNode = new GameObject("menuStars"); starNode.transform.SetParent(world.transform, false);
        stars = starNode.AddComponent<starfieldBackground>(); stars.prepare(new Rect(-45, -16, 90, 32));
        stars.setMotion(.12f, 0, true);
        carrier = sprite("menuHelios", art.carrier, art.carrierColor, 0);
        interceptor = sprite("menuInterceptor", art.interceptor, art.interceptorColor, 1);
        engines = carrier.gameObject.AddComponent<heliosEngineGlow>(); engines.initialize(carrier); engines.setPower(.18f);
        createAtmosphere(); createDecoration();
        SceneManager.sceneLoaded += sceneLoaded;
        configure(SceneManager.GetActiveScene());
    }
    private SpriteRenderer sprite(string name, Sprite source, Color tint, int order)
    {
        var node = new GameObject(name, typeof(SpriteRenderer)); node.transform.SetParent(world.transform, false);
        var renderer = node.GetComponent<SpriteRenderer>(); renderer.sprite = source; renderer.color = tint;
        renderer.sharedMaterial = art.spriteMaterial; renderer.sortingOrder = order;
        return renderer;
    }
    private void createAtmosphere()
    {
        hazeTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "menuAtmosphere", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float distance = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
            hazeTexture.SetPixel(x, y, new Color(.06f, .24f, .43f, Mathf.Pow(Mathf.Clamp01(1 - distance), 2) * .7f));
        }
        hazeTexture.Apply();
        hazeSprite = Sprite.Create(hazeTexture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
        atmosphere = sprite("menuAtmosphere", hazeSprite, Color.white, -60);
    }
    private void createDecoration()
    {
        entryPanel=GetComponentInChildren<terminalPanel>(true);
        decoration = signalTerminal.node(transform, "entryWorldLabels"); signalTerminal.fill(decoration); decoration.SetAsFirstSibling();
        wordmark = signalTerminal.text(decoration, "wordmark", "COMET CATCHER", 30, 46, true);
        wordmark.characterSpacing = 2; wordmark.alignment = TextAlignmentOptions.Center;
        transmission = signalTerminal.text(decoration, "worldLabel", "THE HELIOS RUN", 11, 20, true);
        transmission.color = signalTerminal.accent; transmission.characterSpacing = 2; transmission.alignment = TextAlignmentOptions.Center;
        caption = signalTerminal.text(decoration, "carrierLabel", "HELIOS  /  COLONY SUPPLY CARRIER", 10, 24, true);
        caption.color = signalTerminal.secondary; caption.characterSpacing = 1; caption.alignment = TextAlignmentOptions.Center;
        var cover=signalTerminal.node(transform,"entryTransition");signalTerminal.fill(cover);cover.SetAsLastSibling();
        transition=signalTerminal.image(cover,Color.clear);
    }
    private void sceneLoaded(Scene scene, LoadSceneMode mode) { configure(scene); }
    private void configure(Scene scene)
    {
        restoreCamera();
        bool showing = scene.name == cloudAccount.accountScene || scene.name == cloudAccount.consentScene;
        bool wasShowing=world.activeSelf;
        var particles=stars.GetComponent<ParticleSystem>();
        if(wasShowing && !showing)
        {
            savedStars=new ParticleSystem.Particle[stars.starCount];savedStarCount=particles.GetParticles(savedStars);
        }
        world.SetActive(showing); decoration.gameObject.SetActive(showing);
        if(showing && !wasShowing && savedStars!=null){particles.SetParticles(savedStars,savedStarCount);particles.Play();}
        if(showing || wasShowing){transitionAlpha=1;transition.color=signalTerminal.background;}
        if (!showing) return;
        consent = scene.name == cloudAccount.consentScene;
        foreach (var root in scene.GetRootGameObjects())
        {
            view = root.GetComponentInChildren<Camera>();
            if (view != null) break;
        }
        if (view == null) { world.SetActive(false); return; }
        originalPosition = view.transform.position; originalRotation = view.transform.rotation;
        originalSize = view.orthographicSize; originalBackground = view.backgroundColor;
        originalOrthographic = view.orthographic; originalClearFlags = view.clearFlags;
        view.orthographic = true; view.orthographicSize = 10; view.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
        view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color32(3, 8, 17, 255);
        if (consent)
        {
            var form = FindFirstObjectByType<researchConsentUi>();
            if (form != null)
            {
                var background = form.transform.Find("background")?.GetComponent<UnityEngine.UI.Image>();
                if (background != null) background.color = new Color(7/255f, 14/255f, 26/255f, .12f);
                // Saved consent buttons retain their existing colors, callbacks and labels.
                foreach (var button in form.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    var label = button.GetComponentInChildren<TMP_Text>(true);
                    if (label != null) { label.font = signalTerminal.buttonFont; label.fontSize = 15; }
                }
            }
        }
        draw();
    }
    private void Update()
    {
        if(transition!=null && transitionAlpha>0)
        {
            transitionAlpha=Mathf.Max(0,transitionAlpha-Time.unscaledDeltaTime/.24f);
            var tint=signalTerminal.background;tint.a=transitionAlpha;transition.color=tint;
        }
        if (world == null || !world.activeSelf || view == null) return;
        bool quiet = !consent && cloudAccount.instance != null && cloudAccount.instance.signedIn;
        stars.presentationPaused = engines.paused = quiet || !focused;
        if (!quiet && focused) clock += Mathf.Min(Time.unscaledDeltaTime, .1f) * (consent ? .5f : 1);
        draw();
    }
    private void draw()
    {
        if (view == null) return;
        var canvas = (RectTransform)transform;
        float width = canvas.rect.width, height = canvas.rect.height;
        bool wide = isWide(width), signedIn = cloudAccount.instance != null && cloudAccount.instance.signedIn;
        bool reading = signedIn && !consent;
        float centerX = wide ? .75f : .5f;
        float worldWidth = 20 * view.aspect;
        float targetWidth = wide ? worldWidth * .44f : worldWidth * .88f;
        float factor = Mathf.Min(targetWidth / (art.carrier.bounds.size.x * art.carrierScale.x), 12 / (art.carrier.bounds.size.y * art.carrierScale.y));
        carrier.transform.localScale = art.carrierScale * factor;
        interceptor.transform.localScale = art.interceptorScale * factor;
        float drift = reading ? 0 : Mathf.Sin(clock * .35f) * .06f;
        var center = new Vector3((centerX - .5f) * worldWidth, -.45f + drift, 0);
        carrier.transform.position = center - Vector3.Scale(art.carrier.bounds.center, carrier.transform.localScale);
        interceptor.transform.position = center + new Vector3(0, carrier.bounds.size.y * .29f, -.1f);
        atmosphere.transform.position = center; atmosphere.transform.localScale = new Vector3(worldWidth * 1.25f, 25, 1);
        float brightness = reading ? .55f : wide ? (consent ? .85f : 1) : .3f;
        carrier.color = new Color(art.carrierColor.r * brightness, art.carrierColor.g * brightness, art.carrierColor.b * brightness, 1);
        interceptor.color = new Color(brightness, brightness, brightness, 1) * art.interceptorColor;
        decoration.gameObject.SetActive(!reading);
        bool showBrand=wide || entryPanel==null || -((RectTransform)entryPanel.transform).anchoredPosition.y>=116;
        wordmark.gameObject.SetActive(showBrand);transmission.gameObject.SetActive(showBrand);
        float labelWidth = wide ? width * .48f : width - 40;
        place(wordmark.rectTransform, centerX, wide ? height * .16f : 30, labelWidth, 44);
        wordmark.fontSize = wide ? 30 : 23;
        place(transmission.rectTransform, centerX, wide ? height * .16f + 45 : 76, labelWidth, 20);
        place(caption.rectTransform, centerX, height - 80, labelWidth, 24);
        caption.gameObject.SetActive(wide);
    }
    private static void place(RectTransform rect, float x, float top, float width, float height)
    {rect.anchorMin = rect.anchorMax = new Vector2(x, 1); rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -top); rect.sizeDelta = new Vector2(width, height);}
    private void OnApplicationFocus(bool value) { focused = value; }
    private void restoreCamera()
    {
        if (view == null) return;
        view.transform.SetPositionAndRotation(originalPosition, originalRotation); view.orthographicSize = originalSize;
        view.orthographic = originalOrthographic; view.backgroundColor = originalBackground; view.clearFlags = originalClearFlags; view = null;
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= sceneLoaded; restoreCamera();
        if (world != null) Destroy(world); if (decoration != null) Destroy(decoration.gameObject);
        if (transition != null) Destroy(transition.gameObject);
        if (hazeSprite != null) Destroy(hazeSprite); if (hazeTexture != null) Destroy(hazeTexture);
    }
}
