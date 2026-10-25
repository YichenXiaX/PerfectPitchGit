using System;
using System.IO;
using UnityEngine;

public class dataManager : MonoBehaviour
{
    public static dataManager instance { get; private set; }

    private string saveFolder;
    private string lastSavedSessionId;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        saveFolder = Path.Combine(Application.persistentDataPath, "SessionData");
        if (!Directory.Exists(saveFolder))
            Directory.CreateDirectory(saveFolder);

        Debug.Log($"[dataManager] Save folder: {saveFolder}");
    }

    private void OnEnable()
    {
        gameManager.instance.onGameOver += saveSession; //subscribe to onGameOver, saves data in the end
    }

    private void OnDisable()
    {
        if (gameManager.instance != null)
            gameManager.instance.onGameOver -= saveSession;
    }

    private void OnApplicationQuit()
    {
        // Save unfinished sessions too; saveSession ignores an already saved ID.
        var data = gameManager.instance?.getSessionData();
        if (data != null && data.results.Count > 0)
            saveSession(data);
    }

    public void saveSession(gameSessionData data)
    {
        if (data == null || data.sessionId == lastSavedSessionId) return;

        string json = JsonUtility.ToJson(data, true);  // pretty print

        // filename: PlayerName_2026-09-19_143052_abc1.json
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        string shortId = data.sessionId.Substring(0, Math.Min(4, data.sessionId.Length));
        string filename = $"{data.playerName}_{timestamp}_{shortId}.json";

        string fullPath = Path.Combine(saveFolder, filename);
        File.WriteAllText(fullPath, json);
        lastSavedSessionId = data.sessionId;

        Debug.Log($"[dataManager] Session saved ¡ú {fullPath}");
    }
}