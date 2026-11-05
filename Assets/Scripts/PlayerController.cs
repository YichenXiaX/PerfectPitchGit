using UnityEngine;
using UnityEngine.InputSystem;

public class playerController : MonoBehaviour
{
    [UnityEngine.Serialization.FormerlySerializedAs("celebrationVFX")] public celebrationVfx celebrationVfx;
    [UnityEngine.Serialization.FormerlySerializedAs("laserVFX")] public laserBeamVfx laserVfx;
    public GameObject bulletPrefab, failedPredictionBulletPrefab;
    public Transform firePoint;
    public AudioSource correctLaserSound, normalLaserSound;
    [UnityEngine.Serialization.FormerlySerializedAs("teleportVFX")] public teleportVfx teleportVfx;
    [Tooltip("Seconds between held-fire shots, using gameplay time. Minimum 0.05 seconds.")]
    [Min(heldFireState.minimumInterval)] public float shotInterval = .20f;
    public event System.Action<int,bool> onShotFired;
    private readonly heldFireState fireState = new heldFireState();
    private InputAction[] laneActions;
    private gameManager manager;
    private settingsUi settings;
    private bool hasPredictedThisRound;
    private int currentQuadrant => fireState.lane;
    private bool switchedAfterPrediction;
    private bool activeLeftHanded;
    private float unitWidth;
    private bool focused = true;
    public static bool fireHeld => Keyboard.current != null && Keyboard.current.fKey.isPressed;
    public bool inputBlocked => !focused || !isActiveAndEnabled || Time.timeScale <= 0 || manager == null || !manager.isGameActive || manager.roundState == gameManager.roundStateType.resolved || (manager.isPractice && !manager.practiceInputEnabled) || accountUi.isBlocking || (settings != null && settings.settingPanel != null && settings.settingPanel.activeInHierarchy);
    private void Start()
    {
        manager = gameManager.instance;
        settings = FindFirstObjectByType<settingsUi>(FindObjectsInactive.Include);
        unitWidth = Camera.main.orthographicSize * 2 * Camera.main.aspect / 8;
        if(manager != null) manager.onRoundStart += handleRoundStart;
        activeLeftHanded = controlLayout.leftHanded;
        bindKeys();
    }
    private void bindKeys()
    {
        if(laneActions != null) foreach(var action in laneActions) action.Dispose();
        laneActions = new InputAction[4];
        for(int i=0;i<4;i++)
        {
            int lane=i;
            var key=controlLayout.key(i,activeLeftHanded);
            string keyName=Keyboard.current != null ? Keyboard.current[key].name : key.ToString();
            var action=new InputAction("lane"+i,InputActionType.Button,"<Keyboard>/"+keyName);
            action.performed += context => selectLane(lane);
            action.Enable(); laneActions[i]=action;
        }
        clearHeldInput(true);
    }
    // InputActions preserve event order, including multiple key presses in a frame.
    private void selectLane(int lane)
    {
        if(inputBlocked) return;
        if(hasPredictedThisRound && lane != currentQuadrant) switchedAfterPrediction=true;
        fireState.select(lane);
        var position=transform.position; position.x=unitWidth*(-3+lane*2);
        transform.position=position; moveShip(position);
    }
    private void Update()
    {
        if(activeLeftHanded != controlLayout.leftHanded) { activeLeftHanded=controlLayout.leftHanded; bindKeys(); }
        handleShoot();
    }
    private void OnValidate() { if(float.IsNaN(shotInterval) || float.IsInfinity(shotInterval)) shotInterval=.2f; shotInterval=Mathf.Max(heldFireState.minimumInterval,shotInterval); }
    private void OnApplicationFocus(bool value) { focused=value; clearHeldInput(false); }
    private void OnDisable() { clearHeldInput(false); }
    private void OnDestroy()
    {
        if(manager != null) manager.onRoundStart -= handleRoundStart;
        if(laneActions != null) foreach(var action in laneActions) action.Dispose();
    }
    public void clearHeldInput(bool clearLane) { fireState.reset(clearLane); }
    private void handleRoundStart(int quadrant) { hasPredictedThisRound=false; switchedAfterPrediction=false; clearHeldInput(false); }
    public void handleShoot()
    {
        bool held=currentQuadrant>=0 && Keyboard.current != null && Keyboard.current[controlLayout.key(currentQuadrant,activeLeftHanded)].isPressed;
        if(!fireState.tick(Time.timeAsDouble,shotInterval,fireHeld,held,inputBlocked)) return;
        int firedLane=currentQuadrant;
        if(!manager.acceptsShot(firedLane,!hasPredictedThisRound)) return;
        if(!hasPredictedThisRound)
        {
            hasPredictedThisRound=true;
            manager.onPlayerPrediction(firedLane);
            onShotFired?.Invoke(firedLane,true);
        }
        else
        {
            simpleShot();
            if(switchedAfterPrediction && manager.isSecondGuessWindow) manager.onSecondGuess(currentQuadrant);
            onShotFired?.Invoke(firedLane,false);
        }
    }
    public void fancyShot()
    {
        correctLaserSound.Play(); //play sound effect
        onCorrectPrediction();
    }

    public void onCorrectPrediction()
    {
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(
            Camera.main, firePoint.position
        );

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            celebrationVfx.canvas.GetComponent<RectTransform>(),
            screenPos,
            celebrationVfx.canvas.worldCamera,
            out Vector2 canvasPos
        );

        celebrationVfx.play(canvasPos);

        laserVfx.fire();
    }

    void moveShip(Vector3 newPosition)
    {
        teleportVfx.teleport(newPosition);
    }


    public void failedPredictionShot() //calls special bullet prefab
    {
        GameObject failedPredictionBullet = Instantiate(failedPredictionBulletPrefab, firePoint.position, firePoint.rotation);
        normalLaserSound.Play();
    }

    public void simpleShot()
    {
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        normalLaserSound.Play();
    }


    //getters & setters
    public bool getHasPredictedThisRound()
    {
        return hasPredictedThisRound;
    }
    public void setHasPredictedThisRound(bool predicted)
    {
        hasPredictedThisRound = predicted;
    }

    public int getPlayerQuadrant()
    {
        return currentQuadrant;
    }
}
