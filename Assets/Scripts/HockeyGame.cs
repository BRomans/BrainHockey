using System;
using System.Collections.Generic;
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

    [SerializeField]
    [Tooltip("The UI element that will display the game menu")]
    private GameObject gameMenu;

    public bool discreteMovements = false;

    [Header("Multiplayer / power-ups (optional)")]
    [SerializeField]
    [Tooltip("Leave empty for the classic single-machine game.")]
    private BrainHockeyNet net;

    [SerializeField]
    private PowerUpManager powerUps;

    [Tooltip("Test key: the player at this machine grabs the power-up target (same as BCI class 4).")]
    public KeyCode powerUpTestKey = KeyCode.E;

    [Tooltip("Test key for the right player when playing on one machine.")]
    public KeyCode player2PowerUpTestKey = KeyCode.RightShift;

    /// <summary>C# events used by the network layer.</summary>
    public event Action RoundStarted;
    public event Action RoundEnded;

    private bool isPlaying = false;

    private bool gameOver = false;

    private int playerScore = 0;

    private int player2Score = 0;

    private readonly List<Disk> diskBuffer = new List<Disk>();

    public bool IsPlaying { get { return isPlaying; } }
    public bool IsGameOver { get { return gameOver; } }
    public int PlayerScore { get { return playerScore; } }
    public int Player2Score { get { return player2Score; } }

    private bool Networked { get { return net != null && net.IsNetworked; } }
    private bool IsClient { get { return net != null && net.IsClient; } }
    private GameObject LocalPaddleObject { get { return IsClient ? player2Paddle : playerPaddle; } }

    void Start()
    {
        OnRoundStart.Invoke();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (IsClient) net.SendStartRequest();          // host decides
            else if (!Networked || net.HasPeer) StartGame(); // networked host waits for a player
        }

        if (isPlaying && !IsClient)
        {
            // Move every disk (main + multi-ball). Copy first: a goal can destroy disks.
            diskBuffer.Clear();
            diskBuffer.AddRange(Disk.Active);
            foreach (var d in diskBuffer)
                if (d != null && d.authoritative) d.MoveDisk();
        }

        if (Networked)
        {
            // Over LAN you only control your own paddle: left/blue on the host, right/red on the client.
            GameObject mine = LocalPaddleObject;
            MovePaddle(mine, KeyCode.W, KeyCode.S);
            MovePaddle(mine, KeyCode.UpArrow, KeyCode.DownArrow);
        }
        else
        {
            MovePaddle(playerPaddle, KeyCode.W, KeyCode.S);
            MovePaddle(player2Paddle, KeyCode.UpArrow, KeyCode.DownArrow);
        }

        if (Input.GetKeyDown(powerUpTestKey))
            LocalPaddleObject.GetComponent<Paddle>().SelectTarget(Paddle.PowerUpTargetIndex);
        if (!Networked && Input.GetKeyDown(player2PowerUpTestKey))
            player2Paddle.GetComponent<Paddle>().SelectTarget(Paddle.PowerUpTargetIndex);

        if (Input.GetKeyDown(KeyCode.Tab) && gameMenu != null && !IsClient)
        {
            bool show = !gameMenu.activeSelf;
            SetMenuVisible(show);
            if (net != null) net.SendMenuVisible(show);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitGame();
        }
    }
    
    public bool IsMenuVisible { get { return gameMenu != null && gameMenu.activeSelf; } }

    public void SetMenuVisible(bool visible)
    {
        if (gameMenu != null) gameMenu.SetActive(visible);
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
            gameOver = true; // was missing: P1 winning never reset the scores
            EndRound();
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
            EndRound();
        }
    }

    private void EndRound()
    {
        if (powerUps != null) powerUps.ClearAll();
        OnRoundEnd.Invoke();
        if (RoundEnded != null) RoundEnded();
    }

    public void StartGame()
    {
        OnRoundStart.Invoke();
        if (RoundStarted != null) RoundStarted();
        if(gameOver)
        {
            playerScore = 0;
            player2Score = 0;
            UpdateScoreTexts();
            gameOver = false;
            if (powerUps != null) powerUps.ClearAll();
        }
        if (!isPlaying)
        {
            disk.GetComponent<Disk>().Launch();
            isPlaying = true;
        }
        else
        {
            isPlaying = false;
            if (powerUps != null) powerUps.ClearExtraBalls();
            disk.GetComponent<Disk>().Reset();
        }
    }

    /// <summary>Back to 0-0, nothing moving (new LAN player joined / left).</summary>
    public void ResetMatch()
    {
        isPlaying = false;
        gameOver = false;
        playerScore = 0;
        player2Score = 0;
        UpdateScoreTexts();
        if (powerUps != null) powerUps.ClearAll();
        var d = disk.GetComponent<Disk>();
        if (d.authoritative) d.Reset();
    }

    /// <summary>LAN client: state received from the host (left = blue, right = red).</summary>
    public void ApplyNetworkState(int leftScore, int rightScore, bool playing, bool over)
    {
        if (leftScore != playerScore || rightScore != player2Score)
        {
            playerScore = leftScore;
            player2Score = rightScore;
            UpdateScoreTexts();
        }
        isPlaying = playing;
        gameOver = over;
    }

    /// <summary>LAN client: mirror the host's round start / end UnityEvents (UI, BCI task...).</summary>
    public void ApplyRoundEventFromNetwork(bool started)
    {
        if (started) OnRoundStart.Invoke();
        else OnRoundEnd.Invoke();
    }

    private void UpdateScoreTexts()
    {
        if (playerScoreText != null) playerScoreText.text = playerScore.ToString();
        if (player2ScoreText != null) player2ScoreText.text = player2Score.ToString();
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
