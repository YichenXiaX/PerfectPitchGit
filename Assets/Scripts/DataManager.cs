using UnityEngine;

// Portfolio mode: never serialize trial-level research data to disk or the cloud.
public class dataManager : MonoBehaviour
{
    public static dataManager instance { get; private set; }
    private gameManager manager;
    private void Awake() { instance = this; }
    private void Start()
    {
        manager = gameManager.instance;
        if (manager != null) manager.onGameOver += saveSession;
    }
    private void OnDestroy()
    {
        if (manager != null) manager.onGameOver -= saveSession;
        if (instance == this) instance = null;
    }
    public void saveSession(gameSessionData data)
    {
        if (data == null) return;
        cloudAccount.instance?.capture(true);
        cloudAccount.instance?.retrySave();
    }
}
