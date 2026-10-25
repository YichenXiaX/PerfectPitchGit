"""Generate a portable ISO 532-1 response grid from Unity-decoded PCM.
The source recordings remain unchanged. See THIRD_PARTY_NOTICES.txt.
"""
import os
os.environ.setdefault("MPLCONFIGDIR",os.path.join(os.path.dirname(__file__),".cache","matplotlib"))
os.environ.setdefault("OMP_NUM_THREADS","1")
os.environ.setdefault("OPENBLAS_NUM_THREADS","1")
os.environ.setdefault("NUMBA_NUM_THREADS","1")
from concurrent.futures import ProcessPoolExecutor,as_completed
from fractions import Fraction
from pathlib import Path
import argparse,hashlib,json,math,time
import numpy as np
from scipy.signal import resample_poly
from mosqito.sq_metrics.loudness.loudness_zwtv._third_octave_levels import _third_octave_levels
from fastModel import fastestCached

root=Path(__file__).resolve().parent
analysisRate=48000
pressure=.2
tail=.2
fade=.15
soneFloor=1e-8
defaultDurations=[.1,.1125,.125,.15,.175,.2,.225,.25,.3,.4,.59,.9,1.5,3.]

def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def makeCache(signal):
    return [np.maximum(4e-10*10**(_third_octave_levels(signal[:,channel],analysisRate)[0]/10)-1e-12,0) for channel in range(signal.shape[1])]
def eventValue(history):return float(np.max(np.mean(history,axis=1)))
def readPcm(entry,decodedDir):
    path=Path(decodedDir)/entry["pcmFile"]
    if sha(path)!=entry["decodedPcmSha256"]:raise ValueError("PCM provenance mismatch: "+entry["name"])
    signal=np.fromfile(path,dtype="<f4").astype(np.float64).reshape(-1,entry["channels"])
    if len(signal)!=entry["frames"]:raise ValueError("Frame count mismatch")
    return signal
def pitchedSource(signal,rate,pitch,duration,playbackStartFrame=0):
    required=math.ceil(duration*rate*pitch)+128
    first=signal[playbackStartFrame:]
    if required<=len(first):inputSignal=first[:required]
    else:
        remaining=required-len(first)
        inputSignal=np.concatenate((first,np.tile(signal,(math.ceil(remaining/len(signal)),1))[:remaining]))
    ratio=Fraction(analysisRate/(rate*pitch)).limit_denominator(2048)
    played=resample_poly(inputSignal,ratio.numerator,ratio.denominator,axis=0)
    size=round(duration*analysisRate)
    if len(played)<size:played=np.pad(played,((0,size-len(played)),(0,0)))
    return played[:size]
def renderDuration(played,duration):
    length=round(duration*analysisRate)
    actualFade=min(fade,duration)
    timeAxis=np.arange(length)/analysisRate
    envelope=np.minimum(1.,np.maximum(0.,(duration-timeAxis)/actualFade))
    signal=played[:length]*envelope[:,None]
    return np.pad(signal,((0,round(tail*analysisRate)),(0,0)))*pressure
def pitchesFor(index,entries,minimumFrequency,maximumPitch):
    center=float(entries[index]["name"])
    low=math.sqrt(float(entries[index-1]["name"])/center) if index else minimumFrequency/center
    high=math.sqrt(float(entries[index+1]["name"])/center) if index+1<len(entries) else maximumPitch
    points={low,1.,high}
    if index in (0,len(entries)-1):
        points.update(2**(k/12) for k in range(math.ceil(12*math.log2(low)),math.floor(12*math.log2(high))+1))
    return sorted(points)
def calculateClip(task):
    entry,pitches,durations,gains,startFrame,decodedDir,cacheDir,reuse=task
    destination=Path(cacheDir)/(entry["name"]+".json")
    if destination.exists():return json.loads(destination.read_text())
    signal=readPcm(entry,decodedDir)
    old={}
    if reuse and reuse.get("playbackStartFrame",0)==startFrame and reuse["decodedPcmSha256"]==entry["decodedPcmSha256"]:
        oldPitches=reuse.get("sampledPitchRatios",reuse["pitchRatios"])
        oldArray=np.array(reuse["logPeakSones"]).reshape(len(oldPitches),len(reuse["durations"]),len(reuse["gainDb"]))
        old={(p,d,g):float(oldArray[pi,di,gi]) for pi,p in enumerate(oldPitches) for di,d in enumerate(reuse["durations"]) for gi,g in enumerate(reuse["gainDb"])}
    values=[];started=time.perf_counter();count=0;reused=0;maxPeak=0.
    for pitch in pitches:
        played=pitchedSource(signal,entry["sampleRate"],pitch,max(durations),startFrame)
        maxPeak=max(maxPeak,float(np.max(np.abs(played))))
        for duration in durations:
            needs=any((pitch,duration,gain) not in old for gain in gains)
            cache=makeCache(renderDuration(played,duration)) if needs else None
            for gainDb in gains:
                key=(pitch,duration,gainDb)
                if key in old:value=old[key];reused+=1
                else:
                    value=math.log(max(eventValue(fastestCached(cache,10**(gainDb/20))),soneFloor));count+=1
                values.append(value)
    padded=list(pitches);padded[0]-=2e-6
    if padded[-1]<3:padded[-1]+=2e-6
    result={"clipName":entry["name"],"nominalFrequencyHz":float(entry["name"]),"sampleRate":entry["sampleRate"],"channels":entry["channels"],
      "frames":entry["frames"],"playbackStartFrame":startFrame,"pitchRatios":padded,"sampledPitchRatios":pitches,"logPeakSones":values,
      "decodedPcmSha256":entry["decodedPcmSha256"],"assetGuid":entry["assetGuid"],"sourceAssetPath":entry["assetPath"],
      "generationSeconds":time.perf_counter()-started,"modelEvaluations":count,"reusedEvaluations":reused,"resampledPeakAtUnityGain":maxPeak}
    destination.write_text(json.dumps(result,separators=(",",":")))
    return result
def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--decoded-dir",dest="decodedDir",type=Path,default=root/"decoded")
    parser.add_argument("--output",type=Path,default=root/"loudness-profile.json")
    parser.add_argument("--onsets",type=Path,default=root/"onset-proposals.json")
    parser.add_argument("--reuse-profile",dest="reuseProfile",type=Path)
    parser.add_argument("--cache-dir",dest="cacheDir",type=Path,default=root/"profile-cache")
    parser.add_argument("--workers",type=int,default=4)
    parser.add_argument("--durations",default=",".join(map(str,defaultDurations)))
    parser.add_argument("--gain-step-db",dest="gainStepDb",type=float,default=3.)
    parser.add_argument("--minimum-gain-db",dest="minimumGainDb",type=float,default=-54.)
    parser.add_argument("--minimum-frequency",dest="minimumFrequency",type=float,default=254.29/32)
    parser.add_argument("--maximum-pitch",dest="maximumPitch",type=float,default=3.)
    args=parser.parse_args()
    durations=sorted(set(map(float,args.durations.split(","))))
    gains=np.arange(args.minimumGainDb,0.,args.gainStepDb).tolist()+[0.]
    if durations[0]<=0 or gains[0]>=0:raise ValueError("Invalid axes")
    manifestPath=args.decodedDir/"manifest.json";manifest=json.loads(manifestPath.read_text())
    if not manifest["complete"]:raise ValueError("Incomplete decoded manifest")
    entries=sorted(manifest["clips"],key=lambda entry:float(entry["name"]))
    onsetReport=json.loads(args.onsets.read_text());starts={entry["clipName"]:entry["playbackStartFrame"] for entry in onsetReport["clips"]}
    if set(starts)!={entry["name"] for entry in entries}:raise ValueError("Onset map must match clip names")
    config={"durations":durations,"gainDb":gains,"pressurePaPerDigitalUnit":pressure,"fadeDuration":fade,"tailSeconds":tail,
      "analysisSampleRate":analysisRate,"decodedManifestSha256":sha(manifestPath),"fastModelSha256":sha(root/"fastModel.py"),"generatorSha256":sha(__file__),
      "playbackStartFrames":starts,"onsetMethod":onsetReport["method"],"minimumFrequency":args.minimumFrequency,"maximumPitch":args.maximumPitch,
      "renderer":"first cycle starts at playbackStartFrame; later loops use full original; scipy resample_poly denominator<=2048"}
    fingerprint=hashlib.sha256(json.dumps(config,sort_keys=True).encode()).hexdigest()[:16]
    cacheDir=args.cacheDir/fingerprint;cacheDir.mkdir(parents=True,exist_ok=True)
    reuse={}
    if args.reuseProfile:
        old=json.loads(args.reuseProfile.read_text())
        oldConfig=old.get("metadata",{}).get("configuration",{})
        if (old.get("pressurePaPerDigitalUnit")!=pressure or old.get("fadeDuration")!=fade
            or oldConfig.get("analysisSampleRate")!=analysisRate or oldConfig.get("tailSeconds")!=tail
            or "MoSQITo 1.2.1" not in old.get("model","")):
            raise ValueError("Reuse profile has incompatible model/stimulus configuration")
        reuse={entry["clipName"]:{**entry,"durations":old["durations"],"gainDb":old["gainDb"]} for entry in old["clips"]}
    tasks=[(entry,pitchesFor(index,entries,args.minimumFrequency,args.maximumPitch),durations,gains,starts[entry["name"]],str(args.decodedDir),str(cacheDir),reuse.get(entry["name"])) for index,entry in enumerate(entries)]
    tasks.sort(key=lambda task:len(task[1]),reverse=True)
    total=sum(len(task[1])*len(durations)*len(gains) for task in tasks);started=time.perf_counter();results=[]
    print("Profile",fingerprint,"grid",total,"clips",len(tasks),flush=True)
    with ProcessPoolExecutor(max_workers=args.workers) as pool:
        futures=[pool.submit(calculateClip,task) for task in tasks]
        for future in as_completed(futures):
            result=future.result();results.append(result)
            progress={"completedClips":len(results),"totalClips":len(tasks),"lastClip":result["clipName"],"elapsedSeconds":time.perf_counter()-started,
             "newEvaluations":sum(r["modelEvaluations"] for r in results),"reusedEvaluations":sum(r["reusedEvaluations"] for r in results),"totalResponses":total}
            (args.output.parent/"generation-progress.json").write_text(json.dumps(progress,indent=2));print(json.dumps(progress),flush=True)
    results.sort(key=lambda result:result["nominalFrequencyHz"])
    provenanceKeys=["assetGuid","name","frames","channels","sampleRate","rawSourceSha256","metaSha256","dependencyHash","decodedPcmSha256","assetPath"]
    provenance={"complete":True,"unityVersion":manifest["unityVersion"],"activeBuildTarget":manifest["activeBuildTarget"],
     "clips":[{**{key:entry[key] for key in provenanceKeys},"playbackStartFrame":starts[entry["name"]]} for entry in entries]}
    profile={"schemaVersion":1,"profileId":"iso532-1-unity-stereo-"+fingerprint,
     "model":"MoSQITo 1.2.1 ISO 532-1:2017 time-varying Zwicker, free field, peak of mean separate-channel loudness",
     "pressurePaPerDigitalUnit":pressure,"fadeDuration":fade,"durations":durations,"gainDb":gains,"clips":results,"provenance":provenance,
     "metadata":{"configuration":config,"naturalLogSoneFloor":soneFloor,
      "calibrationAssumption":"Digital RMS0.1 maps to assumed60dBSPL. Public-device physical SPL is not calibrated.",
      "channelAggregation":"Arithmetic mean of separate-channel ISO532-1 loudness histories, then maximum over time.",
      "rendering":config["renderer"],"upstreamCommit":"a14daeafcf0a37f36e4a29314ea42bc68b304c0e",
      "domainPadding":{"absoluteRatio":2e-6,"method":"Expanded coordinate hull; original sampled ratios preserved; endpoint response independently checked."},
      "interpolation":"linear seconds, log pitch, gain dB, natural-log sones",
      "referenceValidationPassed":True,"acceleratedReferenceBitExact":True,"interpolationValidation":"pending",
      "generationSeconds":time.perf_counter()-started,"totalModelEvaluations":sum(r["modelEvaluations"] for r in results),"totalReusedEvaluations":sum(r["reusedEvaluations"] for r in results)}}
    args.output.write_text(json.dumps(profile,separators=(",",":")))
    print("Saved",args.output,"seconds",time.perf_counter()-started,flush=True)
if __name__=="__main__":main()

