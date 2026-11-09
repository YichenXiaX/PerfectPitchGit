using UnityEngine;

public class cometBehavior : MonoBehaviour
{
    private float rotationSpeed; // Speed of rotation (randomized for each comet)
    private float fixedX; // store the spawn X
    public GameObject mothership;

    void Start()
    {
        // Assign a random rotation speed between -200 and 200 for spinning
        rotationSpeed = Random.Range(-200f, 200f);

        // Optionally, set a random starting rotation angle for variety
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        fixedX = transform.position.x; //locks the x axis
    }

    void Update()
    {
        Vector3 pos = transform.position;
        // Move the comet downward continuously
        pos.y -= gameSettings.instance.getCurrentPreset().cometSpeed * Time.deltaTime;
        pos.x = fixedX; // no horizontal drift ever
        transform.position = pos;

        // Rotate the comet circularly on its own center using the random speed
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Mothership"))
        {
            other.GetComponent<AudioSource>().Play();
            other.GetComponent<mothershipMovement>().hit();
            gameManager.instance.onCometHitShip();
            cometExplosion.spawn(transform.position, GetComponent<SpriteRenderer>());
            Destroy(gameObject);
        }
    }
}