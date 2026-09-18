using UnityEngine;

public class CometBehavior : MonoBehaviour
{

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.Translate(Vector3.down * GameSettings.Instance.cometSpeed * Time.deltaTime);
    }
}
