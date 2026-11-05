using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Linq;
public class terminalKeyboardNavigation : MonoBehaviour
{
    public bool blockWithAccount;
    private void Update()
    {
        if(blockWithAccount && accountUi.isBlocking)return;
        if(Keyboard.current==null || !Keyboard.current.tabKey.wasPressedThisFrame || EventSystem.current==null)return;
        var settings=UnityEngine.Object.FindFirstObjectByType<settingsUi>();
        if(settings!=null && settings.settingPanel.activeInHierarchy && transform!=settings.settingPanel.transform && !accountUi.isBlocking)return;
        var choices=GetComponentsInChildren<UnityEngine.UI.Selectable>().Where(s=>s.IsInteractable() && s.gameObject.activeInHierarchy).ToArray();
        if(choices.Length==0)return;
        int current=System.Array.FindIndex(choices,s=>s.gameObject==EventSystem.current.currentSelectedGameObject);
        bool back=Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        int next=(current+(back?-1:1)+choices.Length)%choices.Length;
        EventSystem.current.SetSelectedGameObject(choices[next].gameObject);
        var input=choices[next] as TMPro.TMP_InputField;if(input!=null)input.ActivateInputField();
    }
}

