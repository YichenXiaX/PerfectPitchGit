using UnityEngine;

public class FailedPredictionBullet : MonoBehaviour
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

    private void OnTriggerEnter2D(Collider2D other) //failed prediction bullet does not interfere with the comet
    {
        
        if (other.CompareTag("BulletCatcher"))
        {

            Debug.Log("alkdjfk;lasdjklfjklsadjkflfailedalksdjflkajslkdfjklasdjfklasdk;lf");
            GameManager.Instance.OnPlayerFailedPrediction();
        }
        if (other.CompareTag("BulletDestroyer"))
        {
            Destroy(gameObject);
        }
    }
}
