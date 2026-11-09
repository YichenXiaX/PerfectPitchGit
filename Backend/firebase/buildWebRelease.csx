// Run through Unity CLI's script eval in the existing Editor, with Play mode stopped.
if (UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isCompiling ||
    UnityEditor.EditorApplication.isUpdating || UnityEditor.BuildPipeline.isBuildingPlayer)
    throw new System.InvalidOperationException("Wait for an idle Editor with Play mode stopped.");
for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
        throw new System.InvalidOperationException("Save or discard unsaved scene changes yourself before building.");
var scenes = UnityEditor.EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
if (scenes.Length != 4 || !scenes[0].EndsWith("/account.unity") || scenes.Any(p => p.Contains("tester")))
    throw new System.InvalidOperationException("Unexpected release scenes; review Build Settings.");
var target = UnityEditor.Build.NamedBuildTarget.WebGL;
if (!string.IsNullOrWhiteSpace(UnityEditor.PlayerSettings.GetScriptingDefineSymbols(target)))
    throw new System.InvalidOperationException("Review custom scripting defines before a public build.");
var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Backend/firebase"));
var reportPath = System.IO.Path.Combine(root, ".build-report.json");
System.IO.File.WriteAllText(reportPath, "{\"result\":\"Queued\"}");
UnityEditor.EditorApplication.delayCall += () => {
    try {
        // Firebase negotiates transport compression itself; do not upload an
        // already-compressed body whose Content-Encoding the CDN overrides.
        UnityEditor.PlayerSettings.WebGL.compressionFormat = UnityEditor.WebGLCompressionFormat.Disabled;
        UnityEditor.PlayerSettings.WebGL.decompressionFallback = false;
        UnityEditor.PlayerSettings.WebGL.nameFilesAsHashes = true;
        UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.RuntimeSpeed;
        UnityEditor.AssetDatabase.SaveAssets();
        System.IO.File.WriteAllText(reportPath, "{\"result\":\"Running\"}");
        var report = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions {
            scenes = scenes, locationPathName = System.IO.Path.Combine(root, "public"),
            target = UnityEditor.BuildTarget.WebGL, options = UnityEditor.BuildOptions.None,
            extraScriptingDefines = System.Array.Empty<string>()
        });
        System.IO.File.WriteAllText(reportPath, Newtonsoft.Json.JsonConvert.SerializeObject(new {
            result = report.summary.result.ToString(), errors = report.summary.totalErrors,
            warnings = report.summary.totalWarnings, bytes = report.summary.totalSize,
            seconds = report.summary.totalTime.TotalSeconds, finishedUtc = System.DateTime.UtcNow,
            development = (report.summary.options & UnityEditor.BuildOptions.Development) != 0,
            options = report.summary.options.ToString(), compression = "Firebase CDN", hashedNames = true, scenes,
            messages = report.steps.SelectMany(s => s.messages)
                .Where(m => m.type == UnityEngine.LogType.Error || m.type == UnityEngine.LogType.Warning)
                .Select(m => m.content).ToArray()
        }, Newtonsoft.Json.Formatting.Indented));
    } catch (System.Exception error) {
        System.IO.File.WriteAllText(reportPath, Newtonsoft.Json.JsonConvert.SerializeObject(new {
            result = "Exception", message = error.Message
        }));
    }
};
return new { queued = true, reportPath };
