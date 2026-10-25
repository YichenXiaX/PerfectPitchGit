using System.Collections;
using UnityEngine;

public class quadrantNoteTester : MonoBehaviour
{
    public frequencyManager frequencyManager;

    private Coroutine sequenceLoop;

    void Start()
    {
        if (frequencyManager == null)
        {
            Debug.LogError("frequencyManager not assigned!");
            return;
        }

        sequenceLoop = StartCoroutine(runSequenceLoop());
    }

    private IEnumerator runSequenceLoop()
    {
        while (true)
        {
            var preset = gameSettings.instance.getCurrentPreset();

            int quadrant = Random.Range(0, 4);
            Debug.Log($"--- Triggering sequence for Q{quadrant + 1} ---");
            frequencyManager.playNoteSequence(quadrant);

            // Wait for all notes to finish playing
            float sequenceDuration = preset.notesPerSequence * (preset.noteDuration + preset.pauseBetweenNotes);

            // Then wait the gap before the next sequence
            //yield return new WaitForSeconds(sequenceDuration + preset.sequenceInterval);
        }
    }

    public void stopSequences() 
    {
        if (sequenceLoop != null)
        {
            StopCoroutine(sequenceLoop);
            sequenceLoop = null;
        }
    }
}