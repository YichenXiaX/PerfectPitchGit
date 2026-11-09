using System.Collections;
using UnityEngine;
using TMPro;

public class tutorialDirector : MonoBehaviour
{
    public static tutorialDirector instance {get;private set;}
    public enum lesson { controls, signal, mapping, demonstration, octaves, correct, celebrating, success, miss, awaitingComet, spotlight, recovery, secondGuess, release, impactArrival, impactSpotlight, impact, gameOver, assessment, assessmentRetry, complete, saved }
    public lesson state {get;private set;}
    public int demoQuadrant {get;private set;}
    public bool cuePlaying {get;private set;}
    public bool assessmentDone {get;private set;}
    private const int assessmentQuadrant=2;
    private gameManager manager;
    private TMP_Text transmission,body,hint;
    private UnityEngine.UI.Button next,listen,replay;
    private terminalPanel panel;
    private terminalLayoutSelector layoutChoice;
    private RectTransform inputRow;
    private terminalKeycap laneCap,fireCap;
    private tutorialSpectrum diagram;
    private UnityEngine.UI.Image preview;
    private GameObject dimmer;
    private practiceTarget hazard;
    private bool correctDone,missDone,recoveryDone,impactDone,restartDone,cueHeard,previousAudioPause;
    private float lastAuraChange;
    private int cueGeneration,assessmentErrors;
    private void Awake(){instance=this;previousAudioPause=AudioListener.pause;}
    private IEnumerator Start()
    {
        yield return null;manager=gameManager.instance;
        yield return manager.frequencyManager.waitForRecordings();
        manager.onPredictionSuccess+=correctPrediction;manager.onPredictionMiss+=wrongPrediction;
        manager.onCometCreated+=cometCreated;manager.onRecovery+=recovered;
        manager.onGameOver+=gameOver;manager.onPracticeRestart+=restart;manager.onAuraChanged+=auraChanged;
        controlLayout.changed+=layoutChanged;
        buildUi();Time.timeScale=1;manager.resetPractice();setState(lesson.controls);
    }
    private void buildUi()
    {
        var root=new GameObject("flightTerminal",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
        root.transform.SetParent(transform,false);root.AddComponent<terminalKeyboardNavigation>().blockWithAccount=true;
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=90;signalTerminal.canvas(canvas);
        panel=terminalPanel.create(root.transform,"transmission");panel.centerVertically=true;transmission=panel.title;
        body=signalTerminal.text(panel.content,"body","",18);
        layoutChoice=terminalLayoutSelector.create(panel.content);
        diagram=tutorialSpectrum.create(panel.content,manager.frequencyManager);
        var picture=signalTerminal.node(panel.content,"guidedCometPreview");signalTerminal.height(picture,72);
        preview=picture.gameObject.AddComponent<UnityEngine.UI.Image>();preview.preserveAspect=true;preview.raycastTarget=false;
        inputRow=signalTerminal.node(panel.content,"inputKeys");signalTerminal.height(inputRow,36);signalTerminal.row(inputRow,8);
        var hold=signalTerminal.text(inputRow,"instruction","Hold",17,30);signalTerminal.width(hold.rectTransform,45);
        laneCap=terminalKeycap.create(inputRow,"[",1);laneCap.guided=true;
        var plus=signalTerminal.text(inputRow,"plus","+",16,28);signalTerminal.width(plus.rectTransform,16);
        fireCap=terminalKeycap.create(inputRow,"F",-1,true);
        hint=signalTerminal.text(panel.content,"hint","",17);hint.color=signalTerminal.secondary;
        listen=signalTerminal.button(panel.actions,"listen","Replay sound",playSignal,signalTerminal.actionStyle.secondary,145);
        replay=FindFirstObjectByType<gameOverUi>().restartButton;replay.transform.SetParent(panel.actions,false);signalTerminal.height((RectTransform)replay.transform,40);signalTerminal.width((RectTransform)replay.transform,170);signalTerminal.styleButton(replay,signalTerminal.actionStyle.primary);replay.GetComponentInChildren<TMP_Text>().text="Replay";
        next=signalTerminal.button(panel.actions,"continue","Continue",advance,signalTerminal.actionStyle.primary,170);
    }
    private void stopCue(){cueGeneration++;cuePlaying=false;if(manager!=null && manager.frequencyManager!=null)manager.frequencyManager.stopSequence();if(diagram!=null)diagram.highlight(-1);}
    private void setState(lesson value)
    {
        stopCue();state=value;cueHeard=false;manager.practiceInputEnabled=value==lesson.recovery;manager.playerController.clearHeldInput(true);render();
    }
    private bool isCueStep => state==lesson.demonstration || state==lesson.octaves || state==lesson.correct || state==lesson.miss || state==lesson.assessment;
    private int guidedQuadrant => state==lesson.correct?1:state==lesson.miss?0:3;
    private void render()
    {
        bool map=state==lesson.mapping || state==lesson.demonstration || state==lesson.octaves;
        bool acting=state==lesson.correct || state==lesson.miss || state==lesson.recovery;
        layoutChoice.gameObject.SetActive(state==lesson.controls);diagram.gameObject.SetActive(map);
        inputRow.gameObject.SetActive(acting);laneCap.lane=guidedQuadrant;laneCap.live=fireCap.live=acting;
        bool showPreview=state==lesson.correct || state==lesson.miss;preview.gameObject.SetActive(showPreview);
        if(showPreview)preview.sprite=gameSettings.instance.cometPrefabs[state==lesson.correct?1:3].GetComponentInChildren<SpriteRenderer>().sprite;
        replay.gameObject.SetActive(state==lesson.gameOver);listen.gameObject.SetActive(isCueStep);
        bool navigable=state==lesson.controls || state==lesson.signal || map || state==lesson.success || state==lesson.spotlight || state==lesson.impactSpotlight || state==lesson.secondGuess || state==lesson.assessmentRetry || state==lesson.complete || state==lesson.saved;
        next.gameObject.SetActive(navigable);next.interactable=!cuePlaying && (!isCueStep || cueHeard);
        next.GetComponentInChildren<TMP_Text>().text=state==lesson.complete || state==lesson.saved?"Enter game":state==lesson.spotlight || state==lesson.impactSpotlight?"Resume":state==lesson.assessmentRetry?"Try again":state==lesson.demonstration && demoQuadrant<3?"Next quadrant":"Continue";
        panel.metadata.text="HELIOS / FLIGHT CONTROL";panel.progress.text=stepNumber().ToString("00")+" / 10";
        panel.preferredWidth=map?680:620;panel.reserveTutorialHud=map;
        hint.text="";
        switch(state)
        {
            case lesson.controls:transmission.text="Take command";body.text="HELIOS is carrying supplies to colonies beyond the settled systems. Its route passes through a comet field, and you operate the cannon that protects the ship.";hint.text="Choose your controls. Hold a quadrant key, then F to fire.";break;
            case lesson.signal:transmission.text="Listen before you aim";body.text="Our engineers have developed a sonar system that detects each comet's wave signature before it appears. The system converts that signal into a short sequence of notes. Your job is to interpret what you hear and fire into the correct quadrant.";hint.text="These are sensor signals converted into sound aboard HELIOS.";break;
            case lesson.mapping:transmission.text="Sound points to a lane";body.text="One octave is divided into four quadrants. Each covers a range of pitches and one type of comet. High pitches are on the left; low pitches are on the right.";hint.text="The keyboard is reversed here to match the lanes: high on the left, low on the right.";break;
            case lesson.demonstration:transmission.text="Listen to Quadrant "+(demoQuadrant+1);body.text="Watch the pitches, comet and key for the same quadrant as the sequence plays.";hint.text="The piano keys are reference points. A signal can fall between them, too. You only need its quadrant; you do not need to name the note.";break;
            case lesson.octaves:transmission.text="Same quadrant, another octave";body.text="A signal can also be played an octave higher or lower. It still belongs to the same quadrant and identifies the same type of comet.";hint.text="Listen to Quadrant 2 in the original octave, then one octave higher, then one octave lower.";break;
            case lesson.correct:transmission.text="Predict the comet";body.text="This signal belongs to Quadrant 2. Hold "+controlLayout.label(1)+", then press F while keeping that key held down.";hint.text="Listen to the complete sequence, then try it.";break;
            case lesson.celebrating:transmission.text="Interception";body.text="Watch the interception finish.";break;
            case lesson.success:transmission.text="Correct prediction";body.text="You predicted the correct quadrant and intercepted the comet before it appeared.";hint.text="Aura rewards the interception. The bar below HELIOS shows hull health.";break;
            case lesson.miss:transmission.text="Try a planned mistake";body.text="Now let's see what happens when a prediction is wrong. This signal belongs to Quadrant 4. For this example, hold "+controlLayout.label(0)+" and press F.";break;
            case lesson.awaitingComet:transmission.text="Watch the fourth lane";body.text="The prediction missed. Watch this comet enter from above.";break;
            case lesson.spotlight:transmission.text="Destroy it to reduce exposure";body.text="The prediction missed, so the comet has entered the ship's path. These comets emit radiation while they remain nearby. Destroy it quickly to reduce the ship's exposure.";hint.text="The Aura change is a prediction penalty, separate from hull damage. Resume when ready.";break;
            case lesson.recovery:transmission.text="Recover with backup fire";body.text="The comet is in Quadrant 4. Hold "+controlLayout.label(3)+" and hold F to fire. Keep the quadrant key held down while you shoot.";hint.text="Holding F fires repeatedly. One hit is enough to destroy a comet.";break;
            case lesson.secondGuess:transmission.text="You can change your mind";body.text="After a wrong prediction, switch to your next-best quadrant and keep F held. You can do this before the comet appears.";hint.text="Once you can see the comet, aim at its lane to recover.";break;
            case lesson.release:transmission.text="Watch an impact";body.text="Release F. In this example, let the comet reach HELIOS without shooting.";break;
            case lesson.impactArrival:transmission.text="Watch the comet arrive";body.text="Follow the comet from the top of its lane toward HELIOS.";break;
            case lesson.impactSpotlight:transmission.text="A comet on course for HELIOS";body.text="This comet is on the same path. Resume to watch what happens if it reaches the ship.";hint.text="Firing is disabled for this example.";break;
            case lesson.impact:transmission.text="Watch the impact";body.text="The comet is approaching HELIOS. Firing is disabled for this example.";break;
            case lesson.gameOver:transmission.text="The run has ended";body.text="If a comet hits HELIOS, the run ends. Select Replay to try again.";break;
            case lesson.assessment:transmission.text="Your turn";body.text="Listen and choose the quadrant yourself. Hold its key and press F. You can replay the sound.";hint.text="Choose from "+controlLayout.label(0)+", "+controlLayout.label(1)+", "+controlLayout.label(2)+", "+controlLayout.label(3)+". Release F before trying.";break;
            case lesson.assessmentRetry:transmission.text="Listen once more";body.text="That was a different quadrant. Replay the signal and try again.";hint.text=assessmentErrors>1?"Hint: the sequence belongs to Quadrant 3, the third lane from the left.":"Hint: this signal is in the lower half of the base octave. Compare the two lanes on the right.";break;
            case lesson.complete:transmission.text="Ready to take command";body.text="You matched the signal and intercepted the comet. Listen, choose a quadrant, then hold its key and F.";hint.text="Release F before each new signal. Enter game saves your training.";break;
            case lesson.saved:transmission.text="Training saved";body.text="Your training is saved. Take command when ready.";break;
        }
        hint.gameObject.SetActive(hint.text.Length>0);panel.reflow();
    }
    private int stepNumber(){if(state==lesson.controls)return 1;if(state==lesson.signal)return 2;if(state==lesson.mapping)return 3;if(state==lesson.demonstration)return 4;if(state==lesson.octaves)return 5;if(state<=lesson.success)return 6;if(state<=lesson.recovery)return 7;if(state<=lesson.gameOver)return 8;if(state<=lesson.assessmentRetry)return 9;return 10;}
    private void advance()
    {
        if(!next.gameObject.activeSelf || !next.interactable)return;
        switch(state)
        {
            case lesson.controls:setState(lesson.signal);break;
            case lesson.signal:setState(lesson.mapping);break;
            case lesson.mapping:demoQuadrant=0;setState(lesson.demonstration);playSignal();break;
            case lesson.demonstration:if(demoQuadrant<3){demoQuadrant++;setState(lesson.demonstration);}else setState(lesson.octaves);playSignal();break;
            case lesson.octaves:manager.practiceRound(1);setState(lesson.correct);playSignal();break;
            case lesson.success:manager.practiceRound(3);setState(lesson.miss);playSignal();break;
            case lesson.spotlight:resumeHazard(false);break;
            case lesson.impactSpotlight:resumeHazard(true);break;
            case lesson.secondGuess:manager.clearPracticeHazards();setState(lesson.release);break;
            case lesson.assessmentRetry:beginAssessment();break;
            case lesson.complete:
                if(!(correctDone && missDone && recoveryDone && impactDone && restartDone && assessmentDone))return;
                next.interactable=false;cloudAccount.instance.completeTutorial(ok=>{if(this==null)return;next.interactable=true;if(ok){setState(lesson.saved);Time.timeScale=1;cloudAccount.instance.enterPlay();}else hint.text=cloudAccount.instance.message;});break;
            case lesson.saved:Time.timeScale=1;cloudAccount.instance.enterPlay();break;
        }
    }
    private void playSignal()
    {
        if(!isCueStep)return;stopCue();cueHeard=false;cuePlaying=true;manager.practiceInputEnabled=false;manager.playerController.clearHeldInput(true);next.interactable=false;
        int generation=cueGeneration;int quadrant=state==lesson.demonstration?demoQuadrant:state==lesson.miss?3:state==lesson.assessment?assessmentQuadrant:1;
        playCuePart(quadrant,0,generation);
    }
    private void playCuePart(int quadrant,int octave,int generation)
    {
        try
        {
            manager.frequencyManager.playPracticeSequence(quadrant,octave,frequency=>
            {
                if(generation!=cueGeneration)return;
                if(diagram.gameObject.activeSelf)diagram.highlight(quadrant,frequency,octave);
                if(state==lesson.octaves)hint.text=octave==0?"Quadrant 2 · original octave":"Quadrant 2 · the same sequence one octave "+(octave>0?"higher":"lower");
            },()=>
            {
                if(this==null || generation!=cueGeneration)return;
                if(state==lesson.octaves && octave!=-1){playCuePart(quadrant,octave==0?1:-1,generation);return;}
                cuePlaying=false;cueHeard=true;next.interactable=true;
                if(diagram.gameObject.activeSelf)diagram.highlight(quadrant);
                if(state==lesson.demonstration)hint.text="Those notes were slightly different pitches, but all fell in Quadrant "+(quadrant+1)+". That signal belongs to this comet. Aim at "+new[]{"the first lane on the left","the second lane from the left","the third lane from the left","the far-right lane"}[quadrant]+".";
                if(state==lesson.octaves)hint.text="The pitches changed octave; the quadrant and comet stayed the same. Absolute pitch alone does not tell you the lane across octaves.";
                if(state==lesson.correct || state==lesson.miss || state==lesson.assessment){manager.practiceInputEnabled=true;manager.playerController.clearHeldInput(true);if(state!=lesson.assessment)hint.text="Now hold "+controlLayout.label(guidedQuadrant)+" and press F.";}
            });
        }
        catch(System.Exception error){cuePlaying=false;hint.text="The practice sound could not load. Replay sound to retry, or exit through Account.";Debug.LogException(error);}
    }
    public bool acceptShot(int lane,bool prediction)
    {
        if(!manager.practiceInputEnabled)return false;
        if(state==lesson.assessment && prediction){if(lane==assessmentQuadrant)return true;assessmentErrors++;setState(lesson.assessmentRetry);return false;}
        bool expected=(state==lesson.correct && prediction && lane==1) || (state==lesson.miss && prediction && lane==0) || (state==lesson.recovery && !prediction && lane==3);
        if(!expected)hint.text="For this example, hold "+controlLayout.label(guidedQuadrant)+" and press F. Release F and try again.";
        return expected;
    }
    private void correctPrediction(int lane)
    {
        bool final=state==lesson.assessment && lane==assessmentQuadrant;if(!final && (state!=lesson.correct || lane!=1))return;
        if(final)assessmentDone=true;else correctDone=true;setState(lesson.celebrating);StartCoroutine(finishCelebration(final));
    }
    private IEnumerator finishCelebration(bool final)
    {
        var fx=manager.playerController.celebrationVfx;float duration=Mathf.Max(manager.playerController.laserVfx.beamDuration,fx.sparkLifetime*1.3f,fx.ringDuration,1.4f);
        yield return new WaitForSeconds(duration+.15f);setState(final?lesson.complete:lesson.success);
    }
    private void wrongPrediction(int lane){if(state!=lesson.miss || lane!=0)return;missDone=true;setState(lesson.awaitingComet);}
    private void auraChanged(float delta){lastAuraChange=Time.time;}
    private void cometCreated(GameObject comet)
    {
        if(state!=lesson.awaitingComet && state!=lesson.impactArrival)return;
        bool impact=state==lesson.impactArrival;manager.practiceInputEnabled=false;manager.playerController.clearHeldInput(true);
        comet.GetComponent<radioactiveDamage>()?.stopDamage();comet.GetComponent<cometBehavior>().enabled=false;
        foreach(var collider in comet.GetComponentsInChildren<Collider2D>())collider.enabled=false;
        hazard=comet.AddComponent<practiceTarget>();
        var sprite=comet.GetComponentInChildren<SpriteRenderer>();float radius=sprite.bounds.extents.magnitude*1.35f;
        var camera=Camera.main;float top=camera.ViewportToWorldPoint(new Vector3(.5f,1,-camera.transform.position.z)).y;
        comet.transform.position=new Vector3(comet.transform.position.x,top+radius+.25f,comet.transform.position.z);
        hazard.revealY=top-camera.orthographicSize*2*.22f-radius;hazard.moving=true;StartCoroutine(arrive(hazard,impact));
    }
    private IEnumerator arrive(practiceTarget target,bool impact)
    {
        while(target!=null && !target.arrived)yield return null;if(target==null)yield break;target.moving=false;
        // World-space vapor emitted above the viewport must finish its existing
        // lifetime before the spotlight freezes it. Do not retime the VFX or HUD.
        float trailLifetime=0;
        foreach(var particles in target.GetComponentsInChildren<ParticleSystem>())trailLifetime=Mathf.Max(trailLifetime,particles.main.startLifetime.constantMax);
        float trailsSettledAt=Time.time+trailLifetime+.1f;
        // Observe existing HUD feedback without changing its timing or materials.
        var hud=FindFirstObjectByType<auraHud>();float until=Time.time+6;
        while(Time.time<until)
        {
            float duration=hud==null?1:Mathf.Max(hud.floatingTextDuration,hud.flashDuration,hud.punchDuration)+.15f;
            bool settled=hud==null || (hud.auraText!=null && hud.auraText.text==Mathf.RoundToInt(manager.aura).ToString());
            if(settled && Time.time>=trailsSettledAt && Time.time-lastAuraChange>=duration)break;yield return null;
        }
        setState(impact?lesson.impactSpotlight:lesson.spotlight);showSpotlight(target.transform);Time.timeScale=0;
    }
    private void resumeHazard(bool impact)
    {
        clearSpotlight();Time.timeScale=1;setState(impact?lesson.impact:lesson.recovery);if(hazard==null)return;
        foreach(var collider in hazard.GetComponentsInChildren<Collider2D>())collider.enabled=true;
        hazard.holdAtReveal=false;hazard.moving=true;
        if(!impact)hazard.minimumY=manager.mothership.GetComponent<Collider2D>().bounds.max.y+hazard.GetComponentInChildren<SpriteRenderer>().bounds.extents.magnitude+.4f;
        hazard.GetComponent<radioactiveDamage>()?.initialize(manager.mothership.transform,80);
    }
    private void showSpotlight(Transform target)
    {
        clearSpotlight();var root=signalTerminal.node(panel.transform.parent,"spotlightDimmer");root.SetAsFirstSibling();signalTerminal.fill(root);dimmer=root.gameObject;
        var bounds=target.GetComponentInChildren<SpriteRenderer>().bounds;
        foreach(var renderer in target.GetComponentsInChildren<ParticleSystemRenderer>())bounds.Encapsulate(renderer.bounds);
        bounds.Expand(.3f);
        var a=Camera.main.WorldToViewportPoint(bounds.min);var b=Camera.main.WorldToViewportPoint(bounds.max);
        a.x=Mathf.Clamp01(a.x);a.y=Mathf.Clamp01(a.y);b.x=Mathf.Clamp01(b.x);b.y=Mathf.Clamp01(b.y);
        dim(root,new Vector2(0,0),new Vector2(a.x,.84f));dim(root,new Vector2(b.x,0),new Vector2(1,.84f));
        dim(root,new Vector2(a.x,0),new Vector2(b.x,a.y));dim(root,new Vector2(a.x,b.y),new Vector2(b.x,.84f));
    }
    private void dim(Transform root,Vector2 min,Vector2 max){if(max.x<=min.x || max.y<=min.y)return;var r=signalTerminal.node(root,"dim");r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;signalTerminal.image(r,new Color(0,0,0,.65f));}
    private void clearSpotlight(){if(dimmer!=null)Destroy(dimmer);}
    private void recovered(){if(state!=lesson.recovery)return;recoveryDone=true;setState(lesson.secondGuess);}
    private void Update()
    {
        if(manager==null || accountUi.isBlocking)return;
        if(state==lesson.release && !playerController.fireHeld){setState(lesson.impactArrival);manager.currentHealth=100;manager.practiceRound(3,false);}
        if(hint!=null)hint.gameObject.SetActive(hint.text.Length>0);
    }
    private void gameOver(gameSessionData data){if(state!=lesson.impact || !recoveryDone || manager.currentHealth>0)return;impactDone=true;StartCoroutine(showImpactThenRestart());}
    private IEnumerator showImpactThenRestart(){Time.timeScale=1;yield return new WaitForSeconds(2.5f);Time.timeScale=0;setState(lesson.gameOver);}
    private void restart(){if(state!=lesson.gameOver || !impactDone || !recoveryDone)return;clearSpotlight();Time.timeScale=1;manager.resetPractice();restartDone=true;beginAssessment();}
    private void beginAssessment(){manager.practiceRound(assessmentQuadrant);setState(lesson.assessment);playSignal();}
    private void layoutChanged(){if(manager!=null){manager.playerController.clearHeldInput(true);render();}}
    private void OnDestroy()
    {
        controlLayout.changed-=layoutChanged;clearSpotlight();
        if(manager!=null){manager.onPredictionSuccess-=correctPrediction;manager.onPredictionMiss-=wrongPrediction;manager.onCometCreated-=cometCreated;manager.onRecovery-=recovered;manager.onGameOver-=gameOver;manager.onPracticeRestart-=restart;manager.onAuraChanged-=auraChanged;stopCue();manager.practiceInputEnabled=false;}
        Time.timeScale=1;AudioListener.pause=previousAudioPause;if(instance==this)instance=null;
    }
}
