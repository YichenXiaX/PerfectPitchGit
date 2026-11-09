using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Only the signed-out entry screens. Dashboard and consent presentation stay separate.
public class accountEntry : MonoBehaviour
{
    public enum entryState { login, createAccount, verifyEmail, forgotPassword, resetPassword }
    public entryState state { get; private set; }
    public string heading => state==entryState.createAccount?"Create account":state==entryState.verifyEmail?"Verify email":state==entryState.forgotPassword?"Reset your password":state==entryState.resetPassword?"Choose a new password":"Log in";
    public class field
    {
        public TMP_InputField input;
        public TMP_Text error;
        public UnityEngine.UI.Button visibility;
    }
    public field loginEmail,loginPassword,signupEmail,signupPassword,confirmPassword,confirmationLink,resetEmail,resetLink,newPassword,confirmNewPassword;
    public UnityEngine.UI.Button createButton,sendResetButton,resetButton,linkButton;
    public TMP_Text verificationHelp;
    private TMP_Text resetTarget;
    private readonly Dictionary<entryState,RectTransform> pages=new Dictionary<entryState,RectTransform>();
    private readonly Dictionary<entryState,TMP_Text> notices=new Dictionary<entryState,TMP_Text>();
    private readonly List<field> fields=new List<field>();
    private cloudAccount account;
    private accountUi ui;
    private terminalPanel terminal;
    private RectTransform root,linkSection;
    private UnityEngine.UI.Button useResetButton;
    private string pendingEmail="";
    private bool initialized,wasSignedIn;
    private int enterHandledFrame=-1;
    private static readonly Color errorColor=new Color32(255,182,174,255);
    public void initialize(accountUi owner,terminalPanel panel)
    {
        if(initialized)return;initialized=true;ui=owner;terminal=panel;account=cloudAccount.instance;
        root=signalTerminal.node(panel.content,"accountEntry");signalTerminal.column(root,0);ui.loginPanel=root.gameObject;
        var login=page(entryState.login);
        var greeting=note(login,"intro","Welcome back. Log in to continue your run.");
        greeting.font=signalTerminal.monoFont;
        loginEmail=makeField(login,"loginEmail","Email address",false,ui.emailInput);
        loginPassword=makeField(login,"loginPassword","Password",true,ui.passwordInput);
        notice(login,entryState.login);moveButton(ui.loginButton,login,"Log in",submitLogin,true);
        var links=row(login,"loginLinks");moveButton(ui.signupButton,links,"Create account",showCreate,false,170);
        signalTerminal.button(links,"forgotPassword","Forgot password?",showForgot,signalTerminal.actionStyle.quiet,180);
        bindSubmit(loginEmail,submitLogin);bindSubmit(loginPassword,submitLogin);

        var create=page(entryState.createAccount);
        note(create,"intro","Save your progress with your own account.");
        signupEmail=makeField(create,"signupEmail","Email address");
        note(create,"mailboxHelp","We'll ask you to confirm access to this mailbox.",15);
        note(create,"passwordRequirements",accountAuthPolicy.passwordRequirements,15);
        signupPassword=makeField(create,"signupPassword","Password",true);
        confirmPassword=makeField(create,"confirmPassword","Confirm password",true);
        notice(create,entryState.createAccount);createButton=button(create,"createAccount","Create account",submitSignup,true);
        button(create,"backToLogin","Already have an account? Log in",showLogin);
        bindSubmit(signupEmail,submitSignup);bindSubmit(signupPassword,submitSignup);bindSubmit(confirmPassword,submitSignup);

        var verify=page(entryState.verifyEmail);
        verificationHelp=note(verify,"verificationHelp","",17);
        note(verify,"verificationSteps","Open the confirmation link in your inbox or spam folder. Then return here and log in. If you already verified this address, you can log in now.",16);
        ui.codeInput.gameObject.SetActive(false); // The configured email contains a link, not a code.
        notice(verify,entryState.verifyEmail);
        moveButton(ui.verifyButton,verify,"Back to log in",showLogin,true);
        var resend=row(verify,"verificationActions");moveButton(ui.resendButton,resend,"Resend email",resendConfirmation,false,180);
        signalTerminal.button(resend,"correctEmail","Correct email address",correctEmail,signalTerminal.actionStyle.quiet,210);
        button(verify,"useLinkInstead","Link not opening? Use it here",()=>{linkSection.gameObject.SetActive(!linkSection.gameObject.activeSelf);positionNotice(entryState.verifyEmail,ui.verifyButton.transform.parent);reflow();if(linkSection.gameObject.activeSelf)focus(confirmationLink.input);});
        linkSection=signalTerminal.node(verify,"confirmationLinkSection");signalTerminal.column(linkSection,8);
        note(linkSection,"linkHelp","Copy the confirmation link from the email and paste it below. Use the latest email. If the link has expired, request a new one.",15);
        confirmationLink=makeField(linkSection,"confirmationLink","Confirmation link",true,null,false);
        linkButton=button(linkSection,"confirmLink","Verify email link",submitLink,true);bindSubmit(confirmationLink,submitLink);
        linkSection.gameObject.SetActive(false);

        var forgot=page(entryState.forgotPassword);
        note(forgot,"help","Enter your email to request a password-reset link.");
        resetEmail=makeField(forgot,"resetEmail","Email address");notice(forgot,entryState.forgotPassword);
        sendResetButton=button(forgot,"sendReset","Send reset link",sendReset,true);bindSubmit(resetEmail,sendReset);
        note(forgot,"resetHelp","When the email arrives, copy its reset link and use it here to choose a new password. This also works if the browser cannot open the game.",15);
        resetLink=makeField(forgot,"resetLink","Password-reset link",true,null,false);
        useResetButton=button(forgot,"useResetLink","Use reset link",submitResetLink);bindSubmit(resetLink,submitResetLink);
        button(forgot,"resetBack","Back to log in",showLogin);

        var reset=page(entryState.resetPassword);
        resetTarget=note(reset,"resetTarget","",16);
        note(reset,"requirements",accountAuthPolicy.passwordRequirements,15);
        newPassword=makeField(reset,"newPassword","New password",true);
        confirmNewPassword=makeField(reset,"confirmNewPassword","Confirm new password",true);
        notice(reset,entryState.resetPassword);resetButton=button(reset,"savePassword","Save new password",submitNewPassword,true);
        button(reset,"cancelReset","Back to log in",showLogin);bindSubmit(newPassword,submitNewPassword);bindSubmit(confirmNewPassword,submitNewPassword);
        account.authenticationFinished+=completed;
        switchTo(entryState.login);refresh();
    }
    private RectTransform page(entryState value)
    {var r=signalTerminal.node(root,value.ToString());signalTerminal.column(r,10);pages[value]=r;return r;}
    private RectTransform row(Transform parent,string name)
    {var r=signalTerminal.node(parent,name);signalTerminal.height(r,40);signalTerminal.row(r,8);return r;}
    private TMP_Text note(Transform parent,string name,string value,float size=16)
    {var t=signalTerminal.text(parent,name,value,size,24);t.color=signalTerminal.secondary;t.richText=false;return t;}
    private void notice(Transform parent,entryState value)
    {notices[value]=note(parent,"serverMessage","",16);notices[value].gameObject.SetActive(false);}
    private field makeField(Transform parent,string name,string label,bool password=false,TMP_InputField existing=null,bool allowReveal=true)
    {
        var block=signalTerminal.node(parent,name+"Field");signalTerminal.column(block,4);
        note(block,"label",label,15);
        var inputRow=row(block,"inputRow");
        var input=existing!=null?existing:Instantiate(password?ui.passwordInput:ui.emailInput);
        input.gameObject.name=name;input.transform.SetParent(inputRow,false);input.gameObject.SetActive(true);input.SetTextWithoutNotify("");
        input.onValueChanged=new TMP_InputField.OnChangeEvent();input.onSubmit=new TMP_InputField.SubmitEvent();
        input.contentType=password?TMP_InputField.ContentType.Password:TMP_InputField.ContentType.Standard;
        input.lineType=TMP_InputField.LineType.SingleLine;input.characterLimit=password?0:254;
        var r=(RectTransform)input.transform;signalTerminal.height(r,40);
        var layout=input.GetComponent<UnityEngine.UI.LayoutElement>();layout.minWidth=60;layout.preferredWidth=0;layout.flexibleWidth=1;
        input.targetGraphic=signalTerminal.image(r,new Color32(13,31,46,255),true);
        // Keep enough vertical room for the full glyphs inside the 40-pixel field.
        input.textViewport.offsetMin=new Vector2(12,4);input.textViewport.offsetMax=new Vector2(-12,-4);
        signalTerminal.type(input.textComponent,false,17);input.textComponent.richText=false;
        input.textComponent.alignment=TextAlignmentOptions.Left;input.textComponent.textWrappingMode=TextWrappingModes.NoWrap;
        if(input.placeholder is TMP_Text placeholder){signalTerminal.type(placeholder,false,16);placeholder.alignment=TextAlignmentOptions.Left;placeholder.textWrappingMode=TextWrappingModes.NoWrap;placeholder.color=signalTerminal.secondary;placeholder.text=password?(allowReveal?"Enter password":"Paste the email link"):"you@example.com";}
        if(input.GetComponent<terminalFocus>()==null)input.gameObject.AddComponent<terminalFocus>();
        if(r.Find("terminalRule")==null)signalTerminal.frame(r,signalTerminal.rule).box=true;
        var colors=input.colors;colors.selectedColor=new Color32(169,221,241,255);input.colors=colors;
        var error=note(block,"fieldError","",14);error.color=errorColor;error.gameObject.SetActive(false);
        var value=new field{input=input,error=error};fields.Add(value);
        input.onValueChanged.AddListener(_=>{error.gameObject.SetActive(false);reflow();});
        if(password && allowReveal)value.visibility=signalTerminal.button(inputRow,"passwordVisibility","Show",()=>{
            input.contentType=input.contentType==TMP_InputField.ContentType.Password?TMP_InputField.ContentType.Standard:TMP_InputField.ContentType.Password;
            input.ForceLabelUpdate();value.visibility.GetComponentInChildren<TMP_Text>().text=input.contentType==TMP_InputField.ContentType.Password?"Show":"Hide";
        },signalTerminal.actionStyle.quiet,64);
        return value;
    }
    private UnityEngine.UI.Button button(Transform parent,string name,string label,Action action,bool primary=false)
    {
        var r=row(parent,name+"Row");var b=signalTerminal.button(r,name,label,action,primary?signalTerminal.actionStyle.primary:signalTerminal.actionStyle.quiet,0);
        var layout=b.GetComponent<UnityEngine.UI.LayoutElement>();layout.minWidth=0;layout.flexibleWidth=1;return b;
    }
    private void moveButton(UnityEngine.UI.Button b,Transform parent,string text,Action action,bool primary,float width=0)
    {
        var r=width>0?(RectTransform)parent:row(parent,b.name+"Row");b.transform.SetParent(r,false);b.gameObject.SetActive(true);
        signalTerminal.height((RectTransform)b.transform,40);signalTerminal.width((RectTransform)b.transform,width);
        if(width==0)b.GetComponent<UnityEngine.UI.LayoutElement>().flexibleWidth=1;
        signalTerminal.styleButton(b,primary?signalTerminal.actionStyle.primary:signalTerminal.actionStyle.quiet);b.GetComponentInChildren<TMP_Text>().text=text;
        b.onClick.RemoveAllListeners();b.onClick.AddListener(()=>action());
    }
    private void bindSubmit(field value,Action action){value.input.onSubmit.AddListener(_=>enterSubmit(action));}
    private void enterSubmit(Action action)
    {if(account.busy || enterHandledFrame==Time.frameCount)return;enterHandledFrame=Time.frameCount;action();}
    private void focus(TMP_InputField input){StartCoroutine(focusNextFrame(input));}
    private IEnumerator focusNextFrame(TMP_InputField input)
    {yield return null;if(input!=null && input.gameObject.activeInHierarchy && !account.busy){EventSystem.current?.SetSelectedGameObject(input.gameObject);input.ActivateInputField();}}
    private void switchTo(entryState next)
    {
        if(account.busy)return;state=next;
        foreach(var pair in pages)pair.Value.gameObject.SetActive(pair.Key==next);
        foreach(var message in notices.Values){message.text="";message.gameObject.SetActive(false);}
        foreach(var item in fields)item.error.gameObject.SetActive(false);
        if(next==entryState.login)focus(loginEmail.input);
        if(next==entryState.createAccount)focus(signupEmail.input);
        if(next==entryState.forgotPassword)focus(resetEmail.input);
        if(next==entryState.resetPassword)focus(newPassword.input);
        reflow();
    }
    public void showLogin()
    {
        if(account.busy)return;
        string email=state==entryState.createAccount?signupEmail.input.text:state==entryState.forgotPassword || state==entryState.resetPassword?resetEmail.input.text:pendingEmail;
        if(!string.IsNullOrEmpty(email))loginEmail.input.SetTextWithoutNotify(email);
        account.cancelPasswordReset();
        switchTo(entryState.login);
    }
    public void showCreate()
    {if(account.busy)return;if(state==entryState.login)signupEmail.input.SetTextWithoutNotify(loginEmail.input.text);switchTo(entryState.createAccount);}
    public void showForgot()
    {if(account.busy)return;resetEmail.input.SetTextWithoutNotify(loginEmail.input.text);switchTo(entryState.forgotPassword);}
    private void correctEmail(){if(account.busy)return;signupEmail.input.SetTextWithoutNotify(pendingEmail);switchTo(entryState.createAccount);}
    private void showVerification(string email,bool accepted)
    {
        if(!string.IsNullOrEmpty(email))pendingEmail=email;
        switchTo(entryState.verifyEmail);
        linkSection.gameObject.SetActive(false);positionNotice(entryState.verifyEmail,ui.verifyButton.transform.parent);
        verificationHelp.text=(accepted?"Signup request accepted for ":"Confirm access to ")+(string.IsNullOrEmpty(pendingEmail)?"your email address":pendingEmail)+". If confirmation is needed, look for an email from the game.";
        reflow();
    }
    private bool checkEmail(field value)
    {return check(value,string.IsNullOrWhiteSpace(value.input.text)?"Enter your email address":accountAuthPolicy.validEmail(value.input.text)?"":"Enter a valid email address");}
    private bool check(field value,string problem)
    {value.error.text=problem;value.error.gameObject.SetActive(problem!="");return problem=="";}
    private void startValidation(){notices[state].gameObject.SetActive(false);notices[state].text="";}
    private void focusInvalid(params field[] values)
    {reflow();foreach(var item in values)if(item.error.gameObject.activeSelf){focus(item.input);break;}}
    public void submitLogin()
    {
        if(account.busy || state!=entryState.login)return;startValidation();
        bool emailOk=checkEmail(loginEmail),passwordOk=check(loginPassword,string.IsNullOrEmpty(loginPassword.input.text)?"Enter your password":"");
        if(!emailOk || !passwordOk){focusInvalid(loginEmail,loginPassword);return;}
        account.logIn(loginEmail.input.text,loginPassword.input.text);
    }
    public void submitSignup()
    {
        if(account.busy || state!=entryState.createAccount)return;startValidation();
        bool emailOk=checkEmail(signupEmail),passwordOk=check(signupPassword,accountAuthPolicy.passwordError(signupPassword.input.text));
        bool matches=check(confirmPassword,string.IsNullOrEmpty(confirmPassword.input.text)?"Confirm your password":signupPassword.input.text!=confirmPassword.input.text?"Passwords do not match":"");
        if(!emailOk || !passwordOk || !matches){focusInvalid(signupEmail,signupPassword,confirmPassword);return;}
        account.signUp(signupEmail.input.text,signupPassword.input.text);
    }
    private void positionNotice(entryState page,Transform before)
    {var r=notices[page].transform;r.SetParent(before.parent,false);r.SetAsLastSibling();r.SetSiblingIndex(before.GetSiblingIndex());}
    private void resendConfirmation(){if(!account.busy && account.emailCooldown(pendingEmail)==0){positionNotice(entryState.verifyEmail,ui.verifyButton.transform.parent);account.resendEmail(pendingEmail);}}
    private void submitLink(){if(!account.busy){positionNotice(entryState.verifyEmail,linkButton.transform.parent);account.useEmailLink(confirmationLink.input.text);}}
    private void sendReset(){if(account.busy)return;positionNotice(entryState.forgotPassword,sendResetButton.transform.parent);startValidation();if(!checkEmail(resetEmail)){focusInvalid(resetEmail);return;}account.recoverPassword(resetEmail.input.text);}
    private void submitResetLink(){if(!account.busy){positionNotice(entryState.forgotPassword,useResetButton.transform.parent);account.useEmailLink(resetLink.input.text,true);}}
    private void submitNewPassword()
    {
        if(account.busy)return;startValidation();bool valid=check(newPassword,accountAuthPolicy.passwordError(newPassword.input.text));
        bool matches=check(confirmNewPassword,newPassword.input.text!=confirmNewPassword.input.text?"Passwords do not match":string.IsNullOrEmpty(confirmNewPassword.input.text)?"Confirm your password":"");
        if(!valid || !matches){focusInvalid(newPassword,confirmNewPassword);return;}account.resetPassword(newPassword.input.text);
    }
    private void completed(cloudAccount.authResult result)
    {
        if(!initialized)return;
        if(result.success && result.awaitingConfirmation){clearPasswords();showVerification(result.email,true);return;}
        if(result.code=="email_not_confirmed"){showVerification(result.email,false);showNotice(result.message,false);return;}
        if(result.success && result.recoveryReady){clearPasswords();if(!string.IsNullOrEmpty(result.email))resetEmail.input.SetTextWithoutNotify(result.email);resetTarget.text="Set a new password for "+resetEmail.input.text;switchTo(entryState.resetPassword);return;}
        if(result.success && result.operation==cloudAccount.authOperation.resetPassword){clearPasswords();showLogin();showNotice(result.message,false);return;}
        if(result.code=="recovery_expired"){switchTo(entryState.forgotPassword);showNotice(result.message,true);return;}
        if(result.operation==cloudAccount.authOperation.verify && state==entryState.login && !result.success)showVerification(result.email,false);
        if(result.success && account.signedIn){clearPasswords();pendingEmail="";return;}
        showNotice(result.message,!result.success);
    }
    private void showNotice(string value,bool error)
    {var text=notices[state];text.text=value;text.color=error?errorColor:signalTerminal.accent;text.gameObject.SetActive(!string.IsNullOrEmpty(value));reflow();}
    private void clearPasswords()
    {foreach(var item in new[]{loginPassword,signupPassword,confirmPassword,confirmationLink,resetLink,newPassword,confirmNewPassword}){item.input.SetTextWithoutNotify("");item.input.contentType=TMP_InputField.ContentType.Password;item.input.ForceLabelUpdate();if(item.visibility!=null)item.visibility.GetComponentInChildren<TMP_Text>().text="Show";}}
    public void refresh()
    {
        if(!initialized)return;
        bool signed=account.signedIn && !account.authenticating;
        if(wasSignedIn && !signed && !account.busy){clearPasswords();loginEmail.input.SetTextWithoutNotify("");pendingEmail="";switchTo(entryState.login);}
        wasSignedIn=signed;
        foreach(var selectable in root.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))selectable.interactable=!account.busy;
        int remaining=account.emailCooldown(pendingEmail);ui.resendButton.interactable=!account.busy && remaining==0 && accountAuthPolicy.validEmail(pendingEmail);
        ui.resendButton.GetComponentInChildren<TMP_Text>().text=remaining>0?"Resend in "+remaining+"s":"Resend email";
        int resetRemaining=account.emailCooldown(resetEmail.input.text);sendResetButton.interactable=!account.busy && resetRemaining==0;
        sendResetButton.GetComponentInChildren<TMP_Text>().text=account.busy && state==entryState.forgotPassword?"Requesting...":resetRemaining>0?"Send again in "+resetRemaining+"s":"Send reset link";
        ui.loginButton.GetComponentInChildren<TMP_Text>().text=account.busy && state==entryState.login?"Logging in...":"Log in";
        createButton.GetComponentInChildren<TMP_Text>().text=account.busy && state==entryState.createAccount?"Creating account...":"Create account";
        resetButton.GetComponentInChildren<TMP_Text>().text=account.busy && state==entryState.resetPassword?"Saving password...":"Save new password";
        linkButton.GetComponentInChildren<TMP_Text>().text=account.busy && state==entryState.verifyEmail?"Checking link...":"Verify email link";
        reflow();
    }
    public void reflow()
    {
        if(root==null)return;
        float width=Mathf.Max(200,terminal.content.rect.width);
        foreach(var text in root.GetComponentsInChildren<TMP_Text>())if(text.transform.parent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>()!=null)
            signalTerminal.height(text.rectTransform,Mathf.Max(text.fontSize+4,text.GetPreferredValues(text.text,width,0).y+2));
        var columns=root.GetComponentsInChildren<UnityEngine.UI.VerticalLayoutGroup>();
        for(int i=columns.Length-1;i>=0;i--){var r=(RectTransform)columns[i].transform;UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(r);signalTerminal.height(r,columns[i].preferredHeight);}
    }
    private void Update()
    {
        refresh();
        if(!initialized || !root.gameObject.activeInHierarchy || account.busy)return;
        var keyboard=UnityEngine.InputSystem.Keyboard.current;
        if(keyboard==null || (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame))return;
        var selected=EventSystem.current?.currentSelectedGameObject;
        if(selected==null || selected.GetComponent<TMP_InputField>()==null)return;
        if(state==entryState.login)enterSubmit(submitLogin);
        else if(state==entryState.createAccount)enterSubmit(submitSignup);
        else if(state==entryState.verifyEmail && selected==confirmationLink.input.gameObject)enterSubmit(submitLink);
        else if(state==entryState.forgotPassword)enterSubmit(selected==resetLink.input.gameObject?(Action)submitResetLink:sendReset);
        else if(state==entryState.resetPassword)enterSubmit(submitNewPassword);
    }
    private void OnDestroy(){if(account!=null)account.authenticationFinished-=completed;}
}
