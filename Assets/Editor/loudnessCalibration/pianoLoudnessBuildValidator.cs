using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Put in Assets/Editor. Read-only validation of scenes already loaded by Unity;
/// no scene opening, decoding, playback, importer edits, saves or runtime dependency.
/// The profile JSON's provenance field contains the decoded-export manifest.
/// </summary>
public sealed class pianoLoudnessBuildValidator : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    // Interface callback name is required by Unity. A null report is an Editor
    // scene/play-mode processing event, not a build requiring this validation.
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null) return;
        validateScene(scene, report.summary.platform);
    }

    [MenuItem("Tools/Validate Piano Loudness Profiles")]
    public static void validateEnabledBuildScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new BuildFailedException("Piano loudness validation requires Edit mode.");
        int managerCount = 0;
        int checkedScenes = 0;
        int skippedScenes = 0;
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled) continue;
            Scene scene = SceneManager.GetSceneByPath(entry.path);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                skippedScenes++;
                continue;
            }
            managerCount += validateScene(scene, EditorUserBuildSettings.activeBuildTarget);
            checkedScenes++;
        }
        string message = "Piano loudness validation passed for " + managerCount
            + " frequencyManager component(s) across " + checkedScenes
            + " already-open enabled build scene(s); " + skippedScenes
            + " unopened enabled scene(s) skipped. Every included scene is validated during build processing.";
        if (skippedScenes > 0) Debug.LogWarning(message);
        else Debug.Log(message);
    }

    private static int validateScene(Scene scene, BuildTarget target)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (frequencyManager manager in root.GetComponentsInChildren<frequencyManager>(true))
        {
            validateManager(manager, scene.path, target);
            count++;
        }
        return count;
    }
    private static void validateManager(frequencyManager manager, string scenePath, BuildTarget target)
    {
        string context = scenePath + " / " + manager.name;
        var serialized = new SerializedObject(manager);
        SerializedProperty property = serialized.FindProperty("loudnessCalibrationData");
        var profileAsset = property == null ? null : property.objectReferenceValue as TextAsset;
        if (profileAsset == null) fail(context, "assign a generated loudness profile");
        var data = JsonUtility.FromJson<pianoLoudnessCalibration.profileData>(profileAsset.text);
        try { pianoLoudnessCalibration.createProfile(data); }
        catch (Exception error) { fail(context, "invalid runtime profile: " + error.Message); }

        var wrapper = JsonUtility.FromJson<buildProfile>(profileAsset.text);
        provenanceData provenance = wrapper == null ? null : wrapper.provenance;
        if (provenance == null || !provenance.complete || provenance.clips == null)
            fail(context, "profile is missing its complete decoded-export provenance");
        if (provenance.unityVersion != Application.unityVersion)
            fail(context, "Unity version differs from the decoded export; regenerate the profile");
        if (provenance.activeBuildTarget != target.ToString())
            fail(context, "build target differs from the decoded export (" + provenance.activeBuildTarget + "); regenerate and validate for " + target);
        if (Math.Abs((double)manager.fadeDuration - data.fadeDuration) > 1e-5
            || float.IsNaN(manager.fadeDuration) || float.IsInfinity(manager.fadeDuration))
            fail(context, "note fade differs from the generated profile");
        if (manager.audioSource == null) fail(context, "assign the note AudioSource");
        if (manager.pianoSamples == null || manager.pianoSamples.Count == 0)
            fail(context, "assign the piano recordings");

        var byGuid = new Dictionary<string, provenanceClip>(StringComparer.Ordinal);
        foreach (provenanceClip clip in provenance.clips)
        {
            if (clip == null || string.IsNullOrEmpty(clip.assetGuid) || byGuid.ContainsKey(clip.assetGuid))
                fail(context, "provenance contains a missing or duplicate clip GUID");
            if (!isSha256(clip.rawSourceSha256) || !isSha256(clip.metaSha256) || !isSha256(clip.decodedPcmSha256)
                || string.IsNullOrEmpty(clip.dependencyHash))
                fail(context, "provenance is missing input hashes for " + clip.name);
            byGuid.Add(clip.assetGuid, clip);
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (AudioClip clip in manager.pianoSamples)
        {
            if (clip == null) fail(context, "an assigned piano recording is missing");
            if (!names.Add(clip.name)) fail(context, "assigned recording names are duplicated: " + clip.name);
            string assetPath = AssetDatabase.GetAssetPath(clip);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            provenanceClip saved;
            if (!byGuid.TryGetValue(guid, out saved))
                fail(context, "recording is absent from provenance: " + assetPath);
            var response = Array.Find(data.clips, item => item.clipName == clip.name);
            if (response == null || saved.name != clip.name
                || response.playbackStartFrame != saved.playbackStartFrame
                || response.frames != clip.samples || saved.frames != clip.samples
                || response.channels != clip.channels || saved.channels != clip.channels
                || response.sampleRate != clip.frequency || saved.sampleRate != clip.frequency)
                fail(context, "recording identity or decoded dimensions changed: " + assetPath);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourcePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            if (!File.Exists(sourcePath) || !File.Exists(sourcePath + ".meta"))
                fail(context, "recording source or metadata is missing: " + assetPath);
            if (!string.Equals(hashFile(sourcePath), saved.rawSourceSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(hashFile(sourcePath + ".meta"), saved.metaSha256, StringComparison.OrdinalIgnoreCase)
                || AssetDatabase.GetAssetDependencyHash(assetPath).ToString() != saved.dependencyHash)
                fail(context, "recording source/import settings changed; regenerate calibration: " + assetPath);
        }
    }

    private static string hashFile(string path)
    {
        using (var sha256 = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static bool isSha256(string value)
    {
        if (value == null || value.Length != 64) return false;
        foreach (char character in value)
            if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')
                || (character >= 'A' && character <= 'F'))) return false;
        return true;
    }

    private static void fail(string context, string reason)
    {
        throw new BuildFailedException("Piano loudness profile validation failed at " + context + ": " + reason + ".");
    }

    // JsonUtility populates these Editor-only provenance fields.
#pragma warning disable 0649
    [Serializable]
    private sealed class buildProfile
    {
        public provenanceData provenance;
    }

    [Serializable]
    private sealed class provenanceData
    {
        public bool complete;
        public string unityVersion;
        public string activeBuildTarget;
        public provenanceClip[] clips;
    }

    [Serializable]
    private sealed class provenanceClip
    {
        public string assetGuid;
        public string name;
        public int frames;
        public int playbackStartFrame;
        public int channels;
        public int sampleRate;
        public string rawSourceSha256;
        public string metaSha256;
        public string decodedPcmSha256;
        public string dependencyHash;
    }
}
#pragma warning restore 0649
