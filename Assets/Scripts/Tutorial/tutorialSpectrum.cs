using UnityEngine;
using TMPro;

// Inert previews read the gameplay prefab array. No comet components are instantiated here.
public class tutorialSpectrum : MonoBehaviour
{
    private readonly UnityEngine.UI.Image[] bands=new UnityEngine.UI.Image[4];
    private readonly UnityEngine.UI.Image[] cards=new UnityEngine.UI.Image[4];
    private RectTransform marker;
    private TMP_Text noteLabel;
    private frequencyManager frequencies;
    public int highlighted {get;private set;}=-1;
    public float displayedFrequency {get;private set;}
    public static tutorialSpectrum create(Transform parent,frequencyManager source)
    {
        var root=signalTerminal.node(parent,"soundCometMap");signalTerminal.height(root,300);
        var view=root.gameObject.AddComponent<tutorialSpectrum>();view.frequencies=source;view.build();return view;
    }
    private void build()
    {
        var root=(RectTransform)transform;
        var arrow=signalTerminal.text(root,"pitchDirection","← Pitch rises this way",16,22);terminalPanel.place(arrow.rectTransform,0,0,0,22);arrow.alignment=TextAlignmentOptions.Center;
        var high=signalTerminal.text(root,"high","HIGH",12,20,true);terminalPanel.place(high.rectTransform,0,0,0,20);
        var low=signalTerminal.text(root,"low","LOW",12,20,true);terminalPanel.place(low.rectTransform,0,0,0,20);low.alignment=TextAlignmentOptions.Right;
        var piano=signalTerminal.node(root,"reversedPiano");terminalPanel.place(piano,0,26,0,86);
        string[] whites={"C5","B4","A4","G4","F4","E4","D4","C4"};
        for(int i=0;i<8;i++)
        {
            var key=signalTerminal.node(piano,whites[i]);key.anchorMin=new Vector2(i/8f,0);key.anchorMax=new Vector2((i+1)/8f,1);key.offsetMin=new Vector2(1,0);key.offsetMax=new Vector2(-1,0);signalTerminal.image(key,new Color32(218,231,239,255));
            var label=signalTerminal.text(key,"note",whites[i],15,22);label.color=signalTerminal.background;label.alignment=TextAlignmentOptions.Center;label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=new Vector2(1,0);label.rectTransform.pivot=new Vector2(.5f,0);label.rectTransform.sizeDelta=new Vector2(0,24);label.rectTransform.anchoredPosition=new Vector2(0,3);
        }
        int[] boundaries={2,3,4,6,7};string[] blacks={"A♯4","G♯4","F♯4","D♯4","C♯4"};
        for(int i=0;i<boundaries.Length;i++)
        {
            var key=signalTerminal.node(piano,blacks[i]);key.anchorMin=new Vector2((boundaries[i]-.29f)/8f,.35f);key.anchorMax=new Vector2((boundaries[i]+.29f)/8f,1);key.offsetMin=key.offsetMax=Vector2.zero;signalTerminal.image(key,signalTerminal.background);
            var label=signalTerminal.text(key,"note",blacks[i],15,20);signalTerminal.fill(label.rectTransform,1);label.alignment=TextAlignmentOptions.Bottom;label.margin=new Vector4(0,0,0,5);
        }
        var caption=signalTerminal.text(root,"referenceCaption","C4–C5 piano reference · spectrum below uses logarithmic pitch",13,20);terminalPanel.place(caption.rectTransform,0,116,0,20);caption.color=signalTerminal.secondary;
        var connectors=signalTerminal.node(root,"pitchConnections");terminalPanel.place(connectors,0,137,0,20);
        var links=connectors.gameObject.AddComponent<tutorialPitchConnections>();links.source=frequencies;links.color=signalTerminal.rule;links.raycastTarget=false;
        var strip=signalTerminal.node(root,"logarithmicSpectrum");terminalPanel.place(strip,0,160,0,24);
        float lo=frequencies.quadrants[3].minFrequency,hi=frequencies.quadrants[0].maxFrequency;
        for(int i=0;i<4;i++)
        {
            var q=frequencies.quadrants[i];float a=position(q.maxFrequency),b=position(q.minFrequency);
            var band=signalTerminal.node(strip,"quadrant"+(i+1));band.anchorMin=new Vector2(a,0);band.anchorMax=new Vector2(b,1);band.offsetMin=new Vector2(1,0);band.offsetMax=new Vector2(-1,0);bands[i]=signalTerminal.image(band,signalTerminal.rule);
            var card=signalTerminal.node(root,"cometQ"+(i+1));card.anchorMin=new Vector2(a,1);card.anchorMax=new Vector2(b,1);card.pivot=new Vector2(.5f,1);card.offsetMin=new Vector2(1,-300);card.offsetMax=new Vector2(-1,-208);cards[i]=signalTerminal.image(card,signalTerminal.panelColor);
            var title=signalTerminal.text(card,"quadrantLabel","Q"+(i+1),14,20,true);terminalPanel.place(title.rectTransform,5,2,-40,20);
            var key=terminalKeycap.create(card,controlLayout.label(i),i);var kr=(RectTransform)key.transform;kr.anchorMin=kr.anchorMax=Vector2.one;kr.pivot=Vector2.one;kr.anchoredPosition=new Vector2(-4,-1);kr.sizeDelta=new Vector2(30,28);
            var imageRect=signalTerminal.node(card,"cometPreview");imageRect.anchorMin=new Vector2(.1f,0);imageRect.anchorMax=new Vector2(.9f,.67f);imageRect.offsetMin=imageRect.offsetMax=Vector2.zero;
            var image=imageRect.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=gameSettings.instance.cometPrefabs[i].GetComponentInChildren<SpriteRenderer>().sprite;image.preserveAspect=true;image.raycastTarget=false;
        }
        var range=signalTerminal.node(root,"baseRange");terminalPanel.place(range,0,186,0,18);
        for(int i=0;i<=4;i++)
        {
            float frequency=i==4?lo:frequencies.quadrants[i].maxFrequency;
            var label=signalTerminal.text(range,"boundary"+i,frequency.ToString("F1")+" Hz",12,16);label.color=signalTerminal.secondary;
            var r=label.rectTransform;r.anchorMin=r.anchorMax=new Vector2(position(frequency),.5f);r.pivot=new Vector2(i==0?0:i==4?1:.5f,.5f);r.anchoredPosition=Vector2.zero;r.sizeDelta=new Vector2(70,18);label.alignment=i==0?TextAlignmentOptions.Left:i==4?TextAlignmentOptions.Right:TextAlignmentOptions.Center;
        }
        marker=signalTerminal.node(strip,"soundingPitch");marker.anchorMin=marker.anchorMax=new Vector2(0,.5f);marker.sizeDelta=new Vector2(3,30);signalTerminal.image(marker,signalTerminal.ink);marker.gameObject.SetActive(false);
        noteLabel=signalTerminal.text(root,"notePlaying","",13,20);terminalPanel.place(noteLabel.rectTransform,0,186,0,18);noteLabel.alignment=TextAlignmentOptions.Center;noteLabel.color=signalTerminal.accent;
    }
    public float position(float frequency)
    {float low=frequencies.quadrants[3].minFrequency,high=frequencies.quadrants[0].maxFrequency;return Mathf.Log(high/frequency)/Mathf.Log(high/low);}
    public void highlight(int quadrant,float frequency=0,int octave=0)
    {
        highlighted=quadrant;displayedFrequency=frequency;
        for(int i=0;i<4;i++){bands[i].color=i==quadrant?signalTerminal.accent:signalTerminal.rule;cards[i].color=i==quadrant?new Color32(26,58,77,255):signalTerminal.panelColor;}
        marker.gameObject.SetActive(frequency>0);
        if(frequency>0){marker.anchorMin=marker.anchorMax=new Vector2(position(frequency/Mathf.Pow(2,octave)),.5f);marker.anchoredPosition=Vector2.zero;}
        noteLabel.text=frequency>0?"Q"+(quadrant+1)+" · "+frequency.ToString("F1")+" Hz" : "";
        transform.Find("baseRange").gameObject.SetActive(frequency<=0);
    }
}
