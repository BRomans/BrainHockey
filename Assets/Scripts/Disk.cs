using UnityEngine;
using UnityEngine.Events;

public class Disk : MonoBehaviour
{
    public UnityEvent OnP2Scored;

    public UnityEvent OnP1Scored;

    public float speed = 5f;
    public Vector3 startPosition;

    private Vector3 diskVelocity; // Velocity of the ball

    private TaskController _taskController;

    void Start()
    {
        startPosition = transform.position;
        try
        {
            _taskController = GameObject.FindGameObjectWithTag("Task").GetComponent<TaskController>();
        } catch (System.Exception e)
        {
            Debug.Log("TaskController not found: " + e.Message);
        }
    }

    public void Reset()
    {
        diskVelocity = Vector3.zero;
        transform.position = startPosition;
        if(_taskController != null && _taskController.IsTaskRunning())
            Invoke("Launch", 0.5f);
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
        if(collision.gameObject.CompareTag("TrialCounter"))
        {
            if(_taskController != null && !_taskController.IsTaskRunning())
                _taskController.StartTaskTimer();
        }
        if (collision.gameObject.CompareTag("EndTask"))
        {
            if(_taskController != null)
            {
                if(collision.name == "EndTask_1")
                {
                    _taskController.BallMissed(1);
                }
                if(collision.name == "EndTask_2")
                {
                    _taskController.BallMissed(2);
                }
                if(collision.name == "EndTask_3")
                {
                    _taskController.BallMissed(3);
                }
            }
        }

        if (collision.gameObject.CompareTag("Paddle"))
        {
            // Reverse the ball's horizontal velocity to simulate bounce
            diskVelocity.x *= -1;
            if(collision.name == "Player")
            {
                Paddle paddle = collision.GetComponent<Paddle>();
                if (_taskController != null && _taskController.IsTaskRunning())
                {
                    _taskController.BallHit(paddle.currentAnchor);
                }
            }
        }
        if(collision.gameObject.CompareTag("Wall"))
        {
            // Reverse the ball's vertical velocity to simulate bounce
            diskVelocity.y *= -1;
        }
        if(collision.gameObject.CompareTag("Goal"))
        {
            if (collision.gameObject.name == "PlayerGoal")
            {
                OnP2Scored.Invoke();
            }
            else
            {
                OnP1Scored.Invoke();
            }
            Reset();
        }
    }
}
