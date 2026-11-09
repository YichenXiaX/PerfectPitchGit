using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// The approved forms submit an account-owned receipt before onboarding continues.
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
        public TMP_InputField[] nameFields;
        public TMP_InputField contactField;
    }
    public GameObject openingPanel;
    public form child;
    public form parent;
    public form adult;
    public string gameScene = "SampleScene";
    public TMP_Text statusText;
    private bool transitioning;
    private cloudAccount account;
    private string pendingFingerprint, pendingRequestId;

    private void Awake()
    {
        Time.timeScale = 1f;
        foreach (var item in new[] { child, parent, adult })
        {
            item.agreement.SetIsOnWithoutNotify(false);
            item.followUpYes.SetIsOnWithoutNotify(false);
            item.followUpNo.SetIsOnWithoutNotify(false);
            item.continueButton.interactable = false;
            item.agreement.onValueChanged.AddListener(value => refresh());
        }
        showOpening();
    }
    private void Start()
    {
        account=cloudAccount.instance;
        if(account!=null) account.changed+=refresh;
        refresh();
    }
    private void OnDestroy() { if(account!=null)account.changed-=refresh; }
    private bool canInteract => account!=null && account.signedIn && !account.busy && !accountUi.isBlocking && !transitioning;
    private void refresh()
    {
        bool enabled=account!=null && account.signedIn && !account.busy && !transitioning;
        foreach(var control in GetComponentsInChildren<UnityEngine.UI.Selectable>(true))control.interactable=enabled;
        foreach(var item in new[]{child,parent,adult})item.continueButton.interactable=enabled && item.agreement.isOn;
        if(statusText!=null && account!=null)statusText.text=account.message;
    }
    private void show(GameObject panel)
    {
        if(transitioning || (account!=null && account.busy)) return;
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
    public void continueChild() { if (canInteract && child.agreement.isOn) show(parent.panel); }
    public void continueParent()
    {
        if (canInteract && child.agreement.isOn && parent.agreement.isOn) finish("childAndParentAgreed");
    }
    public void continueAdult() { if (canInteract && adult.agreement.isOn) finish("adultAgreed"); }
    public void decline() { if(canInteract) finish("declined"); }
    private static string choice(form value) => value.followUpYes.isOn ? "yes" : value.followUpNo.isOn ? "no" : "notSpecified";
    private static string printedName(form value,int index) => value.nameFields[index].text.Trim();
    private consentSubmission submission(string decision)
    {
        var value=new consentSubmission{decision=decision};
        // A decline never transmits partially typed names, contact information or agreements.
        if(decision=="adultAgreed") {
            value.adultAgreement=true;value.participantName=printedName(adult,0);value.participantFollowUp=choice(adult);
            if(value.participantFollowUp=="yes")value.participantContact=adult.contactField.text.Trim();
        } else if(decision=="childAndParentAgreed") {
            value.childAgreement=value.parentAgreement=true;
            value.childName=printedName(child,0);value.parentChildName=printedName(parent,0);value.guardianName=printedName(parent,1);
            value.participantFollowUp=choice(child);value.guardianFollowUp=choice(parent);
            if(value.participantFollowUp=="yes" && value.guardianFollowUp=="yes") {
                value.participantContact=child.contactField.text.Trim();value.guardianContact=parent.contactField.text.Trim();
            }
        }
        return value;
    }
    private void finish(string status)
    {
        if (!canInteract) return;
        consentSubmission value;
        try { value=submission(status); }
        catch(System.Exception) { account.setMessage("The form could not load. Return to Account and reopen Research choice."); return; }
        string fingerprint=JsonUtility.ToJson(value);
        if(fingerprint!=pendingFingerprint) { pendingFingerprint=fingerprint;pendingRequestId=System.Guid.NewGuid().ToString(); }
        value.requestId=pendingRequestId;
        transitioning = true;
        refresh();
        account.saveConsent(value,ok=>{
            if(this==null)return;
            transitioning=false;
            if(ok) {
                foreach(var field in GetComponentsInChildren<TMP_InputField>(true))field.SetTextWithoutNotify(string.Empty);
                pendingFingerprint=pendingRequestId=null;Time.timeScale=1f;account.enterPlay();
            } else refresh();
        });
    }
}
