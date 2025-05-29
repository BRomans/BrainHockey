using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class PongGame : MonoBehaviour
{
    public UnityEvent OnRoundStart;
    public UnityEvent OnRoundEnd;

    [SerializeField]
    [Tooltip("Play to the best of X games.")]
    public int BestOf = 3;

    [SerializeField]
    [Tooltip("The disk object that will be used in the game")]
    public GameObject disk;

    [SerializeField]
    [Tooltip("The player's paddle object")]
    public GameObject playerPaddle;

    [SerializeField]
    [Tooltip("The player2's paddle object")]
    public GameObject player2Paddle;

    [SerializeField]
    [Tooltip("The text object that will display the player's score")]
    private TMPro.TextMeshProUGUI playerScoreText;

    [SerializeField]
    [Tooltip("The text object that will display the AI's score")]
    private TMPro.TextMeshProUGUI player2ScoreText;
    public float paddleSpeed = 5f;

    public bool discreteMovements = false;

    private bool isPlaying = false;

    private bool gameOver = false;

    private int playerScore = 0;

    private int player2Score = 0;

    void Start()
    {
        OnRoundStart.Invoke();
    }
    void Update()
    {
       
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartGame();
        }
        if (isPlaying)
        {
            disk.GetComponent<Disk>().MoveDisk(); ;
        }

        MovePaddle(playerPaddle, KeyCode.W, KeyCode.S);
        MovePaddle(player2Paddle, KeyCode.UpArrow, KeyCode.DownArrow);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitGame();
        }
    }

    void MovePaddle(GameObject paddle, KeyCode upKey, KeyCode downKey)
    {
        // Move the paddle based on keyboard input
        bool discreteMovements = paddle.GetComponent<Paddle>().discreteMovements;
        bool upKeyPressed = discreteMovements ? Input.GetKeyDown(upKey) : Input.GetKey(upKey);
        bool downKeyPressed = discreteMovements ? Input.GetKeyDown(downKey) : Input.GetKey(downKey);
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
        if(playerScore>= BestOf)
        {
            isPlaying = false;
            OnRoundEnd.Invoke();
        }
    }

    public void UpdatePlayer2Score()
    {
        player2Score++;
        player2ScoreText.text = player2Score.ToString();
        if(player2Score >= BestOf)
        {
            isPlaying = false;
            gameOver = true;
            OnRoundEnd.Invoke();
        }
    }

    public void StartGame()
    {
        OnRoundStart.Invoke();
        if(gameOver)
        {
            playerScore = 0;
            playerScoreText.text = playerScore.ToString();
            player2Score = 0;
            player2ScoreText.text = player2Score.ToString();
            gameOver = false;
        }    
        if (!isPlaying)
        {
            disk.GetComponent<Disk>().Launch();
            isPlaying = true;
        }
        else
        {
            isPlaying = false;
            disk.GetComponent<Disk>().Reset();
        }
    }

    public void ReloadGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    public void ExitGame()
    {
        Application.Quit();
    }
}
