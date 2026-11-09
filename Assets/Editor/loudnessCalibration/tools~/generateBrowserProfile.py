"""Use Web Audio-rendered waveforms with the existing ISO 532-1 model.
No changes to the Editor profile, source recordings, or runtime gain solver.
"""
import os
os.environ.setdefault('OMP_NUM_THREADS','1')
os.environ.setdefault('OPENBLAS_NUM_THREADS','1')
os.environ.setdefault('NUMBA_NUM_THREADS','1')
import argparse,hashlib,json,math,time
from pathlib import Path
from concurrent.futures import ProcessPoolExecutor,as_completed
import numpy as np
from generateProfile import makeCache,eventValue,renderDuration,fastestCached,pressure,fade,tail,analysisRate,soneFloor

def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def calculateClip(task):
    source,decoded,rows,durations,gains,renderDir,cacheDir=task
    destination=Path(cacheDir)/(source['clipName']+'.json')
    if destination.exists():return json.loads(destination.read_text())
    values=[];peak=0.;started=time.perf_counter()
    for row in rows:
        file=Path(renderDir)/row['file']
        if sha(file)!=row['pcmSha256']:raise ValueError('Rendered waveform changed: '+row['file'])
        played=np.fromfile(file,dtype='<f4').reshape(-1,row['channels']).astype(float)
        if len(played)!=row['frames'] or row['sampleRate']!=analysisRate:raise ValueError('Unexpected browser rendering dimensions')
        peak=max(peak,float(np.max(np.abs(played))))
        for duration in durations:
            cache=makeCache(renderDuration(played,duration))
            for gainDb in gains:
                value=eventValue(fastestCached(cache,10**(gainDb/20)))
                values.append(math.log(max(value,soneFloor)))
    result={k:source[k] for k in ('clipName','nominalFrequencyHz','pitchRatios','sampledPitchRatios','assetGuid','sourceAssetPath')}
    result.update(sampleRate=decoded['sampleRate'],channels=decoded['channels'],frames=decoded['frames'],
      playbackStartFrame=rows[0]['playbackStartFrame'],logPeakSones=values,encodedSha256=decoded['encodedSha256'],
      resampledPeakAtUnityGain=peak,generationSeconds=time.perf_counter()-started,modelEvaluations=len(values))
    destination.write_text(json.dumps(result,separators=(',',':')))
    return result

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--source-profile',type=Path,required=True)
    parser.add_argument('--render-dir',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--cache-dir',type=Path,required=True)
    parser.add_argument('--workers',type=int,default=4)
    args=parser.parse_args()
    source=json.loads(args.source_profile.read_text())
    manifestPath=args.render_dir/'browser-grid-manifest.json'
    manifest=json.loads(manifestPath.read_text())
    if (not manifest.get('complete') or manifest['sourceProfileId']!=source['profileId']
        or manifest['sourceProfileSha256']!=sha(args.source_profile)):raise ValueError('Browser export does not match the Editor profile')
    if source['pressurePaPerDigitalUnit']!=pressure or source['fadeDuration']!=fade:raise ValueError('Model/stimulus assumptions changed')
    durations=sorted(set(source['durations']+[.65,.95]))
    config={'sourceProfileSha256':sha(args.source_profile),'browserManifestSha256':sha(manifestPath),
      'generatorSha256':sha(__file__),'modelSha256':sha(Path(__file__).with_name('fastModel.py')),
      'durations':durations,'gainDb':source['gainDb'],'pressurePaPerDigitalUnit':pressure,
      'analysisSampleRate':analysisRate,'fadeDuration':fade,'tailSeconds':tail}
    fingerprint=hashlib.sha256(json.dumps(config,sort_keys=True).encode()).hexdigest()[:16]
    cacheDir=args.cache_dir/fingerprint;cacheDir.mkdir(parents=True,exist_ok=True)
    tasks=[]
    for clip in source['clips']:
        decoded=[c for c in manifest['clips'] if c['clipName']==clip['clipName']]
        rows=[r for r in manifest['rows'] if r['clipName']==clip['clipName']]
        if len(decoded)!=1 or [r['pitchRatio'] for r in rows]!=clip['sampledPitchRatios']:raise ValueError('Incomplete/duplicate grid: '+clip['clipName'])
        if decoded[0]['sampleRate']!=analysisRate or decoded[0]['channels']!=clip['channels']:raise ValueError('Unexpected decoded format')
        tasks.append((clip,decoded[0],rows,durations,source['gainDb'],str(args.render_dir),str(cacheDir)))
    tasks.sort(key=lambda t:len(t[2]),reverse=True)
    results=[];started=time.perf_counter()
    with ProcessPoolExecutor(max_workers=args.workers) as pool:
        futures=[pool.submit(calculateClip,t) for t in tasks]
        for future in as_completed(futures):
            results.append(future.result())
            progress={'completedClips':len(results),'totalClips':len(tasks),'lastClip':results[-1]['clipName'],
              'modelEvaluations':sum(c['modelEvaluations'] for c in results),'elapsedSeconds':round(time.perf_counter()-started,1)}
            (args.output.parent/'browser-generation-progress.json').write_text(json.dumps(progress));print(json.dumps(progress),flush=True)
    results.sort(key=lambda c:c['nominalFrequencyHz'])
    profile={k:source[k] for k in ('schemaVersion','model','pressurePaPerDigitalUnit','fadeDuration','gainDb','provenance')}
    profile.update(profileId='iso532-1-browser-stereo-'+fingerprint,durations=durations,clips=results,
      browser={'sourceProfileId':source['profileId'],'sourceProfileSha256':sha(args.source_profile),
        'encoding':'AAC','sampleRate':analysisRate,'userAgent':manifest['userAgent'],
        'renderer':'Web Audio OfflineAudioContext; exact exported AAC; original onset time scaled to decoded rate; full-clip looping; ideal linear end fade',
        'validationPassed':False},
      metadata={'configuration':config,'naturalLogSoneFloor':soneFloor,
        'calibrationAssumption':source['metadata']['calibrationAssumption'],
        'channelAggregation':source['metadata']['channelAggregation'],
        'generationSeconds':time.perf_counter()-started,
        'totalModelEvaluations':sum(c['modelEvaluations'] for c in results),
        'limitations':['Public-device SPL is not calibrated.','Ideal continuous fade; validate live Unity scheduling separately.',
          'Browser engine/decoder changes require revalidation.']})
    args.output.write_text(json.dumps(profile,separators=(',',':')))
    print(json.dumps({'saved':str(args.output),'profileId':profile['profileId'],'seconds':time.perf_counter()-started}),flush=True)

if __name__=='__main__':main()
