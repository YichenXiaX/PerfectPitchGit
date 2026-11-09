using UnityEngine;
using TMPro;

// Reveal already-laid-out text so words do not jump between lines while typing.
public class terminalTransmission : MonoBehaviour
{
    [Min(.005f)] public float characterInterval = .025f;
    public bool paused { get; set; }
    public int visibleCharacters { get; private set; }
    private TMP_Text text, cursor;
    private Material glow;
    private float elapsed, nextCharacter, blink;
    private string message;
    public void initialize(TMP_Text target, string value)
    {
        text = target; message = value; text.text = value;
        // Instance material only: the shared font and Aura HUD material stay intact.
        glow = new Material(text.fontSharedMaterial);
        if (glow.HasProperty("_GlowColor"))
        {
            glow.EnableKeyword("GLOW_ON"); glow.SetColor("_GlowColor", new Color(.25f, .85f, 1, .4f));
            glow.SetFloat("_GlowOuter", .12f); glow.SetFloat("_GlowPower", .18f);
        }
        text.fontMaterial = glow;
        cursor = signalTerminal.text(text.transform, "transmissionCursor", "_", text.fontSize, text.fontSize, true);
        cursor.color = text.color; cursor.fontSharedMaterial = glow;
        cursor.rectTransform.anchorMin = cursor.rectTransform.anchorMax = new Vector2(.5f, .5f);
        cursor.rectTransform.pivot = new Vector2(0, .5f); cursor.rectTransform.sizeDelta = new Vector2(30, 40);
        cursor.alignment = TextAlignmentOptions.MidlineLeft;
        text.ForceMeshUpdate(); text.maxVisibleCharacters = 0;
    }
    private void Update()
    {
        if (text == null || paused) return;
        elapsed += Time.unscaledDeltaTime; blink += Time.unscaledDeltaTime;
        while (visibleCharacters < message.Length && elapsed >= nextCharacter)
        {
            char letter = message[visibleCharacters++];
            nextCharacter += characterInterval + (letter == '.' || letter == '\n' ? .18f : letter == ',' ? .08f : 0);
        }
        text.maxVisibleCharacters = visibleCharacters; text.ForceMeshUpdate();
        cursor.fontSize = text.fontSize;
        int index = Mathf.Min(visibleCharacters - 1, text.textInfo.characterCount - 1);
        if (index >= 0)
        {
            var c = text.textInfo.characterInfo[index];
            cursor.rectTransform.localPosition = new Vector3(c.xAdvance + 2, c.baseLine + text.fontSize * .27f, 0);
        }
        cursor.enabled = index >= 0 && (visibleCharacters < message.Length || blink % 1 < .6f);
    }
    private void OnDestroy() { if (glow != null) Destroy(glow); }
}
