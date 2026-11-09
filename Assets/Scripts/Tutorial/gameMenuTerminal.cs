using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Only explicit menu roots move into these canvases. The gameplay HUD is never visited.
public static class gameMenuTerminal
{
    public static void isolate(GameObject root,string name,int order)
    {
        var c=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=c.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=order;signalTerminal.canvas(canvas);
        root.transform.SetParent(c.transform,false);root.transform.localScale=Vector3.one;root.transform.localRotation=Quaternion.identity;signalTerminal.fill((RectTransform)root.transform);
    }
    public static void start(GameObject root)
    {
        if(root==null)return;
        isolate(root,"startTerminal",60);
        foreach(Transform child in root.transform)child.gameObject.SetActive(false);
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==cloudAccount.playScene)
        {
            launchSequence.create(root,root.transform.Find("PlayButton").GetComponent<Button>());
            if(root.GetComponent<terminalKeyboardNavigation>()==null)root.AddComponent<terminalKeyboardNavigation>().blockWithAccount=true;
            return;
        }
        var panel=terminalPanel.create(root.transform,"startConsole");panel.preferredWidth=560;panel.top=180;panel.metadata.text="COMET CATCHER / HELIOS";panel.title.text="Ready for the Slipstream";
        signalTerminal.text(panel.content,"briefing","Listen. Predict. Intercept.\nKeep the carrier alive.",18,46);
        var button=root.transform.Find("PlayButton").GetComponent<Button>();button.transform.SetParent(panel.actions,false);button.gameObject.SetActive(true);signalTerminal.height((RectTransform)button.transform,40);signalTerminal.width((RectTransform)button.transform,180);signalTerminal.styleButton(button,signalTerminal.actionStyle.primary);button.GetComponentInChildren<TMP_Text>().text="Start run";
        if(root.GetComponent<terminalKeyboardNavigation>()==null)root.AddComponent<terminalKeyboardNavigation>().blockWithAccount=true;
    }
    public static void gameOver(GameObject root)
    {
        if(root==null)return;
        signalTerminal.image((RectTransform)root.transform,Color.clear);
        if(gameManager.instance!=null && gameManager.instance.isPractice)return;
        isolate(root,"gameOverTerminal",65);
        var ui=Object.FindFirstObjectByType<gameOverUi>();var oldStats=root.transform.Find("StatsCard");
        var panel=terminalPanel.create(root.transform,"runResult");panel.preferredWidth=560;panel.top=155;panel.metadata.text="HELIOS / RUN ENDED";panel.title.text="Hull breached";
        var grid=signalTerminal.node(panel.content,"runStatistics");signalTerminal.height(grid,132);
        var values=new[]{ui.finalLevelText,ui.totalRoundsText,ui.predictedText,ui.destroyedText,ui.hitText,ui.finalAuraText};
        for(int i=0;i<values.Length;i++){var t=values[i];if(t==null)continue;t.transform.SetParent(grid,false);signalTerminal.type(t,false,18);t.transform.localScale=Vector3.one;t.margin=Vector4.zero;var r=t.rectTransform;r.anchorMin=new Vector2(i%2*.5f,1-(i/2+1)/3f);r.anchorMax=new Vector2((i%2+1)*.5f,1-(i/2)/3f);r.offsetMin=Vector2.zero;r.offsetMax=new Vector2(-12,0);t.alignment=TextAlignmentOptions.MidlineLeft;}
        oldStats.gameObject.SetActive(false);
        var button=ui.restartButton;button.transform.SetParent(panel.actions,false);signalTerminal.height((RectTransform)button.transform,40);signalTerminal.width((RectTransform)button.transform,180);signalTerminal.styleButton(button,signalTerminal.actionStyle.primary);button.GetComponentInChildren<TMP_Text>().text="Replay";
        if(root.GetComponent<terminalKeyboardNavigation>()==null)root.AddComponent<terminalKeyboardNavigation>().blockWithAccount=true;
    }
}
