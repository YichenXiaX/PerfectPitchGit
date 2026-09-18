using System.Collections;
using UnityEngine;

public class QuadrantNoteTester : MonoBehaviour
{
    public FrequencyManager frequencyManager;

    private Coroutine sequenceLoop;

    void Start()
    {
        if (frequencyManager == null)
        {
            Debug.LogError("FrequencyManager not assigned!");
            return;
        }

        sequenceLoop = StartCoroutine(SequenceLoop());
    }

    private IEnumerator SequenceLoop()
    {
        while (true)
        {
            var preset = GameSettings.Instance.GetCurrentPreset();
            int quadrant = Random.Range(0, 4);
            Debug.Log($"--- Triggering sequence for Q{quadrant + 1} ---");
            frequencyManager.PlayNoteSequence(quadrant);
            yield return new WaitForSeconds(preset.sequenceInterval);
        }
    }

    public void StopSequences()
    {
        if (sequenceLoop != null)
        {
            StopCoroutine(sequenceLoop);
            sequenceLoop = null;
        }
    }
}