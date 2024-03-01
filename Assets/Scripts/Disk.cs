using UnityEngine;

public class Disk : MonoBehaviour
{
    public float speed = 5f;
    public Vector3 startPosition;

    private Vector3 diskVelocity; // Velocity of the ball

    void Start()
    {
        startPosition = transform.position;
    }

    public void Reset()
    {
        diskVelocity = Vector3.zero;
        transform.position = startPosition;
    }

    public void Launch()
    {
        float speedX = Random.Range(0, 5) == 0 ? -speed : speed;
        float speedY = Random.Range(0, 5) == 0 ? -speed : speed;
        // Set initial velocity for the ball
        diskVelocity = new Vector3(speedX, speedY, 0f);
    }

    public void MoveDisk()
    {
        // Move the ball based on its velocity
        transform.Translate(diskVelocity * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Paddle"))
        {
            // Reverse the ball's horizontal velocity to simulate bounce
            diskVelocity.x *= -1;
        }
        if(collision.gameObject.CompareTag("Wall"))
        {
            // Reverse the ball's vertical velocity to simulate bounce
            diskVelocity.y *= -1;
        }
        if(collision.gameObject.CompareTag("Goal"))
        {
            Reset();
        }
    }
}
