using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
public class accountTerminal : MonoBehaviour
{
    private accountUi ui;
    private terminalPanel panel;
    private TMP_Text greeting,privacy,simulationLabel;
    private TMP_Text[] values;
    private UnityEngine.UI.Button training;
    private terminalLayoutSelector layouts;
    private bool confirming,visitedTraining,built;
    private void Start()
    {
        ui=GetComponent<accountUi>();build();cloudAccount.instance.changed+=refresh;refresh();
    }
    private void build()
    {
        if(built)return;built=true;
        signalTerminal.canvas(GetComponent<Canvas>());
        if(ui.panel.GetComponent<terminalKeyboardNavigation>()==null)ui.panel.AddComponent<terminalKeyboardNavigation>();
        var oldCard=ui.panel.transform.Find("card");oldCard.gameObject.SetActive(false);
        panel=terminalPanel.create(ui.panel.transform,"pilotConsole");panel.preferredWidth=620;panel.top=65;panel.metadata.text="COMET CATCHER / ACCOUNT";panel.title.text="Pilot account";
        var oldLogin=ui.loginPanel;var oldDashboard=ui.dashboardPanel;
        var login=signalTerminal.node(panel.content,"signIn");signalTerminal.column(login,8);ui.loginPanel=login.gameObject;
        
        label(login,"Email address");moveInput(ui.emailInput,login);
        label(login,"Password");moveInput(ui.passwordInput,login);
        var auth=signalTerminal.node(login,"authenticationActions");signalTerminal.height(auth,40);signalTerminal.row(auth,10);
        moveButton(ui.loginButton,auth,signalTerminal.actionStyle.primary,160);moveButton(ui.signupButton,auth,signalTerminal.actionStyle.secondary,180);
        var verify=signalTerminal.node(login,"verification");signalTerminal.column(verify,8);
        var help=signalTerminal.text(verify,"help","Confirm using your email link, then log in. Enter a code below if your email includes one.",16,40);help.color=signalTerminal.secondary;
        label(verify,"Verification code");moveInput(ui.codeInput,verify);
        var verifyActions=signalTerminal.node(verify,"verificationActions");signalTerminal.height(verifyActions,40);signalTerminal.row(verifyActions,10);
        moveButton(ui.verifyButton,verifyActions,signalTerminal.actionStyle.secondary,160);moveButton(ui.resendButton,verifyActions,signalTerminal.actionStyle.quiet,160);
        var disclosureRow=signalTerminal.node(login,"verificationDisclosure");signalTerminal.height(disclosureRow,40);signalTerminal.row(disclosureRow);
        var disclosure=signalTerminal.button(disclosureRow,"showVerification","Email confirmation",()=>{verify.gameObject.SetActive(!verify.gameObject.activeSelf);refresh();},signalTerminal.actionStyle.quiet,210);
        disclosureRow.SetSiblingIndex(5);verify.gameObject.SetActive(false);
        var dashboard=signalTerminal.node(panel.content,"pilotProgress");signalTerminal.column(dashboard,12);ui.dashboardPanel=dashboard.gameObject;
        greeting=signalTerminal.text(dashboard,"greeting","",18,28);greeting.overflowMode=TextOverflowModes.Ellipsis;greeting.textWrappingMode=TextWrappingModes.NoWrap;
        var stats=signalTerminal.node(dashboard,"statistics");signalTerminal.height(stats,100);values=new TMP_Text[4];
        string[] names={"BEST SCORE","HIGHEST LEVEL","RUNS STARTED","RUNS COMPLETED"};
        for(int i=0;i<4;i++)
        {
            var cell=signalTerminal.node(stats,"stat"+i);int x=i%2,y=i/2;cell.anchorMin=new Vector2(x*.5f,1-(y+1)*.5f);cell.anchorMax=new Vector2((x+1)*.5f,1-y*.5f);cell.offsetMin=Vector2.zero;cell.offsetMax=new Vector2(-12,0);
            var title=signalTerminal.text(cell,"label",names[i],11,16,true);title.color=signalTerminal.secondary;terminalPanel.place(title.rectTransform,0,0,0,16);
            values[i]=signalTerminal.text(cell,"value","",23,30);terminalPanel.place(values[i].rectTransform,0,17,0,30);
        }
        var rule=signalTerminal.node(dashboard,"controlsRule");signalTerminal.height(rule,1);signalTerminal.frame(rule,signalTerminal.rule,true);
        var controlsLabel=signalTerminal.text(dashboard,"controlsLabel","CONTROL LAYOUT",11,16,true);controlsLabel.color=signalTerminal.secondary;
        layouts=terminalLayoutSelector.create(dashboard);
        var tools=signalTerminal.node(dashboard,"accountTools");signalTerminal.height(tools,40);signalTerminal.row(tools,8);
        moveButton(ui.refreshButton,tools,signalTerminal.actionStyle.quiet,130);ui.refreshButton.GetComponentInChildren<TMP_Text>().text="Refresh";
        training=signalTerminal.button(tools,"training","Replay tutorial",()=>{if(gameManager.instance!=null && !gameManager.instance.isPractice && gameManager.instance.isGameActive && !confirming){confirming=true;refresh();return;}cloudAccount.instance.requestTutorial();confirming=false;},signalTerminal.actionStyle.secondary,180);
        moveButton(ui.logoutButton,tools,signalTerminal.actionStyle.quiet,190);
        privacy=signalTerminal.text(panel.content,"accountPrivacy","Progress and scores are saved to your account.\nResearch data collection is disabled.",15,40);privacy.color=signalTerminal.secondary;
        ui.statusText.transform.SetParent(panel.content,false);signalTerminal.type(ui.statusText,false,16);ui.statusText.color=signalTerminal.accent;
        ui.saveText.transform.SetParent(panel.content,false);signalTerminal.type(ui.saveText,false,14);ui.saveText.color=signalTerminal.secondary;
        moveButton(ui.closeButton,panel.actions,signalTerminal.actionStyle.secondary,190);
        moveButton(ui.playButton,panel.actions,signalTerminal.actionStyle.primary,180);
        moveButton(ui.retryButton,panel.actions,signalTerminal.actionStyle.primary,170);
        var bar=(RectTransform)ui.compactBar.transform;bar.anchorMin=bar.anchorMax=new Vector2(1,1);bar.pivot=new Vector2(1,1);bar.sizeDelta=new Vector2(215,40);bar.anchoredPosition=new Vector2(-18,-65);signalTerminal.image(bar,Color.clear);
        ui.accountButton.transform.SetParent(bar,false);signalTerminal.fill((RectTransform)ui.accountButton.transform);signalTerminal.styleButton(ui.accountButton,signalTerminal.actionStyle.secondary);
        simulationLabel=signalTerminal.text(bar,"simulationLabel","Training simulation",11,18,true);simulationLabel.color=signalTerminal.secondary;simulationLabel.alignment=TextAlignmentOptions.Right;terminalPanel.place(simulationLabel.rectTransform,0,44,0,18);
        ui.panel.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        oldLogin.SetActive(false);oldDashboard.SetActive(false);
    }
    private void label(Transform parent,string text){var t=signalTerminal.text(parent,"fieldLabel",text,15,20);t.color=signalTerminal.secondary;}
    private void moveInput(TMP_InputField input,Transform parent)
    {
        input.transform.SetParent(parent,false);signalTerminal.height((RectTransform)input.transform,40);
        var image=signalTerminal.image((RectTransform)input.transform,new Color32(13,31,46,255),true);input.targetGraphic=image;
        signalTerminal.type(input.textComponent,false,17);if(input.placeholder is TMP_Text placeholder){signalTerminal.type(placeholder,false,16);placeholder.color=signalTerminal.secondary;}
        var colors=input.colors;colors.selectedColor=new Color32(169,221,241,255);colors.highlightedColor=Color.white;input.colors=colors;
        if(input.GetComponent<terminalFocus>()==null)input.gameObject.AddComponent<terminalFocus>();
        signalTerminal.frame((RectTransform)input.transform,signalTerminal.rule).box=true;
    }
    private void moveButton(UnityEngine.UI.Button button,Transform parent,signalTerminal.actionStyle style,float width)
    {button.transform.SetParent(parent,false);signalTerminal.height((RectTransform)button.transform,40);signalTerminal.width((RectTransform)button.transform,width);signalTerminal.styleButton(button,style);}
    private void Update()
    {
        if(!built)return;
        if(!ui.panel.activeSelf)confirming=false;
        refresh();
    }
    private void refresh()
    {
        if(!built || cloudAccount.instance==null)return;var account=cloudAccount.instance;
        bool practice=SceneManager.GetActiveScene().name==cloudAccount.tutorialScene;
        simulationLabel.gameObject.SetActive(practice);
        if(!account.signedIn)visitedTraining=false;else if(practice)visitedTraining=true;
        ui.panel.GetComponent<UnityEngine.UI.Image>().color=(SceneManager.GetActiveScene().name==cloudAccount.playScene || practice)?Color.clear:signalTerminal.background;
        panel.title.text=account.signedIn?"Pilot account":"Welcome aboard";
        ui.loginPanel.SetActive(!account.signedIn);ui.dashboardPanel.SetActive(account.signedIn);
        string nickname=account.profile.nickname??"";greeting.text=nickname.Length>24?"Welcome back":("Welcome, "+nickname);
        var p=account.progress;values[0].text=p.bestScore.ToString("N0");values[1].text=p.sessionsPlayed==0?"—":(p.highestLevel+1).ToString();values[2].text=p.sessionsPlayed.ToString();values[3].text=p.sessionsCompleted.ToString();
        ui.playButton.GetComponentInChildren<TMP_Text>().text=practice?"Exit training":account.tutorialComplete?"Play":visitedTraining?"Restart training":"Start training";
        ui.accountButton.GetComponentInChildren<TMP_Text>().text=practice?"Account / exit":account.hasPendingSave?"Account / save pending":"Account";
        training.GetComponentInChildren<TMP_Text>().text=confirming?"Save run & train":"Replay tutorial";
        training.interactable=!account.busy && !account.hasPendingSave;layouts.interactable(!account.busy);
        // First-time training already has a primary entry action; avoid a second competing one.
        training.gameObject.SetActive(account.tutorialComplete);
        ui.saveText.text=practice?"Training simulation":account.saveMessage;
        ui.saveText.gameObject.SetActive(account.signedIn && !string.IsNullOrEmpty(ui.saveText.text));
        ui.statusText.gameObject.SetActive(!string.IsNullOrEmpty(ui.statusText.text));
        var verify=ui.loginPanel.transform.Find("verification") as RectTransform;
        if(verify!=null)fitColumn(verify);
        fitColumn((RectTransform)ui.loginPanel.transform);fitColumn((RectTransform)ui.dashboardPanel.transform);
        panel.reflow();
    }
    private void fitColumn(RectTransform r)
    {UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(r);signalTerminal.height(r,r.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().preferredHeight);}
    private void OnDestroy(){if(cloudAccount.instance!=null)cloudAccount.instance.changed-=refresh;}
}
