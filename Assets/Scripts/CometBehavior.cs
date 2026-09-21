using UnityEngine;

public class CometBehavior : MonoBehaviour
{
    private float rotationSpeed; // Speed of rotation (randomized for each comet)
    public GameObject mothership;

    void Start()
    {
        // Assign a random rotation speed between -200 and 200 for spinning
        rotationSpeed = Random.Range(-200f, 200f);

        // Optionally, set a random starting rotation angle for variety
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
    }

    void Update()
    {
        // Move the comet downward continuously
        transform.Translate(Vector3.down * GameSettings.Instance.GetCurrentPreset().cometSpeed * Time.deltaTime, Space.World);

        // Rotate the comet circularly on its own center using the random speed
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Mothership"))
        {
            other.GetComponent<AudioSource>().Play();
            other.GetComponent<MothershipMovement>().Hit();
            GameManager.Instance.OnCometHitShip();
            CometExplosion.Spawn(transform.position, GetComponent<SpriteRenderer>());
            Destroy(gameObject);
        }
    }
}