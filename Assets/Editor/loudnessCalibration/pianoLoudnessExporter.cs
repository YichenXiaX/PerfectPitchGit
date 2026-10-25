using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only export of imported decoded piano samples. Put this file in Assets/Editor.
/// Sources, importer settings and scenes are read only; audio is never played.
/// The selected folder receives interleaved float32 PCM and a complete provenance manifest.
/// </summary>
public static class pianoLoudnessExporter
{
    [MenuItem("Tools/Export Piano Loudness Inputs")]
    public static void exportInputs()
    {
        try
        {
            if (!canExportInputs())
                throw new InvalidOperationException("Wait for Edit mode, compilation and asset updates before exporting.");
            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDirectory = EditorUtility.OpenFolderPanel(
                "Export Piano Loudness Inputs - outside Assets recommended", projectPath, "decoded");
            if (string.IsNullOrEmpty(outputDirectory)) return;
            string manifestPath = exportClips(outputDirectory);
            Debug.Log("Piano loudness inputs exported without playback or source changes: " + manifestPath);
            EditorUtility.DisplayDialog("Piano loudness export complete",
                "Decoded inputs and manifest saved to:\n" + outputDirectory, "OK");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorUtility.DisplayDialog("Piano loudness export could not finish", error.Message, "OK");
        }
    }

    [MenuItem("Tools/Export Piano Loudness Inputs", true)]
    private static bool canExportInputs()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying
            && !EditorApplication.isCompiling && !EditorApplication.isUpdating;
    }

    private static string exportClips(string outputDirectory)
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.Application.isPlaying)
            throw new System.InvalidOperationException("Export requires Edit mode with no pending Play transition.");
        if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
            throw new System.InvalidOperationException("Wait for Unity compilation and asset updates to finish.");
        
        var projectPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, ".."));
        
        System.Func<byte[], string> hashBytes = bytes =>
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
                return System.BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        };
        System.Func<string, string> hashFile = path =>
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            using (var stream = System.IO.File.OpenRead(path))
                return System.BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        };
        System.Func<string, string> hashOptionalFile = path => System.IO.File.Exists(path) ? hashFile(path) : null;
        
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
            throw new System.InvalidOperationException("Open the scene containing the piano frequencyManager first.");
        var managers = activeScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<frequencyManager>(true)).ToArray();
        if (managers.Length != 1)
            throw new System.InvalidOperationException("The active scene must contain exactly one frequencyManager; found " + managers.Length + ".");
        var managerInstance = managers[0];
        if (managerInstance.pianoSamples == null || managerInstance.pianoSamples.Count == 0)
            throw new System.InvalidOperationException("frequencyManager has no assigned piano samples.");
        var assignedClips = managerInstance.pianoSamples.ToArray();
        if (assignedClips.Any(clip => clip == null))
            throw new System.InvalidOperationException("The assigned sample list contains a missing clip.");
        var assignedNames = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        var assignedFrequencies = new System.Collections.Generic.HashSet<float>();
        var assignedGuids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (var clip in assignedClips)
        {
            float frequency;
            if (!float.TryParse(clip.name, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out frequency)
                || frequency <= 0 || float.IsNaN(frequency) || float.IsInfinity(frequency))
                throw new System.InvalidOperationException("Piano clips need positive frequency names; invalid name: " + clip.name);
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(clip);
            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(guid))
                throw new System.InvalidOperationException("The assigned clip is not an imported asset: " + clip.name);
            if (!assignedNames.Add(clip.name) || !assignedFrequencies.Add(frequency) || !assignedGuids.Add(guid))
                throw new System.InvalidOperationException("Duplicate assigned clip, name, or nominal frequency: " + clip.name);
            if (clip.samples <= 0 || clip.channels <= 0 || clip.frequency <= 0)
                throw new System.InvalidOperationException("The assigned clip has invalid audio dimensions: " + clip.name);
        }
        outputDirectory = System.IO.Path.GetFullPath(outputDirectory);
        var existingManifestPath = System.IO.Path.Combine(outputDirectory, "manifest.json");
        if (System.IO.File.Exists(existingManifestPath))
        {
            var existingManifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(existingManifestPath));
            if ((int?)existingManifest["schemaVersion"] != 1 || (bool?)existingManifest["complete"] != true
                || (string)existingManifest["decoder"] != "UnityEngine.AudioClip.GetData from this Editor's currently imported assets"
                || !(existingManifest["clips"] is Newtonsoft.Json.Linq.JArray))
                throw new System.InvalidOperationException("This folder contains a different manifest.json. Choose a dedicated export folder.");
        }
        
        var initialSceneStates = Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
            .Select(index => UnityEngine.SceneManagement.SceneManager.GetSceneAt(index))
            .Select(scene => new { handle = scene.handle, path = scene.path, dirty = scene.isDirty }).ToArray();
        var activeSceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        var managerBefore = UnityEditor.EditorJsonUtility.ToJson(managerInstance);
        var initialDirtyStates = assignedClips.Distinct().ToDictionary(clip => clip, clip => UnityEditor.EditorUtility.IsDirty(clip));
        
        // Never block Unity's main thread waiting for asynchronous loading.
        // If a clip remains loading, allow an Editor update and rerun this script.
        var loadRequests = new System.Collections.Generic.List<string>();
        foreach (var clip in assignedClips.Distinct())
        {
            if (clip.loadState == UnityEngine.AudioDataLoadState.Failed)
                throw new System.InvalidOperationException("Audio data failed to load: " + clip.name);
            if (clip.loadState == UnityEngine.AudioDataLoadState.Unloaded)
            {
                if (!clip.LoadAudioData())
                    throw new System.InvalidOperationException("LoadAudioData failed; import settings unchanged: " + clip.name);
                loadRequests.Add(clip.name);
            }
        }
        var pendingClips = assignedClips.Distinct().Where(clip => clip.loadState != UnityEngine.AudioDataLoadState.Loaded)
            .Select(clip => new { name = clip.name, state = clip.loadState.ToString() }).ToArray();
        if (pendingClips.Length != 0)
            throw new System.InvalidOperationException("Audio data is still loading: " + string.Join(", ", pendingClips.Select(clip => clip.name + " (" + clip.state + ")")) + ". Let Unity process an Editor update, then choose this menu command again. No export files were written.");
        
        var libraryVersion = UnityEngine.Application.unityVersion;
        var decoderAssemblyVersion = typeof(UnityEngine.AudioClip).Assembly.GetName().Version.ToString();
        var packageManifestPath = System.IO.Path.Combine(projectPath, "Packages", "manifest.json");
        var packageLockPath = System.IO.Path.Combine(projectPath, "Packages", "packages-lock.json");
        var projectVersionPath = System.IO.Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt");
        var records = new System.Collections.Generic.List<object>();
        var provenanceChecks = new System.Collections.Generic.List<System.Action>();
        var exportedGuids = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        var temporaryFiles = new System.Collections.Generic.List<string>();
        var runId = System.Guid.NewGuid().ToString("N");
        System.IO.Directory.CreateDirectory(outputDirectory);
        
        try
        {
            for (var assignedIndex = 0; assignedIndex < assignedClips.Length; assignedIndex++)
            {
                var clip = assignedClips[assignedIndex];
                var assetPath = UnityEditor.AssetDatabase.GetAssetPath(clip);
                var assetGuid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(assetGuid))
                    throw new System.InvalidOperationException("Assigned clip is not a persistent asset: " + clip.name);
                if (!exportedGuids.Add(assetGuid)) continue;
                var sourcePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectPath, assetPath));
                var metaPath = sourcePath + ".meta";
                if (!System.IO.File.Exists(sourcePath) || !System.IO.File.Exists(metaPath))
                    throw new System.InvalidOperationException("Missing source or metadata: " + assetPath);
                var rawSourceSha256 = hashFile(sourcePath);
                var metaSha256 = hashFile(metaPath);
                var dependencyHash = UnityEditor.AssetDatabase.GetAssetDependencyHash(assetPath).ToString();
                var importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.AudioImporter;
                if (importer == null) throw new System.InvalidOperationException("Missing AudioImporter: " + assetPath);
                var importerBefore = UnityEditor.EditorJsonUtility.ToJson(importer);
                var importerWasDirty = UnityEditor.EditorUtility.IsDirty(importer);
                var importSettings = importer.defaultSampleSettings;
        
                var frames = clip.samples;
                var channels = clip.channels;
                var sampleRate = clip.frequency;
                if (frames <= 0 || channels <= 0 || sampleRate <= 0)
                    throw new System.InvalidOperationException("Invalid decoded dimensions: " + assetPath);
                var sampleCount = checked(frames * channels);
                var samples = new float[sampleCount];
                if (!clip.GetData(samples, 0))
                    throw new System.InvalidOperationException("GetData failed: " + assetPath + ". No import settings were changed.");
                double sumSquares = 0;
                var peak = 0f;
                long nonzeroSamples = 0;
                foreach (var sample in samples)
                {
                    if (float.IsNaN(sample) || float.IsInfinity(sample))
                        throw new System.InvalidOperationException("Non-finite decoded sample: " + assetPath);
                    var absolute = System.Math.Abs(sample);
                    if (absolute > peak) peak = absolute;
                    if (sample != 0f) nonzeroSamples++;
                    sumSquares += (double)sample * sample;
                }
                if (nonzeroSamples == 0)
                    throw new System.InvalidOperationException("All-zero decoded piano clip rejected: " + assetPath);
        
                var pcmBytes = new byte[checked(sampleCount * sizeof(float))];
                System.Buffer.BlockCopy(samples, 0, pcmBytes, 0, pcmBytes.Length);
                if (!System.BitConverter.IsLittleEndian)
                    for (var byteOffset = 0; byteOffset < pcmBytes.Length; byteOffset += sizeof(float))
                        System.Array.Reverse(pcmBytes, byteOffset, sizeof(float));
                var decodedPcmSha256 = hashBytes(pcmBytes);
                // Content-addressed names keep any previous complete manifest valid.
                var pcmFile = assetGuid + "-" + decodedPcmSha256 + ".f32le";
                var pcmPath = System.IO.Path.Combine(outputDirectory, pcmFile);
                if (System.IO.File.Exists(pcmPath))
                {
                    if (hashFile(pcmPath) != decodedPcmSha256)
                        throw new System.InvalidOperationException("Existing PCM content hash mismatch: " + pcmPath);
                }
                else
                {
                    var temporaryPath = System.IO.Path.Combine(outputDirectory, "__export_" + runId + "_" + assignedIndex + ".partial");
                    temporaryFiles.Add(temporaryPath);
                    using (var stream = new System.IO.FileStream(temporaryPath, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None))
                    {
                        stream.Write(pcmBytes, 0, pcmBytes.Length);
                        stream.Flush(true);
                    }
                    if (hashFile(temporaryPath) != decodedPcmSha256)
                        throw new System.IO.IOException("PCM write verification failed: " + assetPath);
                    System.IO.File.Move(temporaryPath, pcmPath);
                }
        
                records.Add(new
                {
                    assetGuid, assetPath, name = clip.name, channels, frames, sampleRate,
                    assignedIndices = Enumerable.Range(0, assignedClips.Length).Where(index => assignedClips[index] == clip).ToArray(),
                    rawSourceSha256, metaSha256, decodedPcmSha256, dependencyHash, libraryVersion,
                    pcmFile, pcmPath, pcmByteLength = pcmBytes.LongLength,
                    sampleFormat = "ieee754-float32-little-endian", channelLayout = "interleaved-original-order",
                    durationSeconds = (double)frames / sampleRate,
                    peakAbsolute = peak, rmsAllChannels = System.Math.Sqrt(sumSquares / sampleCount), nonzeroSamples,
                    clipLoadType = clip.loadType.ToString(), loadState = clip.loadState.ToString(),
                    importSettings = new
                    {
                        loadType = importSettings.loadType.ToString(), compressionFormat = importSettings.compressionFormat.ToString(),
                        quality = importSettings.quality, sampleRateSetting = importSettings.sampleRateSetting.ToString(),
                        sampleRateOverride = importSettings.sampleRateOverride, forceToMono = importer.forceToMono,
                        loadInBackground = importer.loadInBackground, ambisonic = importer.ambisonic
                    }
                });
                provenanceChecks.Add(() =>
                {
                    if (hashFile(sourcePath) != rawSourceSha256 || hashFile(metaPath) != metaSha256
                        || UnityEditor.AssetDatabase.GetAssetDependencyHash(assetPath).ToString() != dependencyHash)
                        throw new System.InvalidOperationException("Asset changed during export: " + assetPath);
                    if (UnityEditor.EditorJsonUtility.ToJson(importer) != importerBefore
                        || UnityEditor.EditorUtility.IsDirty(importer) != importerWasDirty)
                        throw new System.InvalidOperationException("Importer state changed during export: " + assetPath);
                });
            }
        
            foreach (var check in provenanceChecks) check();
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.Application.isPlaying)
                throw new System.InvalidOperationException("Play state changed; manifest not committed.");
            if (UnityEditor.EditorJsonUtility.ToJson(managerInstance) != managerBefore)
                throw new System.InvalidOperationException("frequencyManager changed; manifest not committed.");
            foreach (var pair in initialDirtyStates)
                if (UnityEditor.EditorUtility.IsDirty(pair.Key) != pair.Value)
                    throw new System.InvalidOperationException("Clip dirty state changed: " + pair.Key.name);
            var finalSceneStates = Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Select(index => UnityEngine.SceneManagement.SceneManager.GetSceneAt(index))
                .Select(scene => new { handle = scene.handle, path = scene.path, dirty = scene.isDirty }).ToArray();
            if (!initialSceneStates.SequenceEqual(finalSceneStates)
                || UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle != activeSceneHandle)
                throw new System.InvalidOperationException("Scene state changed; manifest not committed.");
        
            var manifest = new
            {
                schemaVersion = 1, complete = true, runId,
                createdUtc = System.DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                projectPath, sourceScene = managerInstance.gameObject.scene.path,
                managerGlobalObjectId = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(managerInstance).ToString(),
                assignedClipCount = assignedClips.Length, uniqueClipCount = records.Count,
                libraryVersion, unityVersion = UnityEngine.Application.unityVersion, decoderAssemblyVersion,
                activeBuildTarget = UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString(),
                packagesManifestSha256 = hashOptionalFile(packageManifestPath), packagesLockSha256 = hashOptionalFile(packageLockPath),
                projectVersionSha256 = hashOptionalFile(projectVersionPath),
                decoder = "UnityEngine.AudioClip.GetData from this Editor's currently imported assets",
                decoderScope = "Editor import/build target only; another platform, importer, Unity version or source needs a fresh export.",
                pcmFormat = "raw interleaved IEEE-754 float32, little-endian, no header, no gain or channel reduction",
                playbackPerformed = false, sceneOrImporterSettingsChanged = false, loadRequests, clips = records
            };
            var manifestPath = System.IO.Path.Combine(outputDirectory, "manifest.json");
            var manifestTemporaryPath = System.IO.Path.Combine(outputDirectory, "__manifest_" + runId + ".partial");
            temporaryFiles.Add(manifestTemporaryPath);
            var manifestJson = Newtonsoft.Json.JsonConvert.SerializeObject(manifest, Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText(manifestTemporaryPath, manifestJson + "\n", new System.Text.UTF8Encoding(false));
            if (System.IO.File.Exists(manifestPath)) System.IO.File.Replace(manifestTemporaryPath, manifestPath, null);
            else System.IO.File.Move(manifestTemporaryPath, manifestPath);
            return manifestPath;
        }
        finally
        {
            // Only transient paths created by this invocation in the selected output folder.
            foreach (var temporaryPath in temporaryFiles)
                if (System.IO.File.Exists(temporaryPath)) System.IO.File.Delete(temporaryPath);
        }
        
    }
}
