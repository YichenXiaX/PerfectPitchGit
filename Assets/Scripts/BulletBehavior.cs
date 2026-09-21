using UnityEngine;

public class BulletBehavior : MonoBehaviour
{
    //public GameManager gameManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.Translate(Vector3.up * 10 * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the comet hits the mothership
        if (other.CompareTag("Comet"))
        {
            GameManager.Instance.OnCometDestroyedByPlayer();
            Destroy(other.gameObject);
            Destroy(gameObject);
        }
        if (other.CompareTag("BulletCatcher"))
        {
            GameManager.Instance.OnPlayerMissedShot();
            Destroy(gameObject);
        }
    }
}
