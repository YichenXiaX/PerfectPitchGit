using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

return realProfileHarness.run(args);

internal static class realProfileHarness
{
    private static readonly float[,] quadrants = {
        {427.65f,508.57f,466.16f}, {359.61f,427.65f,392.00f},
        {302.40f,359.61f,329.63f}, {254.29f,302.40f,277.18f}
    };
    private static readonly float[] durations = {.1f,.125f,.15f,.2f,.25f,.3f,.4f,.59f,.9f,1.5f,3f,.11f,.225f,.35f,.7f,2.5f};
    private static readonly float[] masters = {0f,.1f,.5f,.8f,1f};
    private static readonly HashSet<float> heldOutDurations = new HashSet<float> {.11f,.225f,.35f,.7f,2.5f};
    private static readonly JsonSerializerOptions json = new JsonSerializerOptions {IncludeFields=true,WriteIndented=true};
    private static readonly List<object> conditionReports = new List<object>();
    private static readonly List<exportCase> exported = new List<exportCase>();
    private static readonly List<exportCase> defaultExtras = new List<exportCase>();
    private static pianoLoudnessCalibration.profileData data;
    private static pianoLoudnessCalibration.profile profile;
    private static pianoLoudnessCalibration.clipData[] clips;
    private static long testedNotes;
    private static int conditionCount;
    private static int createdCount;
    private static int failedCount;
    private static int noteFailureCount;
    private static bool defaultPassed;
    private static double defaultTarget;

    public static int run(string[] args)
    {
        if (args.Length != 2) { Console.Error.WriteLine("Usage: realProfileRegression <profile.json> <output-directory>"); return 2; }
        string profilePath = Path.GetFullPath(args[0]);
        string outputDirectory = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(outputDirectory);
        if (!File.Exists(profilePath)) { Console.Error.WriteLine("Profile is not ready: "+profilePath); return 2; }
        try
        {
            data=JsonSerializer.Deserialize<pianoLoudnessCalibration.profileData>(File.ReadAllText(profilePath),json);
            profile=pianoLoudnessCalibration.createProfile(data);
            clips=data.clips.OrderBy(c=>nominal(c.clipName)).ToArray();
            foreach(var clip in clips)
                if(nominal(clip.clipName)!=clip.nominalFrequencyHz)
                    throw new InvalidOperationException("Nominal frequency metadata differs from float clip-name parsing: "+clip.clipName);

            var groups=new List<(string name,int[] shifts)> {("default",new[]{-2,-1,0,1,2})};
            for(int shift=-5;shift<=5;shift++)groups.Add(("octave"+shift.ToString(CultureInfo.InvariantCulture),new[]{shift}));
            groups.Add(("allOctaves",Enumerable.Range(-5,11).ToArray()));
            foreach(var group in groups)
            {
                var intervals=buildIntervals(group.shifts);
                foreach(float duration in durations)
                foreach(float master in masters)
                    checkCondition(group.name,group.shifts,intervals,duration,master);
            }

            // One representative per successful non-muted condition gives broad
            // coverage. Default-condition boundary/interior extras bring the total
            // toward 1000 without letting one setting dominate expensive model checks.
            int extraCount=Math.Min(Math.Max(0,1000-exported.Count),defaultExtras.Count);
            for(int index=0;index<extraCount;index++)
            {
                // Even coverage across the ascending frequency list includes both
                // boundary ends, instead of spending the budget on only low notes.
                int selected=extraCount==1?0:(int)((long)index*(defaultExtras.Count-1)/(extraCount-1));
                exported.Add(defaultExtras[selected]);
            }
            var diagnostics=scanGrid();
            var report=new {
                schemaVersion=1,profileId=data.profileId,profilePath,completedAtUtc=DateTime.UtcNow,
                intervalConstruction="Matches scratch frequencyManager: float name parsing, double geometric cells, four float quadrant bounds, requested octave shifts, float pitch with cap3.",
                nearestClipSelection="Ascending nominal clip order; single-precision log-distance, matching runtime arithmetic apart from exact-tie original-list ordering.",
                conditionCount,createdCount,failedCount,noteFailureCount,testedNotes,
                defaultDuration=.59f,defaultMaster=.8f,defaultPassed,defaultTargetPeakSones=defaultTarget,
                exportedCases=exported.Count,grid=diagnostics,conditions=conditionReports
            };
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory,"runtime-validation-report.json"),JsonSerializer.Serialize(report,json));
            File.WriteAllText(Path.Combine(outputDirectory,"runtime-validation-cases.json"),JsonSerializer.Serialize(new {
                schemaVersion=1,profileId=data.profileId,model=data.model,pressurePaPerDigitalUnit=data.pressurePaPerDigitalUnit,
                note="gain is the System.Single source-volume value actually assigned by Unity; solvedGain retains the double inverse. Expected values are grid predictions, not independent waveform validation.",
                cases=exported
            },json));
            Console.WriteLine("Profile "+data.profileId+": "+conditionCount+" conditions; "+createdCount+" created; "+failedCount+" configuration failures; "+noteFailureCount+" note failures; "+testedNotes+" note checks.");
            Console.WriteLine("Default .59 s / .8 master: "+(defaultPassed?"PASS":"FAIL")+"; target "+defaultTarget.ToString("R")+" sones.");
            Console.WriteLine("Exported "+exported.Count+" independent-model cases to "+outputDirectory);
            return defaultPassed && failedCount==0 && noteFailureCount==0 ? 0 : 1;
        }
        catch(Exception error)
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory,"runtime-validation-report.json"),
                JsonSerializer.Serialize(new {profilePath,failedAt="profileValidation",errorType=error.GetType().Name,error=error.Message},json));
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void checkCondition(string group,int[] shifts,List<intervalRecord> intervals,float duration,float master)
    {
        conditionCount++;
        string id=group+"|duration="+duration.ToString("R",CultureInfo.InvariantCulture)+"|master="+master.ToString("R",CultureInfo.InvariantCulture);
        pianoLoudnessCalibration.condition condition;
        try {condition=profile.createCondition(intervals.Select(i=>i.allowed).ToArray(),duration,data.fadeDuration,master);}
        catch(Exception error)
        {
            failedCount++;
            conditionReports.Add(new {conditionId=id,group,octaveShifts=shifts,duration,masterVolume=master,
                heldOutDuration=heldOutDurations.Contains(duration),status="configurationRejected",
                errorType=error.GetType().Name,error=error.Message,intervalCount=intervals.Count});
            return;
        }
        createdCount++;
        var failures=new List<object>();
        int localFailures=0;
        long beforeNotes=testedNotes;
        double minGain=double.PositiveInfinity,maxGain=0;
        Action<string,float,float,string> test=(clipName,pitch,requested,kind)=>{
            testedNotes++;
            try
            {
                var answer=condition.getGain(clipName,pitch);
                if(answer.gain<0||answer.gain>master||double.IsNaN(answer.gain)||double.IsInfinity(answer.gain))
                    throw new InvalidOperationException("Gain outside declared master bound.");
                if(answer.expectedPeakSones!=condition.targetPeakSones)
                    throw new InvalidOperationException("Returned metric differs from common target.");
                if(master>0)
                {
                    // Check inverse using the double answer to isolate runtime math.
                    double response=profile.getLogPeakSones(clipName,pitch,duration,20*Math.Log10(answer.gain));
                    if(Math.Abs(response-Math.Log(condition.targetPeakSones))>1e-8)
                        throw new InvalidOperationException("Inverse does not predict the fixed target.");
                }
                minGain=Math.Min(minGain,answer.gain);maxGain=Math.Max(maxGain,answer.gain);
            }
            catch(Exception error)
            {
                localFailures++;
                if(failures.Count<12)failures.Add(new {clipName,pitchRatio=pitch,requestedFrequencyHz=requested,kind,errorType=error.GetType().Name,error=error.Message});
            }
        };

        foreach(var interval in intervals)
        {
            var points=new SortedSet<float>{interval.allowed.minPitchRatio,interval.allowed.maxPitchRatio};
            foreach(float pitch in interval.clip.pitchRatios)
                if(pitch>interval.allowed.minPitchRatio&&pitch<interval.allowed.maxPitchRatio)points.Add(pitch);
            float[] nodes=points.ToArray();
            for(int i=1;i<nodes.Length;i++)points.Add((float)Math.Sqrt(nodes[i-1]*(double)nodes[i]));
            foreach(float pitch in points)
                test(interval.clip.clipName,pitch,interval.nominal*pitch,"declaredInterval");
        }

        var actualFrequencies=new SortedSet<float>();
        foreach(int shift in shifts)
        for(int q=0;q<4;q++)
        {
            float octave=(float)Math.Pow(2,shift);
            float low=quadrants[q,0]*octave,high=quadrants[q,1]*octave;
            actualFrequencies.Add(low);actualFrequencies.Add(high);actualFrequencies.Add(quadrants[q,2]*octave);
            foreach(float fraction in new[]{.13f,.37f,.63f,.87f})
                actualFrequencies.Add((quadrants[q,0]+(quadrants[q,1]-quadrants[q,0])*fraction)*octave);
            for(int c=1;c<clips.Length;c++)
            {
                float edge=(float)Math.Sqrt(nominal(clips[c-1].clipName)*(double)nominal(clips[c].clipName));
                foreach(float frequency in new[]{MathF.BitDecrement(edge),edge,MathF.BitIncrement(edge)})
                    if(frequency>=low&&frequency<=high)actualFrequencies.Add(frequency);
            }
        }
        foreach(float requested in actualFrequencies)
        {
            var note=selectNote(requested);
            test(note.clipName,note.pitch,requested,"actualFloatSelection");
        }
        noteFailureCount+=localFailures;
        bool isDefault=group=="default"&&duration==.59f&&master==.8f;
        if(isDefault){defaultPassed=localFailures==0;defaultTarget=condition.targetPeakSones;}
        conditionReports.Add(new {
            conditionId=id,group,octaveShifts=shifts,duration,masterVolume=master,heldOutDuration=heldOutDurations.Contains(duration),
            status=localFailures==0?"validated":"noteValidationFailed",intervalCount=intervals.Count,targetPeakSones=condition.targetPeakSones,
            minGain=double.IsInfinity(minGain)?0:minGain,maxGain,notesChecked=testedNotes-beforeNotes,noteFailures=localFailures,examples=failures
        });

        if(master>0)
        {
            // Use an interior requested frequency, then select the actual source
            // through the same float nearest-clip arithmetic as playback.
            var chosen=intervals[(conditionCount*17)%intervals.Count];
            float requested=(float)Math.Sqrt(chosen.requestedMinimum*chosen.requestedMaximum);
            try{exported.Add(makeCase(id,group,condition,requested,"conditionRepresentative"));}
            catch(Exception){/* Recorded note checks above remain authoritative. */}
            if(group=="allOctaves"&&master==.1f&&(duration==.1f||duration==.11f))
            {
                var low=intervals.Where(i=>i.clip.clipName=="27.50").OrderBy(i=>i.allowed.minPitchRatio).First();
                exported.Add(makeExplicitCase(id,group,condition,low.clip.clipName,low.allowed.minPitchRatio,
                    (float)low.requestedMinimum,"fullRangeSoftTargetEndpoint"));
                var bright=intervals.Where(i=>i.clip.clipName=="739.99").OrderByDescending(i=>i.allowed.maxPitchRatio).First();
                exported.Add(makeExplicitCase(id,group,condition,bright.clip.clipName,bright.allowed.maxPitchRatio,
                    (float)bright.requestedMaximum,"fullRangeGainFloorEndpoint"));
            }
            if(isDefault)
            {
                foreach(float frequency in actualFrequencies)
                {
                    try{defaultExtras.Add(makeCase(id,group,condition,frequency,"defaultBoundaryOrInterior"));}
                    catch(Exception){ }
                }
            }
        }
    }

    private static exportCase makeCase(string id,string group,pianoLoudnessCalibration.condition condition,float requested,string kind)
    {
        var note=selectNote(requested);
        return makeExplicitCase(id,group,condition,note.clipName,note.pitch,requested,kind);
    }

    private static exportCase makeExplicitCase(string id,string group,pianoLoudnessCalibration.condition condition,
        string clipName,float pitch,float requested,string kind)
    {
        var answer=condition.getGain(clipName,pitch);
        float unityGain=(float)answer.gain;
        if(unityGain==0&&answer.gain>0)throw new InvalidOperationException("Gain underflows Unity float.");
        return new exportCase {
            caseId="runtime-"+(exported.Count+defaultExtras.Count).ToString("D5"),conditionId=id,group=group,sampleKind=kind,
            clipName=clipName,playbackStartFrame=profile.getPlaybackStartFrame(clipName),nominalFrequencyHz=nominal(clipName),requestedFrequencyHz=requested,
            nominalPlaybackFrequencyHz=nominal(clipName)*pitch,pitchRatio=pitch,duration=condition.duration,
            fadeDuration=condition.fadeDuration,masterVolume=condition.masterVolume,gain=unityGain,solvedGain=answer.gain,
            targetPeakSones=condition.targetPeakSones,expectedPeakSones=answer.expectedPeakSones,
            heldOutDuration=heldOutDurations.Contains(condition.duration)
        };
    }

    private static List<intervalRecord> buildIntervals(int[] shifts)
    {
        var answer=new List<intervalRecord>();
        for(int i=0;i<clips.Length;i++)
        {
            var clip=clips[i];float center=nominal(clip.clipName);
            double cellMin=i==0?0:Math.Sqrt(nominal(clips[i-1].clipName)*(double)center);
            double cellMax=i==clips.Length-1?double.PositiveInfinity:Math.Sqrt(center*(double)nominal(clips[i+1].clipName));
            foreach(int shift in shifts)
            for(int q=0;q<4;q++)
            {
                double octave=Math.Pow(2,shift);
                double low=Math.Max(cellMin,quadrants[q,0]*octave),high=Math.Min(cellMax,quadrants[q,1]*octave);
                if(low>high)continue;
                float minPitch=(float)Math.Min(low/center,3),maxPitch=(float)Math.Min(high/center,3);
                answer.Add(new intervalRecord {
                    allowed=new pianoLoudnessCalibration.allowedPitchInterval(clip.clipName,minPitch,maxPitch),
                    clip=clip,nominal=center,requestedMinimum=low,requestedMaximum=high
                });
            }
        }
        return answer;
    }

    private static (string clipName,float pitch) selectNote(float requested)
    {
        string closest=null;float distance=float.MaxValue;
        foreach(var clip in clips)
        {
            float candidate=MathF.Abs(MathF.Log(requested/nominal(clip.clipName)));
            if(candidate<distance){distance=candidate;closest=clip.clipName;}
        }
        return (closest,Math.Min(requested/nominal(closest),3f));
    }

    private static object scanGrid()
    {
        long responseCount=0,plateauCount=0,belowThresholdCount=0;
        double minLog=double.PositiveInfinity,maxLog=double.NegativeInfinity;
        foreach(var clip in clips)
        {
            responseCount+=clip.logPeakSones.Length;
            for(int p=0;p<clip.pitchRatios.Length;p++)
            for(int d=0;d<data.durations.Length;d++)
            for(int g=0;g<data.gainDb.Length;g++)
            {
                int index=(p*data.durations.Length+d)*data.gainDb.Length+g;
                double value=clip.logPeakSones[index];
                minLog=Math.Min(minLog,value);maxLog=Math.Max(maxLog,value);
                if(value<=Math.Log(1e-6))belowThresholdCount++;
                if(g>0&&value==clip.logPeakSones[index-1])plateauCount++;
            }
        }
        return new {clipCount=clips.Length,responseCount,plateauCount,belowThresholdCount,minLog,maxLog,
            durationAxis=data.durations,gainDbAxis=data.gainDb};
    }

    private static float nominal(string name){return float.Parse(name,NumberStyles.Float,CultureInfo.InvariantCulture);}

    private sealed class intervalRecord
    {
        internal pianoLoudnessCalibration.allowedPitchInterval allowed;
        internal pianoLoudnessCalibration.clipData clip;
        internal float nominal;
        internal double requestedMinimum,requestedMaximum;
    }

    private sealed class exportCase
    {
        public string caseId,conditionId,group,sampleKind,clipName;
        public int playbackStartFrame;
        public float nominalFrequencyHz,requestedFrequencyHz,nominalPlaybackFrequencyHz,pitchRatio,duration,fadeDuration,masterVolume,gain;
        public double solvedGain,targetPeakSones,expectedPeakSones;
        public bool heldOutDuration;
    }
}
