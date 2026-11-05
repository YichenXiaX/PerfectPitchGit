using UnityEngine;
using TMPro;
public class controlLegend : MonoBehaviour
{
    private settingsUi settings;
    private TMP_Text label;
    private void Start()
    {
        settings=GetComponent<settingsUi>();
        var group=settings.leftHandedToggle.gameObject.GetComponent<CanvasGroup>();if(group==null)group=settings.leftHandedToggle.gameObject.AddComponent<CanvasGroup>();group.ignoreParentGroups=true;group.interactable=true;group.blocksRaycasts=true;group.alpha=1;
        label=settings.leftHandedToggle.GetComponentInChildren<TMP_Text>();
        if(label!=null){label.fontSize=18;label.enableAutoSizing=false;}
        controlLayout.changed+=refresh;refresh();
    }
    private void OnEnable(){if(label!=null)refresh();}
    private void refresh()
    {
        settings.leftHandedToggle.SetIsOnWithoutNotify(controlLayout.leftHanded);
        if(label!=null)label.text="Left-hand layout (off = right hand)\n"+controlLayout.legend(controlLayout.leftHanded);
    }
    private void OnDestroy(){controlLayout.changed-=refresh;}
}


