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
        if(instance != null && instance != this) { Destroy(this); return; }
        // The saved GameSettings object also owns settingsUi. Persist only data,
        // never the UI's references into a scene that is about to unload.
        if(GetComponent<settingsUi>() != null)
        {
            string values=JsonUtility.ToJson(this);
            var persistent=new GameObject("gameSettingsPreferences").AddComponent<gameSettings>();
            JsonUtility.FromJsonOverwrite(values,persistent);
            persistent.leftHanded=controlLayout.savedLeftHanded;
            Destroy(this);
            return;
        }
        instance=this;
        leftHanded=controlLayout.savedLeftHanded;
        DontDestroyOnLoad(gameObject);
    }
    private void OnDestroy() { if(instance==this) instance=null; }
    public levelPreset getCurrentPreset()
    {
        if (isCustomMode)
            return normalizePreset(customPreset);

        if (levelPresets == null || levelPresets.Length == 0)
        {
            Debug.LogError("No level presets defined!");
            return new levelPreset();
        }

        int index = Mathf.Clamp(level, 0, levelPresets.Length - 1);
        return normalizePreset(levelPresets[index]);
    }

    // Old presets keep every supported choice; unsupported values are removed,
    // never clamped into a different octave. An empty selection falls back to 0.
    public static int[] normalizeOctaves(int[] values)
    {
        bool alreadyValid=values!=null && values.Length>0;
        if(alreadyValid)for(int i=0;i<values.Length;i++)if(values[i]<-2 || values[i]>2 || (i>0 && values[i]<=values[i-1])){alreadyValid=false;break;}
        if(alreadyValid)return values;
        var valid=new System.Collections.Generic.SortedSet<int>();
        if(values!=null)foreach(int value in values)if(value>=-2 && value<=2)valid.Add(value);
        if(valid.Count==0)valid.Add(0);
        var result=new int[valid.Count];valid.CopyTo(result);return result;
    }
    public static levelPreset normalizePreset(levelPreset value)
    {if(value==null)value=new levelPreset();value.octaveShifts=normalizeOctaves(value.octaveShifts);return value;}


}
