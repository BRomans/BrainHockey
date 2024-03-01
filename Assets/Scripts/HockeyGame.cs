using UnityEngine;

public class PongGame : MonoBehaviour
{
    public GameObject disk;
    public GameObject playerPaddle;
    public GameObject aiPaddle;
    public float paddleSpeed = 5f;

    public bool discreteMovements = false;

    private bool isPlaying = false;

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            if (!isPlaying)
            {
                disk.GetComponent<Disk>().Launch();
                isPlaying = true;
            } else 
            {
                isPlaying = false;
                disk.GetComponent<Disk>().Reset();
            }
        }
        if(isPlaying)
        {
            disk.GetComponent<Disk>().MoveDisk();;
        }
    
        MovePaddle(playerPaddle, KeyCode.W, KeyCode.S);
        MovePaddle(playerPaddle, KeyCode.UpArrow, KeyCode.DownArrow);
       
    }

    void MovePaddle(GameObject paddle, KeyCode upKey, KeyCode downKey)
    {
        // Move the paddle based on keyboard input
        if (Input.GetKeyDown(upKey))
        {
            paddle.GetComponent<Paddle>().MoveUp(paddleSpeed);
        }
        else if (Input.GetKeyDown(downKey))
        {
            paddle.GetComponent<Paddle>().MoveDown(paddleSpeed);
        }
    }
}
