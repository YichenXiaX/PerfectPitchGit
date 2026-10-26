using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Temporary UI state only. No names or contact details are persisted or logged.
public class researchConsentUi : MonoBehaviour
{
    [System.Serializable]
    public class form
    {
        public GameObject panel;
        public UnityEngine.UI.ScrollRect scroll;
        public UnityEngine.UI.Toggle agreement;
        public UnityEngine.UI.Toggle followUpYes;
        public UnityEngine.UI.Toggle followUpNo;
        public UnityEngine.UI.Button continueButton;
    }
    public GameObject openingPanel;
    public form child;
    public form parent;
    public form adult;
    public string gameScene = "SampleScene";
    private bool transitioning;

    private void Awake()
    {
        researchConsentState.reset();
        Time.timeScale = 1f;
        foreach (var item in new[] { child, parent, adult })
        {
            item.agreement.SetIsOnWithoutNotify(false);
            item.followUpYes.SetIsOnWithoutNotify(false);
            item.followUpNo.SetIsOnWithoutNotify(false);
            item.continueButton.interactable = false;
            var captured = item;
            item.agreement.onValueChanged.AddListener(value => captured.continueButton.interactable = value);
        }
        showOpening();
    }
    private void show(GameObject panel)
    {
        openingPanel.SetActive(panel == openingPanel);
        foreach (var item in new[] { child, parent, adult })
        {
            item.panel.SetActive(panel == item.panel);
            if (panel == item.panel)
            {
                Canvas.ForceUpdateCanvases();
                item.scroll.verticalNormalizedPosition = 1f;
            }
        }
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
    }
    public void showOpening() { show(openingPanel); }
    public void showChild() { show(child.panel); }
    public void showAdult() { show(adult.panel); }
    public void continueChild() { if (child.agreement.isOn) show(parent.panel); }
    public void continueParent()
    {
        if (child.agreement.isOn && parent.agreement.isOn) finish("childAndParentAgreed");
    }
    public void continueAdult() { if (adult.agreement.isOn) finish("adultAgreed"); }
    public void decline() { finish("declined"); }
    private void finish(string status)
    {
        if (transitioning) return;
        transitioning = true;
        researchConsentState.status = status;
        foreach (var field in GetComponentsInChildren<TMP_InputField>(true))
            field.SetTextWithoutNotify(string.Empty);
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameScene);
    }
}
