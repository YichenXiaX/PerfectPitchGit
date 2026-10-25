using UnityEngine;

public class gameSettings : MonoBehaviour
{
    public static gameSettings instance { get; private set; }

    [System.Serializable]
    public class levelPreset
    {
        public string name;
        public int notesPerSequence = 4;
        public float noteDuration = 0.95f;
        public float pauseBetweenNotes = 0.15f;
        public int[] octaveShifts = { 0 };
        public float predictionWindow = 8f;
        public float cometSpawnDelay = 3;
        public float cometSpeed = 2f;
        public int consecutiveCorrectToAdvance = 3;
        public float pauseBetweenRounds = 2f;
    }

    [Header("Mode")]
    public bool isCustomMode = false;
    public levelPreset customPreset = new levelPreset();

    [Header("Player")]
    public string playerName;
    public bool leftHanded;

    [Header("Health")]
    public int maxHealth = 100;

    [Header("Current Level")]
    public int level;

    [Header("Level Presets")]
    public levelPreset[] levelPresets;

    [Header("Comets")]
    public GameObject[] cometPrefabs = new GameObject[4];   // one per quadrant
    public float cometSpawnY = 5f;  // top of screen
    //public float cometSpawnDelay = 0.5f; //gap after player mispredicts before comet spawns



    //public float cometSpeed = 2f;

    //[Header("General")]
    //public float waitTime;
    //public float cometSpeed = 10f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public levelPreset getCurrentPreset()
    {
        if (isCustomMode)
            return customPreset;

        if (levelPresets == null || levelPresets.Length == 0)
        {
            Debug.LogError("No level presets defined!");
            return new levelPreset();
        }

        int index = Mathf.Clamp(level, 0, levelPresets.Length - 1);
        return levelPresets[index];
    }


}