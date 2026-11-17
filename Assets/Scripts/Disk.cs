using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Disk : MonoBehaviour
{
    /// <summary>Every disk currently in the scene (main + multi-ball copies).</summary>
    public static readonly List<Disk> Active = new List<Disk>();

    public UnityEvent OnP2Scored;

    public UnityEvent OnP1Scored;

    public float speed = 5f;
    public Vector3 startPosition;

    [Header("Multiplayer")]
    [Tooltip("True offline / on the host. On a LAN client this is switched off and the disk follows the host.")]
    public bool authoritative = true;

    [HideInInspector] public bool isClone;
    [HideInInspector] public int netId;

    private Vector3 diskVelocity; // Velocity of the ball

    private TaskController _taskController;

    // replica (client) state
    private bool hasNetState;
    private Vector2 netPos;
    private Vector2 netVel;
    private float netTime;

    public Vector3 Velocity { get { return diskVelocity; } }
    /// <summary>Velocity including power-up speed modifiers.</summary>
    public Vector3 EffectiveVelocity { get { return diskVelocity * SpeedMultiplier(); } }

    void OnEnable() { Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    void Start()
    {
        if (!isClone) startPosition = transform.position;
        var task = GameObject.FindGameObjectWithTag("Task");
        if (task != null) _taskController = task.GetComponent<TaskController>();
    }

    /// <summary>Turns a freshly instantiated copy into a multi-ball disk.</summary>
    public void MakeClone(int id, bool isAuthoritative, Vector3 velocity)
    {
        isClone = true;
        netId = id;
        authoritative = isAuthoritative;
        diskVelocity = velocity;
        hasNetState = false;
    }

    public void Reset()
    {
        diskVelocity = Vector3.zero;
        transform.position = startPosition;
        CancelInvoke("Launch");
        if (authoritative && _taskController != null && _taskController.IsTaskRunning())
            Invoke("Launch", 0.5f);
    }

    public void Launch()
    {
        if (!authoritative) return;
        float speedX = Random.Range(0, 5) == 0 ? -speed : speed;
        float speedY = Random.Range(0, 5) == 0 ? -speed : speed;
        // Set initial velocity for the ball
        diskVelocity = new Vector3(speedX, speedY, 0f);
    }

    public void MoveDisk()
    {
        if (!authoritative) return;
        // Move the ball based on its velocity (power-ups may speed it up / slow it down per half)
        transform.Translate(diskVelocity * SpeedMultiplier() * Time.deltaTime);
    }

    private float SpeedMultiplier()
    {
        var pm = PowerUpManager.Instance;
        return pm != null ? pm.GetSpeedMultiplier(transform.position.x) : 1f;
    }

    // ---------------------------------------------------------------- client replica

    /// <summary>Client only: latest position/velocity from the host.</summary>
    public void SetNetworkState(Vector2 pos, Vector2 vel)
    {
        netPos = pos;
        netVel = vel;
        netTime = Time.time;
        if (!hasNetState)
        {
            hasNetState = true;
            transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        }
    }

    void Update()
    {
        if (authoritative || !hasNetState) return;

        // Extrapolate from the last snapshot and smooth towards it
        float dt = Mathf.Min(Time.time - netTime, 0.25f);
        var predicted = new Vector3(netPos.x + netVel.x * dt, netPos.y + netVel.y * dt, transform.position.z);
        if ((predicted - transform.position).sqrMagnitude > 2.25f)
            transform.position = predicted; // teleport (reset after a goal)
        else
            transform.position = Vector3.Lerp(transform.position, predicted, 1f - Mathf.Exp(-20f * Time.deltaTime));
    }

    // ---------------------------------------------------------------- collisions

    private static int LocalSide
    {
        get { return BrainHockeyNet.Instance != null ? BrainHockeyNet.Instance.LocalOwner : 1; }
    }

    /// <summary>Which player a trigger belongs to: paddles by their side, zones by a PlayerSide component (default left).</summary>
    private static int BciSideOf(Collider2D c)
    {
        var paddle = c.GetComponent<Paddle>();
        if (paddle != null) return paddle.side;
        var zone = c.GetComponent<PlayerSide>();
        return zone != null ? zone.side : 1;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // --- BCI task bookkeeping: only the real ball, and only the zones/paddle of the side
        //     played on THIS machine (left/blue on the host, right/red on the client) ---
        if (!isClone && _taskController != null && BciSideOf(collision) == LocalSide)
        {
            if (collision.gameObject.CompareTag("TrialCounter"))
            {
                if (!_taskController.IsTaskRunning())
                    _taskController.StartTaskTimer();
            }
            if (collision.gameObject.CompareTag("EndTask"))
            {
                if (collision.name == "EndTask_1") _taskController.BallMissed(1);
                if (collision.name == "EndTask_2") _taskController.BallMissed(2);
                if (collision.name == "EndTask_3") _taskController.BallMissed(3);
            }
            if (collision.gameObject.CompareTag("Paddle"))
            {
                Paddle paddle = collision.GetComponent<Paddle>();
                if (paddle != null) _taskController.BallHit(paddle.currentAnchor);
            }
        }

        // --- Physics & scoring: only the host / offline game decides ---
        if (!authoritative) return;

        if (collision.gameObject.CompareTag("Paddle"))
        {
            // Bounce AWAY from the paddle (robust with multi-ball and resized paddles,
            // where a plain *= -1 could flip twice)
            float dir = transform.position.x < collision.bounds.center.x ? -1f : 1f;
            diskVelocity.x = Mathf.Abs(diskVelocity.x) * dir;
        }
        if (collision.gameObject.CompareTag("Wall"))
        {
            float dir = transform.position.y < collision.bounds.center.y ? -1f : 1f;
            diskVelocity.y = Mathf.Abs(diskVelocity.y) * dir;
        }
        if (collision.gameObject.CompareTag("Goal"))
        {
            if (collision.gameObject.name == "PlayerGoal")
            {
                OnP2Scored.Invoke();
            }
            else
            {
                OnP1Scored.Invoke();
            }

            // Any goal ends multi-ball and resets the main disk
            var pm = PowerUpManager.Instance;
            if (pm != null) pm.HandleGoal();
            else Reset();
        }
    }
}
