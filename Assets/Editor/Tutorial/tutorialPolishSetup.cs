using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

public static class tutorialPolishSetup
{
    public static void apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first.");
        var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(current.isDirty)throw new System.InvalidOperationException("Preserve the unsaved scene before setup.");
        string original=current.path;
        terminalFontBuilder.build();
        foreach(string path in new[]{"Assets/Scenes/SampleScene.unity","Assets/Scenes/flightTraining.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);
            var ui=Object.FindFirstObjectByType<settingsUi>(FindObjectsInactive.Include);
            var row=(RectTransform)ui.octave0.transform.parent;
            foreach(string name in new[]{"Toggle -5","Toggle -4","Toggle -3","Toggle +3","Toggle +4","Toggle +5"})
            {var child=row.Find(name);if(child!=null)Object.DestroyImmediate(child.gameObject);}
            var grid=row.GetComponent<UnityEngine.UI.GridLayoutGroup>();if(grid!=null)Object.DestroyImmediate(grid);
            signalTerminal.row(row,8,true);signalTerminal.height(row,44);
            foreach(var toggle in new[]{ui.octaveNeg2,ui.octaveNeg1,ui.octave0,ui.octavePos1,ui.octavePos2})
            {signalTerminal.width((RectTransform)toggle.transform,78);signalTerminal.height((RectTransform)toggle.transform,40);}
            var settings=Object.FindFirstObjectByType<gameSettings>();
            settings.customPreset=gameSettings.normalizePreset(settings.customPreset);
            for(int i=0;i<settings.levelPresets.Length;i++)settings.levelPresets[i]=gameSettings.normalizePreset(settings.levelPresets[i]);
            EditorUtility.SetDirty(ui);EditorUtility.SetDirty(settings);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        EditorSceneManager.OpenScene(original);
    }
}
