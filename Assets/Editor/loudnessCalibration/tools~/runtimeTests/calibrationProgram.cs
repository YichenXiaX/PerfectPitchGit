using System;
using System.Collections.Generic;

return calibrationTests.run();

internal static class calibrationTests
{
    private static int count;
    private static int suites;

    public static int run()
    {
        try
        {
            suite("Explicit 2x2x2 grid/flattening/log-pitch interpolation", explicitGrid);
            suite("Analytical multilinear cross-term oracle", analyticalInterpolation);
            suite("Common feasible target and monotonic inverse across clips", commonTarget);
            suite("Interior pitch minimum and endpoint-restricted domains", interiorExtrema);
            suite("Interior minimum-gain maximum rejects infeasible condition", infeasibleInteriorFloor);
            suite("Piecewise gain inversion and plateau lower endpoint", piecewiseInverse);
            suite("Continuity across pitch/duration/gain nodes", continuity);
            suite("Muted condition and declared interval union", mutedAndIntervals);
            suite("Profile/condition deep-copy ownership", ownership);
            suite("Validated playback starts and immutable metadata", playbackStarts);
            suite("Invalid source schema, axes, dimensions and responses", invalidProfiles);
            suite("Invalid condition settings and out-of-domain queries", invalidConditions);
            suite("Model threshold and gain floor produce explicit errors", thresholdAndFloor);
            Console.WriteLine("PASS: " + suites + " calibration suites, " + count + " assertions.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL after " + count + " assertions: " + error);
            return 1;
        }
    }

    private static void explicitGrid()
    {
        var data = bare(new[] { 1f, 3f }, new[] { -20f, 0f },
            clip("tone", new[] { 1f, 4f }, new[] { -4f, -2f, -3f, 0f, -2f, -1f, -1f, 2f }));
        var profile = pianoLoudnessCalibration.createProfile(data);
        near(profile.getLogPeakSones("tone", 2f, 2f, -10), -1.375, 1e-12, "Eight-corner arithmetic mean");
        near(profile.getLogPeakSones("tone", 4f, 3f, 0), 2, 0, "Flattened last node");
        near(profile.getLogPeakSones("tone", 1f, 3f, -20), -3, 0, "Duration stride");
        near(profile.getLogPeakSones("tone", 4f, 1f, -20), -2, 0, "Pitch stride");
        near(profile.getLogPeakSones("tone", 2.5f, 2f, -10),
            -2.25 + Math.Log(2.5) / Math.Log(4) * 1.75, 1e-12, "Natural-log pitch coordinate");
    }

    private static void analyticalInterpolation()
    {
        var profile = pianoLoudnessCalibration.createProfile(analyticData());
        Random random = new Random(217);
        for (int i = 0; i < 300; i++)
        {
            float pitch = (float)(0.5 * Math.Pow(4, random.NextDouble()));
            float duration = (float)(0.5 + 2 * random.NextDouble());
            double gainDb = -60 + 60 * random.NextDouble();
            near(profile.getLogPeakSones("soft", pitch, duration, gainDb),
                analytic(0, pitch, duration, gainDb), 3e-7, "Independent multilinear expression");
            near(profile.getLogPeakSones("bright", pitch, duration, gainDb),
                analytic(0.4, pitch, duration, gainDb), 3e-7, "Second source clip");
        }
    }

    private static void commonTarget()
    {
        var profile = pianoLoudnessCalibration.createProfile(analyticData());
        float duration = 1.3f;
        float master = 0.5f;
        var intervals = new[] { interval("soft", 0.5f, 2), interval("bright", 0.5f, 2) };
        var condition = profile.createCondition(intervals, duration, 0.15f, master);
        double expectedLogTarget = analytic(0, 0.5, duration, 20 * Math.Log10(master));
        near(Math.Log(condition.targetPeakSones), expectedLogTarget, 3e-7, "Common target uses all allowed notes");
        near(condition.getGain("soft", 0.5f).gain, master, 1e-12, "Quietest permitted note uses master");
        double savedTarget = condition.targetPeakSones;
        for (int i = 0; i <= 1000; i++)
        {
            float pitch = i == 1000 ? 2f : (float)(0.5 * Math.Pow(4, i / 1000.0));
            foreach (string name in new[] { "soft", "bright" })
            {
                var result = condition.getGain(name, pitch);
                truth(result.gain > 0 && result.gain <= master, "Every gain is positive and bounded by master.");
                near(result.expectedPeakSones, savedTarget, 0, "Result reports fixed target.");
                near(profile.getLogPeakSones(name, pitch, duration, 20 * Math.Log10(result.gain)),
                    Math.Log(savedTarget), 1e-10, "Returned gain actually predicts the target in the grid.");
                double x = Math.Log(pitch);
                double intercept = (name == "soft" ? 0 : 0.4) + 0.2 * x + 0.15 * duration + 0.01 * x * duration;
                double slope = 0.03 + 0.001 * duration + 0.002 * x;
                double expectedGain = Math.Pow(10, (expectedLogTarget - intercept) / slope / 20);
                near(result.gain, expectedGain, 2e-6, "Independent analytic inverse.");
            }
        }
        near(condition.targetPeakSones, savedTarget, 0, "Queries never renormalize the condition.");
        for (int i = intervals.Length - 1; i >= 0; i--)
            near(condition.getGain(intervals[i].clipName, 1).expectedPeakSones, savedTarget, 0, "Order independence");
    }

    private static void interiorExtrema()
    {
        var data = fromFunction(new[] { 1f }, new[] { -60f, 0f }, new[] { 0.5f, 1f, 2f },
            (name, pitch, duration, gain) => (pitch == 0.5 ? 0.6 : pitch == 1 ? -0.4 : 0.8) + 0.1 * duration + 0.05 * gain,
            "tone");
        var profile = pianoLoudnessCalibration.createProfile(data);
        var condition = profile.createCondition(new[] { interval("tone", 0.5f, 2) }, 1, 0.15f, 1);
        near(Math.Log(condition.targetPeakSones), -0.3, 1e-7, "Interior grid node is the quietest.");
        near(condition.getGain("tone", 1).gain, 1, 0, "Interior quietest note uses master.");
        near(condition.getGain("tone", 0.5f).gain, 0.1, 1e-7, "Louder endpoint is attenuated.");
        near(condition.getGain("tone", 2).gain, Math.Pow(10, -24.0 / 20), 1e-7, "Other endpoint is attenuated.");

        var cropped = profile.createCondition(new[] { interval("tone", 0.75f, 1.5f) }, 1, 0.15f, 1);
        near(cropped.targetPeakSones, condition.targetPeakSones, 0, "Interior node retained between non-node endpoints.");
        var increasing = profile.createCondition(new[] { interval("tone", 1.25f, 1.75f) }, 1, 0.15f, 1);
        near(Math.Log(increasing.targetPeakSones), -0.3 + 1.2 * Math.Log(1.25) / Math.Log(2), 1e-7,
            "Restricted interval minimum comes from its actual endpoint.");
    }

    private static void infeasibleInteriorFloor()
    {
        // At master, endpoints have log loudness 0; the interior pitch has 2.
        // Its minimum-gain response is 1, so the endpoint-limited target 0 is unreachable.
        var data = bare(new[] { 1f }, new[] { -60f, 0f },
            clip("tone", new[] { 0.5f, 1f, 2f }, new[] { -5f, 0f, 1f, 2f, -5f, 0f }));
        var profile = pianoLoudnessCalibration.createProfile(data);
        throws<InvalidOperationException>(() => profile.createCondition(
            new[] { interval("tone", 0.5f, 2) }, 1, 0.15f, 1));
        // Removing the infeasible interior pitch by using two singleton intervals is valid.
        var endpointsOnly = profile.createCondition(
            new[] { interval("tone", 0.5f, 0.5f), interval("tone", 2, 2) }, 1, 0.15f, 1);
        near(endpointsOnly.targetPeakSones, 1, 0, "Exact declared union, not its convex hull.");
    }

    private static void piecewiseInverse()
    {
        var data = bare(new[] { 1f }, new[] { -60f, -20f, -5f, 0f },
            clip("soft", new[] { 1f }, new[] { -8f, -3f, -1f, 0f }),
            clip("bright", new[] { 1f }, new[] { -5f, -1f, 1f, 2f }));
        var profile = pianoLoudnessCalibration.createProfile(data);
        var allowed = new[] { interval("soft", 1, 1), interval("bright", 1, 1) };
        var condition = profile.createCondition(allowed, 1, 0.15f, 1);
        near(condition.targetPeakSones, 1, 0, "Singleton common target.");
        near(condition.getGain("bright", 1).gain, Math.Pow(10, -12.5 / 20), 1e-12,
            "Inverse uses the correct piecewise gain segment.");

        data.clips[1].logPeakSones = new[] { -5f, 0f, 0f, 2f };
        var plateau = pianoLoudnessCalibration.createProfile(data).createCondition(allowed, 1, 0.15f, 1);
        near(plateau.getGain("bright", 1).gain, 0.1, 1e-12, "First gain attaining plateau target.");

        data.clips[1].logPeakSones = new[] { 0f, 0f, 0f, 2f };
        var floorPlateau = pianoLoudnessCalibration.createProfile(data).createCondition(allowed, 1, 0.15f, 1);
        near(floorPlateau.getGain("bright", 1).gain, 0.001, 1e-14, "Valid target at minimum gain.");

        var nonNodeMaster = profile.createCondition(allowed, 1, 0.15f, 0.5f);
        near(nonNodeMaster.getGain("soft", 1).gain, 0.5, 1e-12, "Master need not be a grid node.");
    }

    private static void continuity()
    {
        var profile = pianoLoudnessCalibration.createProfile(analyticData());
        var condition = profile.createCondition(
            new[] { interval("soft", 0.5f, 2), interval("bright", 0.5f, 2) }, 1.5f, 0.15f, 0.731f);
        foreach (float pitch in new[] { 0.9999f, 1f, 1.0001f })
        {
            var answer = condition.getGain("bright", pitch);
            near(profile.getLogPeakSones("bright", pitch, 1.5f, 20 * Math.Log10(answer.gain)),
                Math.Log(condition.targetPeakSones), 1e-10, "Target continuous at pitch node.");
        }
        near(condition.getGain("bright", 0.9999f).gain, condition.getGain("bright", 1.0001f).gain,
            0.001, "No gain jump at pitch node.");
        foreach (float duration in new[] { 1.4999f, 1.5f, 1.5001f })
        foreach (double gain in new[] { -30.0001, -30.0, -29.9999 })
            near(profile.getLogPeakSones("soft", 1.25f, duration, gain),
                analytic(0, 1.25, duration, gain), 3e-7, "No duration/gain knot discontinuity.");
    }

    private static void mutedAndIntervals()
    {
        var profile = pianoLoudnessCalibration.createProfile(analyticData());
        var allowed = new[] { interval("soft", 0.5f, 0.75f), interval("soft", 1.5f, 2) };
        var muted = profile.createCondition(allowed, 1, 0.15f, 0);
        truth(muted.isMuted, "Explicit mute flag.");
        near(muted.targetPeakSones, 0, 0, "Muted target is zero.");
        near(muted.getGain("soft", 0.5f).gain, 0, 0, "Muted gain is zero.");
        near(muted.getGain("soft", 2).expectedPeakSones, 0, 0, "Muted metric is zero.");
        throws<ArgumentOutOfRangeException>(() => muted.getGain("soft", 1));
        throws<ArgumentOutOfRangeException>(() => muted.getGain("bright", 1));
        throws<ArgumentException>(() => muted.getGain("unknown", 1));
        throws<ArgumentOutOfRangeException>(() => muted.getGain("soft", float.NaN));
    }

    private static void ownership()
    {
        var data = analyticData();
        var profile = pianoLoudnessCalibration.createProfile(data);
        var allowed = new[] { interval("soft", 0.5f, 2) };
        var condition = profile.createCondition(allowed, 1, 0.15f, 1);
        double before = condition.getGain("soft", 1).gain;
        double beforeQuery = profile.getLogPeakSones("soft", 1, 1, -20);
        data.durations[0] = float.NaN;
        data.gainDb[0] = 100;
        data.clips[0].pitchRatios[0] = 100;
        data.clips[0].logPeakSones[0] = float.PositiveInfinity;
        data.clips[0].clipName = "changed";
        data.clips = null;
        allowed[0] = interval("bright", 1, 1);
        near(condition.getGain("soft", 1).gain, before, 0, "DTO/list mutations cannot alter the condition.");
        near(profile.getLogPeakSones("soft", 1, 1, -20), beforeQuery, 0, "Profile owns copied arrays.");
    }

    private static void playbackStarts()
    {
        var data = analyticData();
        var legacy = pianoLoudnessCalibration.createProfile(data);
        near(legacy.getPlaybackStartFrame("soft"), 0, 0, "Missing start field defaults to frame zero.");
        data.clips[0].playbackStartFrame = 4321;
        data.clips[1].playbackStartFrame = data.clips[1].frames - 1;
        var profile = pianoLoudnessCalibration.createProfile(data);
        near(profile.getPlaybackStartFrame("soft"), 4321, 0, "Nonzero source-frame start retained.");
        near(profile.getPlaybackStartFrame("bright"), data.clips[1].frames - 1, 0, "Last source frame is valid.");
        data.clips[0].playbackStartFrame = -1;
        data.clips[1].playbackStartFrame = 0;
        near(profile.getPlaybackStartFrame("soft"), 4321, 0, "Profile owns immutable playback-start metadata.");
        throws<ArgumentException>(() => profile.getPlaybackStartFrame("unknown"));
        invalid(source => source.clips[0].playbackStartFrame = -1);
        invalid(source => source.clips[0].playbackStartFrame = source.clips[0].frames);
        invalid(source => source.clips[0].playbackStartFrame = int.MaxValue);
    }

    private static void invalidProfiles()
    {
        throws<ArgumentNullException>(() => pianoLoudnessCalibration.createProfile(null));
        invalid(data => data.schemaVersion = 2);
        invalid(data => data.profileId = " ");
        invalid(data => data.model = null);
        invalid(data => data.pressurePaPerDigitalUnit = 0);
        invalid(data => data.pressurePaPerDigitalUnit = float.NaN);
        invalid(data => data.fadeDuration = -1);
        invalid(data => data.fadeDuration = float.PositiveInfinity);
        invalid(data => data.durations = null);
        invalid(data => data.durations = new[] { 1f, 1f });
        invalid(data => data.durations = new[] { 2f, 1f });
        invalid(data => data.durations = new[] { 0f });
        invalid(data => data.durations[1] = float.NaN);
        invalid(data => data.gainDb = new[] { 0f });
        invalid(data => data.gainDb = new[] { -60f, -30f, -1f });
        invalid(data => data.gainDb = new[] { -60f, 0f, 1f });
        invalid(data => data.gainDb[0] = float.NegativeInfinity);
        invalid(data => data.clips = null);
        invalid(data => data.clips = new pianoLoudnessCalibration.clipData[0]);
        invalid(data => data.clips[0] = null);
        invalid(data => data.clips[1].clipName = data.clips[0].clipName);
        invalid(data => data.clips[0].clipName = "");
        invalid(data => data.clips[0].nominalFrequencyHz = -1);
        invalid(data => data.clips[0].sampleRate = 0);
        invalid(data => data.clips[0].channels = 0);
        invalid(data => data.clips[0].frames = 0);
        invalid(data => data.clips[0].pitchRatios = new[] { 1f, 1f });
        invalid(data => data.clips[0].pitchRatios[0] = 0);
        invalid(data => data.clips[0].pitchRatios[1] = float.NaN);
        invalid(data => data.clips[0].logPeakSones = null);
        invalid(data => data.clips[0].logPeakSones = new[] { 1f });
        invalid(data => data.clips[0].logPeakSones[0] = float.NaN);
        invalid(data => data.clips[0].logPeakSones[0] = float.PositiveInfinity);
        invalid(data => data.clips[0].logPeakSones[0] = 1000);
        invalid(data => data.clips[0].logPeakSones[1] = data.clips[0].logPeakSones[0] - 1);
    }

    private static void invalidConditions()
    {
        var profile = pianoLoudnessCalibration.createProfile(analyticData());
        var allowed = new[] { interval("soft", 0.5f, 2) };
        throws<ArgumentNullException>(() => profile.createCondition(null, 1, 0.15f, 1));
        throws<ArgumentException>(() => profile.createCondition(new pianoLoudnessCalibration.allowedPitchInterval[0], 1, 0.15f, 1));
        throws<ArgumentException>(() => profile.createCondition(new pianoLoudnessCalibration.allowedPitchInterval[] { null }, 1, 0.15f, 1));
        throws<ArgumentException>(() => profile.createCondition(new[] { interval("soft", 2, 1) }, 1, 0.15f, 1));
        throws<ArgumentException>(() => profile.createCondition(new[] { interval("missing", 1, 1) }, 1, 0.15f, 1));
        foreach (float invalid in new[] { 0f, 0.49f, 2.51f, float.NaN, float.PositiveInfinity })
            throws<ArgumentException>(() => profile.createCondition(allowed, invalid, 0.15f, 1));
        foreach (float invalid in new[] { -1f, 0f, 0.1501f, float.NaN, float.PositiveInfinity })
            throws<ArgumentException>(() => profile.createCondition(allowed, 1, invalid, 1));
        foreach (float invalid in new[] { -1f, 1.01f, float.NaN, float.PositiveInfinity })
            throws<ArgumentException>(() => profile.createCondition(allowed, 1, 0.15f, invalid));
        throws<ArgumentException>(() => profile.createCondition(allowed, 1, 0.15f, 0.00001f));
        foreach (float invalid in new[] { 0f, 0.49f, 2.01f, float.NaN, float.PositiveInfinity })
            throws<ArgumentException>(() => profile.createCondition(new[] { interval("soft", invalid, invalid) }, 1, 0.15f, 1));
        var condition = profile.createCondition(allowed, 1, 0.150009f, 1);
        truth(condition.targetPeakSones > 0, "Fade within requested tolerance accepted.");
        throws<ArgumentException>(() => condition.getGain(null, 1));
        throws<ArgumentException>(() => profile.getLogPeakSones("soft", 1, 1, double.NaN));
        throws<ArgumentException>(() => profile.getLogPeakSones("soft", 1, 1, -60.0001));
        throws<ArgumentException>(() => profile.getLogPeakSones("soft", 1, 1, 0.0001));
    }

    private static void thresholdAndFloor()
    {
        var quiet = bare(new[] { 1f }, new[] { -60f, 0f }, clip("quiet", new[] { 1f }, new[] { -30f, -20f }));
        var profile = pianoLoudnessCalibration.createProfile(quiet);
        throws<InvalidOperationException>(() => profile.createCondition(new[] { interval("quiet", 1, 1) }, 1, 0.15f, 1));
        near(profile.createCondition(new[] { interval("quiet", 1, 1) }, 1, 0.15f, 0).getGain("quiet", 1).gain,
            0, 0, "Explicit mute needs no extrapolated model response.");

        var infeasible = bare(new[] { 1f }, new[] { -60f, 0f },
            clip("soft", new[] { 1f }, new[] { -5f, 0f }),
            clip("loud", new[] { 1f }, new[] { 1f, 2f }));
        var impossible = pianoLoudnessCalibration.createProfile(infeasible);
        throws<InvalidOperationException>(() => impossible.createCondition(
            new[] { interval("soft", 1, 1), interval("loud", 1, 1) }, 1, 0.15f, 1));
    }

    private static double analytic(double offset, double pitch, double duration, double gain)
    {
        double x = Math.Log(pitch);
        return offset + 0.2 * x + 0.15 * duration + 0.03 * gain
            + 0.01 * x * duration + 0.001 * duration * gain + 0.002 * x * gain;
    }

    private static pianoLoudnessCalibration.profileData analyticData()
    {
        return fromFunction(new[] { 0.5f, 1.5f, 2.5f }, new[] { -60f, -30f, 0f }, new[] { 0.5f, 1f, 2f },
            (name, pitch, duration, gain) => analytic(name == "soft" ? 0 : 0.4, pitch, duration, gain),
            "soft", "bright");
    }

    private static pianoLoudnessCalibration.profileData fromFunction(
        float[] durations, float[] gains, float[] pitches, Func<string, double, double, double, double> function,
        params string[] names)
    {
        var clips = new pianoLoudnessCalibration.clipData[names.Length];
        for (int c = 0; c < names.Length; c++)
        {
            List<float> responses = new List<float>();
            foreach (float pitch in pitches)
            foreach (float duration in durations)
            foreach (float gain in gains)
                responses.Add((float)function(names[c], pitch, duration, gain));
            clips[c] = clip(names[c], (float[])pitches.Clone(), responses.ToArray());
        }
        return bare(durations, gains, clips);
    }

    private static pianoLoudnessCalibration.profileData bare(
        float[] durations, float[] gains, params pianoLoudnessCalibration.clipData[] clips)
    {
        return new pianoLoudnessCalibration.profileData
        {
            schemaVersion = 1, profileId = "test-profile", model = "ISO532-1 peak(mean channel histories)",
            pressurePaPerDigitalUnit = 1, fadeDuration = 0.15f,
            durations = durations, gainDb = gains, clips = clips
        };
    }

    private static pianoLoudnessCalibration.clipData clip(string name, float[] pitches, float[] responses)
    {
        return new pianoLoudnessCalibration.clipData
        {
            clipName = name, nominalFrequencyHz = 440, sampleRate = 48000, channels = 2, frames = 48000,
            pitchRatios = pitches, logPeakSones = responses
        };
    }

    private static pianoLoudnessCalibration.allowedPitchInterval interval(string name, float min, float max)
    {
        return new pianoLoudnessCalibration.allowedPitchInterval(name, min, max);
    }

    private static void invalid(Action<pianoLoudnessCalibration.profileData> mutate)
    {
        var data = analyticData();
        mutate(data);
        throws<ArgumentException>(() => pianoLoudnessCalibration.createProfile(data));
    }

    private static void suite(string name, Action action)
    {
        action();
        suites++;
        Console.WriteLine("PASS " + name);
    }
    private static void truth(bool condition, string name)
    {
        count++;
        if (!condition) throw new InvalidOperationException(name);
    }
    private static void near(double actual, double expected, double tolerance, string name)
    {
        count++;
        if (double.IsNaN(actual) || double.IsInfinity(actual) || Math.Abs(actual - expected) > tolerance)
            throw new InvalidOperationException(name + ": expected " + expected.ToString("R")
                + ", got " + actual.ToString("R") + ", tolerance " + tolerance);
    }
    private static void throws<exceptionType>(Action action) where exceptionType : Exception
    {
        count++;
        try { action(); }
        catch (exceptionType) { return; }
        throw new InvalidOperationException("Expected " + typeof(exceptionType).Name);
    }
}
