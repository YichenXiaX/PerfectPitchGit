using System;
using UnityEngine;

// Observes the existing game. This clock never drives movement, weapons, audio
// scheduling, scoring or trial transitions. Times are software event times.
public class researchRecorder : MonoBehaviour
{
    private gameManager manager;
    private researchTrialRecord trial;
    private double elapsedMs, lastSample;
    private bool paused;
    private bool heardTone;
    private string settingsJson;

    public void initialize(gameManager value)
    {
        manager=value;
        manager.frequencyManager.notePlayed-=notePlayed;
        manager.frequencyManager.notePlayed+=notePlayed;
    }
    private researchTrialSettings snapshot()
    {
        var settings=gameSettings.instance;var preset=settings.getCurrentPreset();
        return new researchTrialSettings {
            mode=settings.isCustomMode?"custom":"standard",controlLayout=controlLayout.leftHanded?"left":"right",
            notesPerSequence=preset.notesPerSequence,consecutiveCorrectToAdvance=preset.consecutiveCorrectToAdvance,
            allowedOctaveShifts=(int[])preset.octaveShifts.Clone(),noteDuration=preset.noteDuration,
            pauseBetweenNotes=preset.pauseBetweenNotes,predictionWindow=preset.predictionWindow,
            cometSpawnDelay=preset.cometSpawnDelay,pauseBetweenRounds=preset.pauseBetweenRounds,
            cometSpeed=preset.cometSpeed,shotInterval=manager.playerController.shotInterval
        };
    }
    public void beginTrial(int index,int quadrant)
    {
        if(!cloudAccount.instance.canRecordResearch || manager.isPractice)return;
        elapsedMs=0;lastSample=Time.realtimeSinceStartupAsDouble;paused=Time.timeScale<=0;heardTone=false;
        var settings=snapshot();settingsJson=JsonUtility.ToJson(settings);
        trial=new researchTrialRecord { roundIndex=index,correctQuadrant=quadrant,level=gameSettings.instance.level,
            startedAtUnixMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),settings=settings };
        cloudAccount.instance.recordResearch(trial);
    }
    private double now()
    {
        double sample=Time.realtimeSinceStartupAsDouble;
        if(!paused)elapsedMs+=Math.Max(0,sample-lastSample)*1000;
        lastSample=sample;paused=Time.timeScale<=0;
        if(trial!=null && paused)trial.wasPaused=true;
        return elapsedMs;
    }
    private void Update(){if(trial!=null)now();}
    private void OnApplicationFocus(bool focused){if(trial!=null && !focused)trial.lostFocus=true;}
    private void OnApplicationPause(bool value){if(trial!=null && value)trial.lostFocus=true;}
    private void notePlayed(notePlaybackRecord note,int octave)
    {
        if(trial==null)return;
        heardTone=true;trial.octaveShift=octave;note.startedAtMs=now();
    }
    private researchDecision decision(int quadrant)
    {
        var audio=heardTone?manager.frequencyManager.createLoudnessRecord():null;int count=audio?.notes.Count??0;
        int? recent=count>0?(int?)(count-1):null;
        return new researchDecision { quadrant=quadrant,atMs=now(),mostRecentToneIndex=recent,
            activeToneIndex=manager.frequencyManager.audioSource.isPlaying?recent:null };
    }
    public void prediction(int quadrant)
    {
        if(trial==null || trial.firstDecision!=null)return;
        trial.firstDecision=decision(quadrant);
        trial.predictionStatus=quadrant==trial.correctQuadrant?"correct":"wrong";
    }
    public void secondGuess(int quadrant)
    {if(trial!=null && trial.secondDecision==null)trial.secondDecision=decision(quadrant);}
    public void spawned(){if(trial!=null && trial.cometSpawnedAtMs==null)trial.cometSpawnedAtMs=now();}
    public void finish(string resolution,string interruptionReason=null)
    {
        if(trial==null)return;
        trial.resolvedAtMs=now();trial.state="resolved";trial.resolution=resolution;trial.interruptionReason=interruptionReason;
        trial.settingsChanged=settingsJson!=JsonUtility.ToJson(snapshot());
        trial.audioLoudness=heardTone?manager.frequencyManager.createLoudnessRecord():null;
        cloudAccount.instance.recordResearch(trial);trial=null;
    }
    private void OnDestroy()
    {if(manager!=null && manager.frequencyManager!=null)manager.frequencyManager.notePlayed-=notePlayed;}
}
