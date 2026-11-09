using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

public static class consentIntegrationSetup
{
    public static string apply()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play first.");
        var loaded=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/researchConsent.unity");
        if(loaded.IsValid() && loaded.isLoaded)throw new System.InvalidOperationException("Save and close the consent scene before applying setup; open scenes are left untouched.");
        var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/researchConsent.unity",OpenSceneMode.Additive);
        try {
            var ui=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<researchConsentUi>(true)).Single();
            bind(ui.adult,1);bind(ui.child,1);bind(ui.parent,2);
            foreach(var field in ui.GetComponentsInChildren<TMP_InputField>(true))field.characterLimit=254;
            var status=ui.transform.Find("consentSaveStatus") as RectTransform;
            if(status==null) {
                status=signalTerminal.node(ui.transform,"consentSaveStatus");
                status.gameObject.AddComponent<TextMeshProUGUI>();
            }
            ui.statusText=status.GetComponent<TMP_Text>();signalTerminal.type(ui.statusText,false,15);
            ui.statusText.color=signalTerminal.accent;ui.statusText.alignment=TextAlignmentOptions.Center;
            ui.statusText.text="";ui.statusText.raycastTarget=false;
            status.anchorMin=new Vector2(.1f,0);status.anchorMax=new Vector2(.9f,0);status.pivot=new Vector2(.5f,0);
            status.offsetMin=new Vector2(0,4);status.offsetMax=new Vector2(0,44);
            var footer=ui.transform.Find("terminalMetadata");
            if(footer!=null)footer.gameObject.SetActive(false);
            EditorUtility.SetDirty(ui);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        } finally {EditorSceneManager.CloseScene(scene,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);}
        var scenes=EditorBuildSettings.scenes.ToList();
        var consent=scenes.Find(s=>s.path=="Assets/Scenes/researchConsent.unity");
        if(consent==null)scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/researchConsent.unity",true));else consent.enabled=true;
        EditorBuildSettings.scenes=scenes.ToArray();
        return "Bound existing form fields and enabled consent; gameplay scenes unchanged.";
    }
    private static void bind(researchConsentUi.form form,int names)
    {
        var fields=form.scroll.content.GetComponentsInChildren<TMP_InputField>(true);
        if(fields.Length!=names+1)throw new System.InvalidOperationException("Unexpected form fields in "+form.panel.name);
        form.nameFields=fields.Take(names).ToArray();form.contactField=fields.Last();
    }
}
