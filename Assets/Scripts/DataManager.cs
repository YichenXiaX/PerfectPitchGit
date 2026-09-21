using System;
using System.IO;
using UnityEngine;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    private string saveFolder;
    private bool hasSavedThisSession = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        saveFolder = Path.Combine(Application.persistentDataPath, "SessionData");
        if (!Directory.Exists(saveFolder))
            Directory.CreateDirectory(saveFolder);

        Debug.Log($"[DataManager] Save folder: {saveFolder}");
    }

    private void OnEnable()
    {
        GameManager.Instance.OnGameOver += SaveSession; //subscribe to OnGameOver, saves data in the end
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameOver -= SaveSession;
    }

    private void OnApplicationQuit()
    {
        // Safety net ¡ª if game didn't end cleanly, save whatever we have
        if (!hasSavedThisSession)
        {
            var data = GameManager.Instance?.GetSessionData();
            if (data != null && data.results.Count > 0)
            {
                Debug.Log("[DataManager] App quitting ¡ª emergency save");
                SaveSession(data);
            }
        }
    }

    public void SaveSession(GameSessionData data)
    {
        if (hasSavedThisSession) return;

        string json = JsonUtility.ToJson(data, true);  // pretty print

        // filename: PlayerName_2026-09-19_143052_abc1.json
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        string shortId = data.sessionId.Substring(0, Math.Min(4, data.sessionId.Length));
        string filename = $"{data.playerName}_{timestamp}_{shortId}.json";

        string fullPath = Path.Combine(saveFolder, filename);
        File.WriteAllText(fullPath, json);
        hasSavedThisSession = true;

        Debug.Log($"[DataManager] Session saved ¡ú {fullPath}");
    }
}