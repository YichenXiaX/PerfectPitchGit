using UnityEngine;
using TMPro;

// Keeps the text's disabled contrast in sync with the existing Button state.
[RequireComponent(typeof(UnityEngine.UI.Button))]
public class consentButtonLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color enabledColor = new Color(.95f,.95f,.95f);
    [SerializeField] private Color disabledColor = new Color(.58f,.61f,.63f);
    private UnityEngine.UI.Button button;
    private bool previous;

    private void OnEnable()
    {
        button = GetComponent<UnityEngine.UI.Button>();
        if(label == null) label = GetComponentInChildren<TMP_Text>();
        refresh();
    }
    private void LateUpdate()
    {
        if(previous != button.IsInteractable()) refresh();
    }
    private void refresh()
    {
        previous = button.IsInteractable();
        if(label != null) label.color = previous ? enabledColor : disabledColor;
    }
}
