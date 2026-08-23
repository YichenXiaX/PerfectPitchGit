using System.ComponentModel;
using UnityEngine;


//this file is used to store all the different variables, which helps us to store data in JSON and create in-game configurations

public class GameSettings : MonoBehaviour
{

    public static GameSettings Instance { get; private set; }

    public float waitTime;
    public int level;
    public string playerName;

    public bool leftHanded;

    public float lerpSpeed = 10;


    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
