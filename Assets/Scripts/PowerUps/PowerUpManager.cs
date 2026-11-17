using System;
using System.Collections.Generic;
using UnityEngine;
using BrainHockey.Net;

/// <summary>
/// Spawns the 4th BCI target in the middle of the field and applies the power-ups.
/// Runs the logic only on the host (or offline). On a LAN client it just mirrors
/// the host's state (target position, active effects) for display.
///
/// Owners: 1 = left / blue paddle ("Player", host); 2 = right / red paddle ("Player2", client).
/// </summary>
[DefaultExecutionOrder(-40)]
public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance { get; private set; }

    [Serializable]
    public class ActiveEffect
    {
        public PowerUpType type;
        public int owner;
        public float remaining; // seconds; < 0 = until the next goal
    }

    [Header("Scene references")]
    public PongGame game;
    public Disk mainDisk;
    [Tooltip("Left paddle ('Player')")]
    public Paddle leftPaddle;
    [Tooltip("Right paddle ('Player2')")]
    public Paddle rightPaddle;
    public PowerUpTarget target;
    [Tooltip("Optional. Leave empty for single-machine play.")]
    public BrainHockeyNet net;

    [Header("Power-up target (4th BCI class)")]
    public bool powerUpsEnabled = true;
    [Tooltip("X of the centre line (the target appears here).")]
    public float fieldCenterX = 0f;
    [Tooltip("Centre height of the spawn area.")]
    public float spawnCenterY = 0f;
    [Tooltip("Target appears at a random height within +/- this value.")]
    public float spawnHalfHeight = 2.5f;
    [Tooltip("Random wait (min, max) in seconds before the next target appears.")]
    public Vector2 spawnDelay = new Vector2(6f, 14f);
    [Tooltip("How long a target stays before vanishing unclaimed.")]
    public float targetLifetime = 12f;

    [Header("Effects")]
    public float effectDuration = 30f;
    [Tooltip("Ball speed multiplier in the opponent's half (Turbo).")]
    public float fastMultiplier = 1.6f;
    [Tooltip("Ball speed multiplier in your own half (Slow-mo).")]
    public float slowMultiplier = 0.6f;
    public float bigPaddleScale = 1.6f;
    public float smallPaddleScale = 0.6f;
    [Tooltip("Power-ups the target can give (picked at random). Add an entry twice to make it more likely.")]
    public PowerUpType[] pool =
    {
        PowerUpType.FasterInOpponentField,
        PowerUpType.SlowerInOwnField,
        PowerUpType.BigPaddle,
        PowerUpType.ShrinkOpponentPaddle,
        PowerUpType.MultiBall,
    };

    /// <summary>(type, owner) — raised on host and client whenever a power-up is triggered.</summary>
    public event Action<PowerUpType, int> PowerUpActivated;

    public readonly List<ActiveEffect> Effects = new List<ActiveEffect>();

    public bool Authority { get { return net == null || !net.IsClient; } }
    public bool TargetActive { get { return target != null && target.IsActive; } }

    private float nextSpawnAt;
    private float expireAt;
    private int targetCounter;
    private int nextBallId = 1;
    private readonly List<Disk> extraBalls = new List<Disk>();

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (mainDisk != null) mainDisk.netId = 0;
        ScheduleNextSpawn();
    }

    void Update()
    {
        if (!Authority) return; // the client gets everything from snapshots

        extraBalls.RemoveAll(d => d == null);

        bool peerOk = net == null || !net.IsNetworked || net.HasPeer;
        bool running = powerUpsEnabled && game != null && game.IsPlaying && peerOk;

        if (running)
        {
            float dt = Time.deltaTime;
            for (int i = Effects.Count - 1; i >= 0; i--)
            {
                var e = Effects[i];
                if (e.type == PowerUpType.MultiBall) continue;
                e.remaining -= dt;
                if (e.remaining <= 0f) Effects.RemoveAt(i);
            }

            if (!TargetActive && Time.time >= nextSpawnAt) SpawnTarget();
            else if (TargetActive && Time.time >= expireAt) HideTarget();
        }
        else if (TargetActive)
        {
            HideTarget();
        }

        if (extraBalls.Count == 0) Effects.RemoveAll(e => e.type == PowerUpType.MultiBall);
        ApplyPaddleSizes();
    }

    // ------------------------------------------------------------------ target

    private void ScheduleNextSpawn()
    {
        nextSpawnAt = Time.time + UnityEngine.Random.Range(spawnDelay.x, spawnDelay.y);
    }

    private void SpawnTarget()
    {
        if (target == null) return;
        targetCounter++;
        var pos = new Vector2(fieldCenterX, spawnCenterY + UnityEngine.Random.Range(-spawnHalfHeight, spawnHalfHeight));
        target.Show(targetCounter, pos);
        expireAt = Time.time + targetLifetime;
    }

    private void HideTarget()
    {
        if (target != null) target.Hide();
        ScheduleNextSpawn();
    }

    /// <summary>
    /// A player on THIS machine selected the 4th target (BCI class 4 or test key).
    /// viewSide: 1 = left paddle, 2 = right paddle.
    /// </summary>
    public void RequestClaim(int viewSide)
    {
        if (!TargetActive) return;

        if (Authority)
        {
            TryClaim(viewSide, target.CurrentId);
        }
        else if (net != null && viewSide == net.LocalOwner)
        {
            net.SendClaim(target.CurrentId); // host decides who was first
        }
    }

    /// <summary>Host only: first valid claim for the current target wins.</summary>
    public bool TryClaim(int owner, int targetId)
    {
        if (!Authority || !TargetActive || target.CurrentId != targetId) return false;
        if (pool == null || pool.Length == 0) return false;

        HideTarget();
        var type = pool[UnityEngine.Random.Range(0, pool.Length)];
        Activate(type, owner);
        return true;
    }

    private void Activate(PowerUpType type, int owner)
    {
        float duration = effectDuration;
        if (type == PowerUpType.MultiBall)
        {
            SpawnExtraBall();
            duration = -1f;
        }

        var existing = Effects.Find(e => e.type == type && e.owner == owner);
        if (existing != null) existing.remaining = duration; // re-trigger refreshes the timer
        else Effects.Add(new ActiveEffect { type = type, owner = owner, remaining = duration });

        ApplyPaddleSizes();
        if (PowerUpActivated != null) PowerUpActivated(type, owner);
    }

    /// <summary>Client side: the host told us a power-up fired (for HUD announcements).</summary>
    public void RaiseActivatedFromNetwork(PowerUpType type, int owner)
    {
        if (PowerUpActivated != null) PowerUpActivated(type, owner);
    }

    // ------------------------------------------------------------------ effects

    /// <summary>Speed multiplier for a ball at world x.</summary>
    public float GetSpeedMultiplier(float x)
    {
        int half = x < fieldCenterX ? 1 : 2; // whose side the ball is on
        float m = 1f;
        foreach (var e in Effects)
        {
            if (e.type == PowerUpType.FasterInOpponentField && e.owner != half) m *= fastMultiplier;
            else if (e.type == PowerUpType.SlowerInOwnField && e.owner == half) m *= slowMultiplier;
        }
        return m;
    }

    public float GetPaddleScale(int owner)
    {
        float m = 1f;
        foreach (var e in Effects)
        {
            if (e.type == PowerUpType.BigPaddle && e.owner == owner) m *= bigPaddleScale;
            else if (e.type == PowerUpType.ShrinkOpponentPaddle && e.owner != owner) m *= smallPaddleScale;
        }
        return m;
    }

    private void ApplyPaddleSizes()
    {
        if (leftPaddle != null) leftPaddle.SetSizeMultiplier(GetPaddleScale(1));
        if (rightPaddle != null) rightPaddle.SetSizeMultiplier(GetPaddleScale(2));
    }

    private void SpawnExtraBall()
    {
        if (mainDisk == null) return;

        // Duplicate whichever ball is currently in play (the main one if it's moving).
        Disk source = mainDisk;
        if (source.Velocity.sqrMagnitude < 0.01f)
        {
            foreach (var d in extraBalls)
                if (d != null && d.Velocity.sqrMagnitude > 0.01f) { source = d; break; }
        }

        Vector3 v = source.Velocity;
        if (v.sqrMagnitude < 0.01f) v = new Vector3(source.speed, source.speed, 0f);

        Disk clone = Instantiate(mainDisk, source.transform.position, source.transform.rotation, mainDisk.transform.parent);
        clone.name = mainDisk.name + " (extra " + nextBallId + ")";
        clone.MakeClone(nextBallId++, true, new Vector3(v.x, -v.y, v.z)); // split vertically
        extraBalls.Add(clone);
    }

    /// <summary>Host: called by Disk when any ball enters a goal (after the score event).</summary>
    public void HandleGoal()
    {
        ClearExtraBalls();
        if (mainDisk != null) mainDisk.Reset();
    }

    public void ClearExtraBalls()
    {
        foreach (var d in extraBalls)
            if (d != null) Destroy(d.gameObject);
        extraBalls.Clear();
        Effects.RemoveAll(e => e.type == PowerUpType.MultiBall);
    }

    /// <summary>Removes every effect, extra ball and the target (new match, disconnect...).</summary>
    public void ClearAll()
    {
        ClearExtraBalls();
        Effects.Clear();
        if (TargetActive) target.Hide();
        ScheduleNextSpawn();
        if (Authority) ApplyPaddleSizes();
    }

    // ------------------------------------------------------------------ networking

    public void WriteState(Snapshot s)
    {
        s.targetActive = TargetActive;
        if (TargetActive)
        {
            s.targetId = target.CurrentId;
            s.targetPos = target.transform.position;
        }
        foreach (var e in Effects)
            s.effects.Add(new EffectState { type = e.type, owner = e.owner, remaining = e.remaining });
    }

    public void ReadState(Snapshot s)
    {
        if (target != null)
        {
            if (s.targetActive) target.Show(s.targetId, s.targetPos);
            else if (target.IsActive) target.Hide();
        }

        Effects.Clear();
        foreach (var e in s.effects)
            Effects.Add(new ActiveEffect { type = e.type, owner = e.owner, remaining = e.remaining });
    }

    // ------------------------------------------------------------------ helpers for UI

    public int LocalOwner { get { return net != null ? net.LocalOwner : 1; } }

    public static string DisplayName(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.FasterInOpponentField: return "Turbo ball in opponent's half";
            case PowerUpType.SlowerInOwnField: return "Slow-mo ball in own half";
            case PowerUpType.BigPaddle: return "Big paddle";
            case PowerUpType.ShrinkOpponentPaddle: return "Opponent paddle shrunk";
            case PowerUpType.MultiBall: return "Multi-ball";
        }
        return type.ToString();
    }
    
    public void RequestLocalClaim() { RequestClaim(LocalOwner); }
}
