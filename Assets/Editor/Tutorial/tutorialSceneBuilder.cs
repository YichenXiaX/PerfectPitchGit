using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;
public static class tutorialSceneBuilder
{
    [MenuItem("Tools/Tutorial/Create Flight Training Scene")]
    public static void build()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play first.");
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Additive);
        try
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            new GameObject("flightTraining",typeof(tutorialDirector));
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/flightTraining.unity",true);
        }
        finally {EditorSceneManager.CloseScene(scene,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);}
        var scenes=EditorBuildSettings.scenes.Where(s=>s.path!="Assets/Scenes/flightTraining.unity").ToList();
        scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/flightTraining.unity",true));EditorBuildSettings.scenes=scenes.ToArray();
    }
}
