from pathlib import Path
import argparse,json,numpy as np
root=Path(__file__).resolve().parent
parser=argparse.ArgumentParser()
parser.add_argument("--decoded-dir",dest="decodedDir",type=Path,default=root/"decoded")
parser.add_argument("--output",type=Path,default=root/"onset-proposals.json")
args=parser.parse_args()
manifest=json.loads((args.decodedDir/"manifest.json").read_text())
results=[]
for entry in manifest["clips"]:
    signal=np.fromfile(args.decodedDir/entry["pcmFile"],dtype="<f4").reshape(-1,entry["channels"]).astype(np.float64)
    rate=entry["sampleRate"];window=round(.010*rate);step=round(.001*rate)
    power=np.mean(signal*signal,axis=1)
    sums=np.concatenate(([0.],np.cumsum(power)))
    starts=np.arange(0,min(len(signal)-window,round(.5*rate))+1,step)
    rms=np.sqrt((sums[starts+window]-sums[starts])/window)
    maximum=float(np.max(rms));threshold=maximum*10**(-30/20)
    candidates=np.flatnonzero((starts<=round(.25*rate))&(rms>=threshold))
    crossing=int(starts[candidates[0]]) if len(candidates) else 0
    proposed=max(0,crossing-round(.002*rate))
    start=proposed if proposed>round(.005*rate) else 0
    results.append({"clipName":entry["name"],"sampleRate":rate,"playbackStartFrame":start,
     "playbackStartSeconds":start/rate,"thresholdRms":threshold,"first500msMaxWindowRms":maximum,
     "thresholdCrossingFrame":crossing,"proposedFrameBeforeMateriality":proposed})
report={"method":"10ms combined-channel mean-power RMS windows, 1ms step, first window crossing -30dB relative to maximum window RMS in first500ms; search first250ms; preserve2ms lead-in; apply only starts>5ms.",
 "changedCount":sum(row["playbackStartFrame"]>0 for row in results),"clips":results}
args.output.write_text(json.dumps(report,indent=2))
print("Saved",len(results),"starts,",report["changedCount"],"nonzero")

