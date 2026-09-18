using UnityEngine;

public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance { get; private set; }

    [System.Serializable]
    public class LevelPreset
    {
        public string name;
        public int notesPerSequence = 4;
        public float noteDuration = 0.95f;
        public float pauseBetweenNotes = 0.15f;
        public float sequenceInterval = 6f;
        public int[] octaveShifts = { 0 };
        public float responseWindow = 5f;
        public int cometSpeed = 5;
    }

    [Header("Player")]
    public string playerName;
    public bool leftHanded;

    [Header("Current Level")]
    public int level;

    [Header("Level Presets")]
    public LevelPreset[] levelPresets;

    [Header("General")]
    public float waitTime;
    public float cometSpeed = 10f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public LevelPreset GetCurrentPreset()
    {
        if (levelPresets == null || levelPresets.Length == 0)
        {
            Debug.LogError("No level presets defined!");
            return new LevelPreset();
        }

        int index = Mathf.Clamp(level, 0, levelPresets.Length - 1);
        return levelPresets[index];
    }
}