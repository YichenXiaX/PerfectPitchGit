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
        public int[] octaveShifts = { 0 };
        public float preSpawnDelay = 3f;   // gap after sequence ends before comet spawns
        public float cometSpeed = 2f;
        //public float responseWindow = 5f;
        public int consecutiveCorrectToAdvance = 3;
        public float pauseBetweenRounds = 2f;
    }

    [Header("Mode")]
    public bool isCustomMode = false;
    public LevelPreset customPreset = new LevelPreset();

    [Header("Player")]
    public string playerName;
    public bool leftHanded;

    [Header("Health")]
    public int maxHealth = 3;

    [Header("Current Level")]
    public int level;

    [Header("Level Presets")]
    public LevelPreset[] levelPresets;

    [Header("Comets")]
    public GameObject[] cometPrefabs = new GameObject[4];   // one per quadrant
    public float cometSpawnY = 5f;  // top of screen
    public float cometSpawnDelay = 0.5f;
    //public float cometSpeed = 2f;

    [Header("General")]
    public float waitTime;
    //public float cometSpeed = 10f;

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
        if (isCustomMode)
            return customPreset;

        if (levelPresets == null || levelPresets.Length == 0)
        {
            Debug.LogError("No level presets defined!");
            return new LevelPreset();
        }

        int index = Mathf.Clamp(level, 0, levelPresets.Length - 1);
        return levelPresets[index];
    }
}