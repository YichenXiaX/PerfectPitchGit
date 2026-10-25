import os
os.environ.setdefault("MPLCONFIGDIR",os.path.join(os.path.dirname(__file__),".cache","matplotlib"))
"""Check one official reference fixture and exact accelerated/API agreement."""
from pathlib import Path
import argparse,json
import numpy as np
from mosqito.sq_metrics import loudness_zwtv
from mosqito.utils import load
from mosqito.sq_metrics.loudness.loudness_zwtv._third_octave_levels import _third_octave_levels
from mosqito.sq_metrics.loudness.loudness_zwst._main_loudness import _main_loudness
from mosqito.sq_metrics.loudness.loudness_zwtv._temporal_weighting import _temporal_weighting
from fastModel import nonlinearDecay,slopesFast
root=Path(__file__).resolve().parent
parser=argparse.ArgumentParser()
parser.add_argument("--reference-wav",dest="referenceWav",type=Path,default=root/"reference/test10.wav")
parser.add_argument("--reference-values",dest="referenceValues",type=Path,default=root/"reference/test10-values.json")
parser.add_argument("--output",type=Path,default=root/"model-validation.json")
args=parser.parse_args()
signal,rate=load(str(args.referenceWav),wav_calib=2*np.sqrt(2))
total,specific,_,_=loudness_zwtv(signal,rate)
levels,_,_=_third_octave_levels(signal,rate)
fastTotal,fastSpecific=slopesFast(nonlinearDecay(_main_loudness(levels,"free")))
fastTotal=_temporal_weighting(fastTotal)[::4];fastSpecific=fastSpecific[:,::4]
reference=json.loads(args.referenceValues.read_text())
expectedTotal=np.array(reference["total"])
expectedSpecific=np.array(reference["specific_8_5_bark"])
totalError=np.abs(total-expectedTotal)
specificError=np.abs(specific[84]-expectedSpecific)
report={"fixture":"MoSQITo official test10:1kHz10ms70dB pulse; no alignment shift",
 "referenceTotalMaximumAbsoluteError":float(totalError.max()),
 "referenceSpecificMaximumAbsoluteError":float(specificError.max()),
 "referenceWithin5PercentOrPoint1":bool(np.all(totalError<=np.maximum(.05*expectedTotal,.1)) and np.all(specificError<=np.maximum(.05*expectedSpecific,.1))),
 "acceleratedTotalBitExact":bool(np.array_equal(total,fastTotal)),
 "acceleratedSpecificBitExact":bool(np.array_equal(specific,fastSpecific)),
 "scope":"One official fixture and exact execution comparison, not exhaustive ISO conformance certification."}
report["pass"]=report["referenceWithin5PercentOrPoint1"] and report["acceleratedTotalBitExact"] and report["acceleratedSpecificBitExact"]
args.output.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
if not report["pass"]:raise SystemExit(1)

