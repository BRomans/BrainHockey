using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using BrainHockey.Net;

/// <summary>
/// Two-player LAN multiplayer for BrainHockey.
///
/// * Auto mode: listens for a game on the LAN for a couple of seconds; joins it if found,
///   otherwise hosts one and broadcasts it. Start the game on both PCs and they pair up.
/// * The HOST simulates everything (ball, goals, power-ups). The CLIENT only sends its
///   paddle position / power-up claims and displays the host's snapshots.
/// * Fixed sides, same picture on both screens: the HOST is BLUE on the LEFT ("Player"),
///   the CLIENT is RED on the RIGHT ("Player2"). LocalSideChanged tells the BCI setup
///   (BciSideSwitcher) which side the person at this machine plays.
/// </summary>
[DefaultExecutionOrder(-50)]
public class BrainHockeyNet : MonoBehaviour
{
    public static BrainHockeyNet Instance { get; private set; }

    public enum Mode { Offline, Auto, Host, Client }
    public enum State { Offline, Searching, Connecting, Hosting, Connected }

    [Header("Mode")]
    [Tooltip("Auto = join a game on the LAN if one exists, otherwise host one. " +
             "Can be overridden from the command line with -host, -client, -offline or -connect <ip>.")]
    public Mode mode = Mode.Auto;
    [Tooltip("Optional: connect straight to this IP instead of using broadcast discovery (for networks that block broadcast).")]
    public string manualHostIp = "";
    [Tooltip("Auto mode: listen this long for an existing game before hosting (a random 0-1 s is added).")]
    public float searchTime = 2.5f;
    public int discoveryPort = NetProtocol.DefaultDiscoveryPort;
    public int gamePort = NetProtocol.DefaultGamePort;
    [Tooltip("Snapshots / input messages per second.")]
    public float sendRate = 30f;
    [Tooltip("Shown to the other player. Empty = computer name.")]
    public string playerName = "";

    [Header("Scene references")]
    public PongGame game;
    public Disk mainDisk;
    [Tooltip("LEFT / BLUE paddle ('Player') — played by the host (and by you when offline).")]
    public Paddle leftPaddle;
    [Tooltip("RIGHT / RED paddle ('Player2') — played by the client that joins.")]
    public Paddle rightPaddle;
    public PowerUpManager powerUps;

    /// <summary>Fired with 1 (left/blue) or 2 (right/red) whenever the side played on this machine changes.</summary>
    public event Action<int> LocalSideChanged;

    [Header("UI")]
    public bool showStatus = true;

    public State CurrentState { get; private set; }
    public bool IsNetworked { get { return mode != Mode.Offline; } }
    public bool IsClient { get { return isClientRole && CurrentState == State.Connected; } }
    public bool IsHost { get { return CurrentState == State.Hosting; } }
    public bool HasPeer { get { return conn != null && conn.Connected; } }
    /// <summary>Side played on this machine: 1 = left/blue (host or offline), 2 = right/red (client).</summary>
    public int LocalOwner { get { return (IsClient || mode == Mode.Client) ? 2 : 1; } }
    /// <summary>The paddle the BCI on this machine controls. Call LocalPaddle.SelectTarget(n) from your classifier.</summary>
    public Paddle LocalPaddle { get { return IsClient ? rightPaddle : leftPaddle; } }
    public Paddle RemotePaddle { get { return IsClient ? leftPaddle : rightPaddle; } }
    public string RemoteName { get { return remoteName; } }

    private LanDiscovery discovery;
    private TcpListener listener;
    private Task<TcpClient> acceptTask;
    private TcpClient connectingClient;
    private Task connectTask;
    private float connectDeadline;
    private LanConnection conn;
    private bool isClientRole;
    private bool peerWasConnected;
    private uint sessionId;
    private float searchDeadline;
    private float nextBroadcast;
    private float nextSend;
    private uint tick;
    private string remoteName = "";
    private string connectingTo = "";
    private string localIp = "";

    private readonly Snapshot outSnapshot = new Snapshot();
    private readonly Dictionary<int, Disk> replicaBalls = new Dictionary<int, Disk>();
    private readonly List<int> staleIds = new List<int>();

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        Instance = this;
        sessionId = (uint)UnityEngine.Random.Range(1, int.MaxValue);
        if (string.IsNullOrEmpty(playerName)) playerName = SystemInfo.deviceName;
        ParseCommandLine();
        CurrentState = State.Offline;
    }

    void Start()
    {
        RaiseLocalSide(); // offline / before connecting: left
        if (!IsNetworked) return;

        // An AI script would fight the network for a paddle
        var cpuL = leftPaddle as CPU;
        if (cpuL != null) cpuL.enabled = false;
        var cpuR = rightPaddle as CPU;
        if (cpuR != null) cpuR.enabled = false;

        if (game != null)
        {
            game.RoundStarted += OnLocalRoundStarted;
            game.RoundEnded += OnLocalRoundEnded;
        }
        if (powerUps != null) powerUps.PowerUpActivated += OnLocalPowerUpActivated;

        localIp = LanDiscovery.GetLocalIPv4();
        discovery = new LanDiscovery();
        try { discovery.StartListening(discoveryPort); }
        catch (Exception e) { Debug.LogWarning("[BrainHockeyNet] Discovery listen failed: " + e.Message); }

        if (mode == Mode.Host) StartHosting();
        else if (!string.IsNullOrEmpty(manualHostIp)) ConnectToManualIp();
        else BeginSearch();
    }

    void OnDestroy()
    {
        if (game != null)
        {
            game.RoundStarted -= OnLocalRoundStarted;
            game.RoundEnded -= OnLocalRoundEnded;
        }
        if (powerUps != null) powerUps.PowerUpActivated -= OnLocalPowerUpActivated;
        Shutdown();
        if (Instance == this) Instance = null;
    }

    void OnApplicationQuit()
    {
        Shutdown();
    }

    private void Shutdown()
    {
        if (conn != null) { conn.Dispose(); conn = null; }
        StopHosting();
        if (connectingClient != null) { connectingClient.Close(); connectingClient = null; }
        if (discovery != null) { discovery.Dispose(); discovery = null; }
    }

    private void ParseCommandLine()
    {
        string[] args;
        try { args = Environment.GetCommandLineArgs(); } catch (Exception) { return; }
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            if (a == "-host") mode = Mode.Host;
            else if (a == "-client" || a == "-join") mode = Mode.Client;
            else if (a == "-offline") mode = Mode.Offline;
            else if (a == "-auto") mode = Mode.Auto;
            else if (a == "-connect" && i + 1 < args.Length) { mode = Mode.Client; manualHostIp = args[++i]; }
        }
    }

    // ------------------------------------------------------------------ main loop

    void Update()
    {
        if (!IsNetworked) return;

        switch (CurrentState)
        {
            case State.Searching: UpdateSearching(); break;
            case State.Connecting: UpdateConnecting(); break;
            case State.Hosting: UpdateHosting(); break;
            case State.Connected: UpdateClient(); break;
        }
    }

    // ------------------------------------------------------------------ discovery / joining

    private void BeginSearch()
    {
        CurrentState = State.Searching;
        searchDeadline = Time.unscaledTime + searchTime + UnityEngine.Random.Range(0f, 1f);
        if (discovery != null) discovery.ClearFound();
    }

    private void UpdateSearching()
    {
        LanDiscovery.HostInfo host;
        while (discovery != null && discovery.TryGetHost(out host))
        {
            if (host.sessionId == sessionId) continue; // our own echo
            ConnectTo(host.address, host.port, host.name);
            return;
        }
        if (mode == Mode.Auto && Time.unscaledTime >= searchDeadline) StartHosting();
    }

    private void ConnectToManualIp()
    {
        IPAddress address;
        if (!IPAddress.TryParse(manualHostIp.Trim(), out address))
        {
            Debug.LogError("[BrainHockeyNet] Invalid manualHostIp: " + manualHostIp);
            BeginSearch();
            return;
        }
        ConnectTo(address, gamePort, manualHostIp);
    }

    private void ConnectTo(IPAddress address, int port, string name)
    {
        connectingTo = name + " (" + address + ")";
        connectingClient = new TcpClient(AddressFamily.InterNetwork);
        connectTask = connectingClient.ConnectAsync(address, port);
        connectDeadline = Time.unscaledTime + 3f;
        CurrentState = State.Connecting;
    }

    private void UpdateConnecting()
    {
        if (connectTask.IsCompleted)
        {
            bool ok = connectTask.Status == TaskStatus.RanToCompletion && connectingClient.Connected;
            if (connectTask.IsFaulted) { var ignored = connectTask.Exception; } // observe the exception

            if (ok)
            {
                conn = new LanConnection(connectingClient);
                connectingClient = null;
                EnterClientRole();
                conn.Send(NetProtocol.Build(MsgType.Hello, w => { w.Write(NetProtocol.Version); w.Write(playerName); }));
            }
            else
            {
                FailConnect();
            }
        }
        else if (Time.unscaledTime > connectDeadline)
        {
            FailConnect();
        }
    }

    private void FailConnect()
    {
        Debug.LogWarning("[BrainHockeyNet] Could not connect to " + connectingTo);
        if (connectingClient != null) { connectingClient.Close(); connectingClient = null; }
        if (!string.IsNullOrEmpty(manualHostIp)) ConnectToManualIp(); // keep retrying the given IP
        else BeginSearch();
    }

    // ------------------------------------------------------------------ hosting

    private void StartHosting()
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, gamePort);
            listener.Start();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BrainHockeyNet] Cannot host on port " + gamePort + ": " + e.Message);
            listener = null;
            BeginSearch(); // probably another instance on this PC is hosting — join it instead
            return;
        }
        isClientRole = false;
        CurrentState = State.Hosting;
        acceptTask = listener.AcceptTcpClientAsync();
        nextBroadcast = 0f;
        Debug.Log("[BrainHockeyNet] Hosting on " + localIp + ":" + gamePort);
    }

    private void StopHosting()
    {
        if (listener != null)
        {
            try { listener.Stop(); } catch (Exception) { }
            listener = null;
        }
        acceptTask = null;
    }

    private void UpdateHosting()
    {
        // Accept a client (only one at a time)
        if (acceptTask != null && acceptTask.IsCompleted)
        {
            if (acceptTask.Status == TaskStatus.RanToCompletion)
            {
                TcpClient incoming = acceptTask.Result;
                if (!HasPeer)
                {
                    if (conn != null) conn.Dispose();
                    conn = new LanConnection(incoming);
                    OnPeerJoinedAsHost();
                }
                else
                {
                    incoming.Close(); // game is full
                }
            }
            else if (acceptTask.IsFaulted) { var ignored = acceptTask.Exception; }

            acceptTask = listener != null ? listener.AcceptTcpClientAsync() : null;
        }

        if (!HasPeer)
        {
            if (peerWasConnected) OnPeerLost();

            if (Time.unscaledTime >= nextBroadcast && discovery != null)
            {
                discovery.Broadcast(discoveryPort, gamePort, sessionId, playerName);
                nextBroadcast = Time.unscaledTime + 1f;
            }

            // Tie-break: both PCs started hosting at the same moment -> the higher session id joins the lower one.
            LanDiscovery.HostInfo other;
            while (discovery != null && discovery.TryGetHost(out other))
            {
                if (mode == Mode.Auto && other.sessionId != sessionId && other.sessionId < sessionId)
                {
                    StopHosting();
                    ConnectTo(other.address, other.port, other.name);
                    return;
                }
            }
            return;
        }

        if (discovery != null) discovery.ClearFound();

        byte[] msg;
        while (conn != null && conn.TryDequeue(out msg)) HandleClientMessage(msg);

        if (Time.unscaledTime >= nextSend)
        {
            nextSend = Time.unscaledTime + 1f / Mathf.Max(1f, sendRate);
            SendSnapshot();
        }
    }

    private void OnPeerJoinedAsHost()
    {
        peerWasConnected = true;
        remoteName = conn.RemoteAddress;
        Debug.Log("[BrainHockeyNet] Player joined from " + conn.RemoteAddress);
        if (game != null) game.ResetMatch();
        conn.Send(NetProtocol.Build(MsgType.Welcome, w => w.Write(playerName)));
    }

    private void HandleClientMessage(byte[] data)
    {
        using (var r = new BinaryReader(new MemoryStream(data)))
        {
            var type = (MsgType)r.ReadByte();
            switch (type)
            {
                case MsgType.Hello:
                    int version = r.ReadInt32();
                    remoteName = r.ReadString();
                    if (version != NetProtocol.Version)
                        Debug.LogWarning("[BrainHockeyNet] Client uses protocol v" + version + ", host v" + NetProtocol.Version);
                    break;

                case MsgType.ClientInput:
                    float y = r.ReadSingle();
                    int anchor = r.ReadInt32();
                    if (rightPaddle != null)
                    {
                        rightPaddle.SetNetworkY(y);
                        rightPaddle.currentAnchor = anchor;
                    }
                    break;

                case MsgType.ClaimPowerUp:
                    int targetId = r.ReadInt32();
                    if (powerUps != null) powerUps.TryClaim(2, targetId);
                    break;

                case MsgType.StartRequest:
                    if (game != null) game.StartGame();
                    break;
            }
        }
    }

    private void SendSnapshot()
    {
        var s = outSnapshot;
        s.Clear();
        s.tick = ++tick;
        if (game != null)
        {
            s.isPlaying = game.IsPlaying;
            s.gameOver = game.IsGameOver;
            s.leftScore = game.PlayerScore;
            s.rightScore = game.Player2Score;
        }
        if (leftPaddle != null)
        {
            s.leftPaddleY = leftPaddle.transform.position.y;
            s.leftPaddleScale = leftPaddle.SizeMultiplier;
        }
        if (rightPaddle != null) s.rightPaddleScale = rightPaddle.SizeMultiplier;
        if (powerUps != null) powerUps.WriteState(s);

        foreach (var d in Disk.Active)
        {
            if (d == null || !d.authoritative) continue;
            s.balls.Add(new BallState
            {
                id = d.netId,
                pos = d.transform.position,
                vel = d.transform.TransformDirection(d.EffectiveVelocity),
            });
        }

        conn.Send(NetProtocol.Build(MsgType.Snapshot, w => s.Write(w)));
    }

    // Host-side game events forwarded to the client
    private void OnLocalRoundStarted()
    {
        if (IsHost && HasPeer) conn.Send(NetProtocol.Build(MsgType.RoundEvent, w => w.Write(true)));
    }

    private void OnLocalRoundEnded()
    {
        if (IsHost && HasPeer) conn.Send(NetProtocol.Build(MsgType.RoundEvent, w => w.Write(false)));
    }

    private void OnLocalPowerUpActivated(PowerUpType type, int owner)
    {
        if (IsHost && HasPeer)
            conn.Send(NetProtocol.Build(MsgType.PowerUpEvent, w => { w.Write((byte)type); w.Write((byte)owner); }));
    }

    // ------------------------------------------------------------------ client

    private void EnterClientRole()
    {
        // Clean up while still authoritative, then hand authority to the host.
        if (game != null) game.ResetMatch();
        isClientRole = true;
        peerWasConnected = true;
        CurrentState = State.Connected;
        if (mainDisk != null) mainDisk.authoritative = false;
        RaiseLocalSide();
        Debug.Log("[BrainHockeyNet] Connected to host " + connectingTo);
    }

    private void UpdateClient()
    {
        if (!HasPeer)
        {
            OnPeerLost();
            if (!string.IsNullOrEmpty(manualHostIp)) ConnectToManualIp();
            else BeginSearch();
            return;
        }

        byte[] msg;
        while (conn != null && conn.TryDequeue(out msg)) HandleHostMessage(msg);
        if (discovery != null) discovery.ClearFound();

        if (Time.unscaledTime >= nextSend && rightPaddle != null)
        {
            nextSend = Time.unscaledTime + 1f / Mathf.Max(1f, sendRate);
            float y = rightPaddle.transform.position.y;
            int anchor = rightPaddle.currentAnchor;
            conn.Send(NetProtocol.Build(MsgType.ClientInput, w => { w.Write(y); w.Write(anchor); }));
        }
    }

    private void HandleHostMessage(byte[] data)
    {
        using (var r = new BinaryReader(new MemoryStream(data)))
        {
            var type = (MsgType)r.ReadByte();
            switch (type)
            {
                case MsgType.Welcome:
                    remoteName = r.ReadString();
                    break;

                case MsgType.Snapshot:
                    ApplySnapshot(Snapshot.Read(r));
                    break;

                case MsgType.PowerUpEvent:
                    var puType = (PowerUpType)r.ReadByte();
                    int owner = r.ReadByte();
                    if (powerUps != null) powerUps.RaiseActivatedFromNetwork(puType, owner);
                    break;

                case MsgType.RoundEvent:
                    bool started = r.ReadBoolean();
                    if (game != null) game.ApplyRoundEventFromNetwork(started);
                    break;
            }
        }
    }

    private void ApplySnapshot(Snapshot s)
    {
        // Same coordinates on both machines: the host's paddle is the left one, ours the right one.
        if (game != null) game.ApplyNetworkState(s.leftScore, s.rightScore, s.isPlaying, s.gameOver);
        if (leftPaddle != null)
        {
            leftPaddle.SetNetworkY(s.leftPaddleY);
            leftPaddle.SetSizeMultiplier(s.leftPaddleScale);
        }
        if (rightPaddle != null) rightPaddle.SetSizeMultiplier(s.rightPaddleScale);
        if (powerUps != null) powerUps.ReadState(s);
        SyncBalls(s.balls);
    }

    private void SyncBalls(List<BallState> balls)
    {
        staleIds.Clear();
        staleIds.AddRange(replicaBalls.Keys);

        foreach (var b in balls)
        {
            Disk d;
            if (b.id == 0)
            {
                d = mainDisk;
            }
            else if (!replicaBalls.TryGetValue(b.id, out d) || d == null)
            {
                d = Instantiate(mainDisk, mainDisk.transform.position, mainDisk.transform.rotation, mainDisk.transform.parent);
                d.name = mainDisk.name + " (extra " + b.id + ")";
                d.MakeClone(b.id, false, Vector3.zero);
                replicaBalls[b.id] = d;
            }
            if (d == null) continue;
            d.SetNetworkState(b.pos, b.vel);
            staleIds.Remove(b.id);
        }

        foreach (int id in staleIds)
        {
            Disk d;
            if (replicaBalls.TryGetValue(id, out d) && d != null) Destroy(d.gameObject);
            replicaBalls.Remove(id);
        }
    }

    private void ClearReplicaBalls()
    {
        foreach (var d in replicaBalls.Values) if (d != null) Destroy(d.gameObject);
        replicaBalls.Clear();
    }

    /// <summary>Called from PowerUpManager when the local (client) player selects the power-up target.</summary>
    public void SendClaim(int targetId)
    {
        if (IsClient && HasPeer) conn.Send(NetProtocol.Build(MsgType.ClaimPowerUp, w => w.Write(targetId)));
    }

    /// <summary>Called from PongGame when SPACE is pressed on the client.</summary>
    public void SendStartRequest()
    {
        if (IsClient && HasPeer) conn.Send(NetProtocol.Build(MsgType.StartRequest));
    }

    // ------------------------------------------------------------------ disconnects

    private void OnPeerLost()
    {
        Debug.LogWarning("[BrainHockeyNet] Other player disconnected");
        peerWasConnected = false;
        if (conn != null) { conn.Dispose(); conn = null; }
        remoteName = "";

        if (isClientRole)
        {
            isClientRole = false;
            ClearReplicaBalls();
            if (mainDisk != null) mainDisk.authoritative = true;
            if (leftPaddle != null) leftPaddle.SetSizeMultiplier(1f);
            if (rightPaddle != null) rightPaddle.SetSizeMultiplier(1f);
            if (powerUps != null) powerUps.ReadState(new Snapshot()); // hides target, clears effects
            RaiseLocalSide(); // back to left until we pair again
        }
        if (game != null) game.ResetMatch();
    }

    private void RaiseLocalSide()
    {
        if (LocalSideChanged != null) LocalSideChanged(LocalOwner);
    }

    // ------------------------------------------------------------------ status overlay

    void OnGUI()
    {
        if (!showStatus || !IsNetworked) return;

        string text;
        switch (CurrentState)
        {
            case State.Searching: text = "Searching for a game on the LAN..."; break;
            case State.Connecting: text = "Joining " + connectingTo + "..."; break;
            case State.Hosting:
                text = HasPeer
                    ? "Hosting - playing against " + remoteName
                    : "Hosting on " + localIp + " - waiting for the other player...";
                break;
            case State.Connected: text = "Connected to " + remoteName; break;
            default: text = ""; break;
        }

        var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(12, Screen.height - 32, 900, 28), text, style);
    }
}
