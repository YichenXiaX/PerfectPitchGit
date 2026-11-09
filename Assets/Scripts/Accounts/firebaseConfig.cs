using System;
using UnityEngine;

// Public Firebase configuration only. Never put an Admin SDK key in Resources.
[Serializable]
public class firebaseConfig
{
    public string projectId = "comet-catcher", apiKey = "";
    public string authDomain => projectId + ".firebaseapp.com";
    public string authUrl => "https://identitytoolkit.googleapis.com/v1";
    public string tokenUrl => "https://securetoken.googleapis.com/v1/token";
    public string databaseUrl => "https://firestore.googleapis.com/v1/projects/" + projectId + "/databases/(default)/documents";
    public static firebaseConfig load()
    {
        var asset = Resources.Load<TextAsset>("firebaseConfig");
        return asset == null ? new firebaseConfig() : JsonUtility.FromJson<firebaseConfig>(asset.text);
    }
}
