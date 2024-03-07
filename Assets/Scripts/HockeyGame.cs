using UnityEngine;

public class PongGame : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The disk object that will be used in the game")]
    public GameObject disk;

    [SerializeField]
    [Tooltip("The player's paddle object")]
    public GameObject playerPaddle;

    [SerializeField]
    [Tooltip("The AI's paddle object")]
    public GameObject aiPaddle;

    [SerializeField]
    [Tooltip("The text object that will display the player's score")]
    private TMPro.TextMeshProUGUI playerScoreText;

    [SerializeField]
    [Tooltip("The text object that will display the AI's score")]
    private TMPro.TextMeshProUGUI aiScoreText;
    public float paddleSpeed = 5f;

    public bool discreteMovements = false;

    private bool isPlaying = false;

    private int playerScore = 0;

    private int aiScore = 0;

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
       
       if(Input.GetKeyDown(KeyCode.Escape))
       {
           ExitGame();
       }
    }

    void MovePaddle(GameObject paddle, KeyCode upKey, KeyCode downKey)
    {
        // Move the paddle based on keyboard input
        bool discreteMovements = paddle.GetComponent<Paddle>().discreteMovements;
        bool upKeyPressed = discreteMovements? Input.GetKeyDown(upKey) : Input.GetKey(upKey);
        bool downKeyPressed = discreteMovements? Input.GetKeyDown(downKey) : Input.GetKey(downKey);
        if (upKeyPressed)
        {
            paddle.GetComponent<Paddle>().MoveUp(paddleSpeed);
        }
        else if (downKeyPressed)
        {
            paddle.GetComponent<Paddle>().MoveDown(paddleSpeed);
        }
    }

    public void UpdatePlayerScore()
    {
        playerScore++;
        playerScoreText.text = playerScore.ToString();
    }

    public void UpdateAIScore()
    {
        aiScore++;
        aiScoreText.text = aiScore.ToString();
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
