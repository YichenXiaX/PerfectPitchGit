"""Check actual runtime gain solutions using fresh time-varying model traces."""
import os
os.environ.setdefault("OMP_NUM_THREADS","1")
os.environ.setdefault("OPENBLAS_NUM_THREADS","1")
os.environ.setdefault("MPLCONFIGDIR",os.path.join(os.path.dirname(__file__),".cache","matplotlib"))
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
import argparse,hashlib,json,time
import numpy as np
from generateProfile import readPcm,pitchedSource,renderDuration,makeCache,fastestCached,eventValue
root=Path(__file__).resolve().parent

def validateClip(task):
    entry,cases,decodedDir=task
    signal=readPcm(entry,decodedDir);results=[]
    for case in cases:
        pitch=float(case["pitchRatio"]);duration=float(case["duration"]);gain=float(case["gain"])
        played=pitchedSource(signal,entry["sampleRate"],pitch,duration,int(case["playbackStartFrame"]))
        actual=eventValue(fastestCached(makeCache(renderDuration(played,duration)),gain))
        target=float(case["targetPeakSones"])
        if target<=0:raise ValueError("Solved-case verification expects positive non-muted targets")
        predicted=float(case.get("expectedPeakSones",target))
        results.append({**case,"actualPeakSones":actual,"relativeErrorToTarget":abs(actual-target)/target,
          "relativePredictionError":abs(actual-predicted)/max(actual,1e-8)})
    return results

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--decoded-dir",dest="decodedDir",type=Path,default=root/"decoded")
    parser.add_argument("--profile",type=Path,default=root/"loudness-profile.json")
    parser.add_argument("--cases",type=Path,default=root/"runtime-validation-cases.json")
    parser.add_argument("--output",type=Path,default=root/"runtime-model-validation.json")
    parser.add_argument("--workers",type=int,default=12)
    args=parser.parse_args()
    data=json.loads(args.cases.read_text(encoding="utf-8-sig"));cases=data["cases"]
    profile=json.loads(args.profile.read_text())
    if data["profileId"]!=profile["profileId"]:raise ValueError("Cases belong to another profile")
    manifest=json.loads((args.decodedDir/"manifest.json").read_text())
    entries={entry["name"]:entry for entry in manifest["clips"]}
    profileClips={clip["clipName"]:clip for clip in profile["clips"]}
    groups={}
    for case in cases:
        if case["playbackStartFrame"]!=profileClips[case["clipName"]]["playbackStartFrame"]:
            raise ValueError("Solved case uses another playback start")
        groups.setdefault(case["clipName"],[]).append(case)
    started=time.perf_counter();results=[]
    tasks=[(entries[name],group,str(args.decodedDir)) for name,group in groups.items()]
    with ProcessPoolExecutor(max_workers=args.workers) as pool:
        for rows in pool.map(validateClip,tasks):results.extend(rows)
    conditions={}
    for case in results:conditions.setdefault(case["conditionId"],[]).append(case)
    summary={name:{"count":len(rows),"maximumRelativeError":max(row["relativeErrorToTarget"] for row in rows),
        "meanRelativeError":float(np.mean([row["relativeErrorToTarget"] for row in rows])),
        "minimumActualSones":min(row["actualPeakSones"] for row in rows),
        "maximumActualSones":max(row["actualPeakSones"] for row in rows),
        "failures":sum(row["relativeErrorToTarget"]>.05 for row in rows)}
      for name,rows in conditions.items()}
    report={"profileId":profile["profileId"],"sourceCasesSha256":hashlib.sha256(args.cases.read_bytes()).hexdigest(),
      "caseCount":len(results),"maximumRelativeError":max(row["relativeErrorToTarget"] for row in results),
      "toleranceRelative":.05,"failureCount":sum(row["relativeErrorToTarget"]>.05 for row in results),
      "elapsedSeconds":time.perf_counter()-started,"conditions":summary,
      "worstCases":sorted(results,key=lambda row:row["relativeErrorToTarget"],reverse=True)[:50],"cases":results,
      "scope":"Fresh modeled responses of stated solved cases; not exhaustive interpolation or physical-device validation."}
    report["pass"]=report["failureCount"]==0
    args.output.write_text(json.dumps(report,indent=2))
    print(json.dumps({key:value for key,value in report.items() if key not in ("cases","worstCases","conditions")},indent=2))
    if not report["pass"]:raise SystemExit(1)
if __name__=="__main__":main()

