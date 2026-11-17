// NeuroDevice backed by a TinyBCI engine over bci_link (the Pico over USB).
//
// Auto mode (PortName empty): probes the serial ports in the background and
// takes the one whose engine reports link A (the presenter's link). Link B
// is left for the Python collector, which records the EEG and inference CSVs.
// Manual mode: set PortName, or call Reconnect("COM7") / Reconnect("/dev/cu.usbmodem1201").
//
// Inferences and engine status only: EEG frames are skipped (the collector
// records them). Timestamps are engine seconds (the Pico's clock since boot).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using TinyBCI.Link;
using Debug = UnityEngine.Debug;

public class TinyBCILinkDevice : NeuroDevice
{
    // --- settings: change, then call Reconnect() (or before Connect())

    public string PortName;                    // empty: auto-detect
    public int AcceptedLinkId = BciLink.LinkA; // auto-detect only; -1 accepts any link
    public bool AutoReconnect = true;
    public int ProbeTimeoutMs = 1500;          // the engine answers HELLO with STATUS at once
    public int RetryIntervalMs = 1000;
    public double SilenceTimeoutSeconds = 3.0; // STATUS arrives every second

    /// Opens a transport by name; replaceable for tests or other transports.
    public Func<string, ILinkTransport> TransportFactory = name => new SerialLinkTransport(name);
    /// Lists candidate ports for GetAvailableSources() and auto-detect.
    public Func<List<string>> PortLister = SerialLinkTransport.ListPorts;

    // --- state, readable from the main thread

    public string ConnectedPort { get { return connectedPort; } }
    public BciEngineState EngineState { get { return engineState; } }
    public bool IsConnected
    {
        get { return transport != null && SecondsSince(Interlocked.Read(ref lastFrameTicks)) < SilenceTimeoutSeconds; }
    }
    public bool IsReady { get { return IsConnected && engineState == BciEngineState.Ready; } }
    public BciLinkStatus LastStatus { get { lock (statusLock) return lastStatus; } }
    public int LostFrames { get { return lostFrames; } }   // sequence gaps, all message types

    public string StatusText
    {
        get
        {
            if (!IsConnected) return string.IsNullOrEmpty(PortName)
                ? "Waiting for BCI Engine (searching USB)..."
                : "Waiting for BCI Engine on " + PortName + "...";
            return "BCI Engine: " + BciLink.StateName(engineState);
        }
    }

    // ---

    readonly ConcurrentQueue<InferenceResult> inferences = new ConcurrentQueue<InferenceResult>();
    readonly object writeLock = new object();
    readonly object statusLock = new object();

    Thread worker;
    volatile bool running;
    volatile ILinkTransport transport;
    volatile string connectedPort;
    volatile BciEngineState engineState = BciEngineState.Booting;
    BciLinkStatus lastStatus;
    long lastFrameTicks;
    volatile int lostFrames;
    int expectedSequence = -1;
    byte txSequence;

    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static double SecondsSince(long ticks) { return (Clock.ElapsedTicks - ticks) / (double)Stopwatch.Frequency; }

    public TinyBCILinkDevice() : this(null) { }

    public TinyBCILinkDevice(string portName)
    {
        OnDataReceived = new UnityEvent<double[], double>();
        OnQualityReceived = new UnityEvent<double[], double>();
        OnInferenceReceived = new UnityEvent<InferenceResult>();
        sources = new List<string>();
        PortName = portName;
        Connect();
    }

    // --- NeuroDevice

    public override void GetAvailableSources()
    {
        sources = PortLister();
        OnSourcesUpdated.Invoke(sources);
    }

    public override void Connect()
    {
        if (worker != null && worker.IsAlive) return;
        running = true;
        Application.quitting -= Disconnect;
        Application.quitting += Disconnect;

        worker = new Thread(Run) { IsBackground = true, Name = "bci_link reader" };
        worker.Start();
    }

    public override void Disconnect()
    {
        running = false;
        CloseTransport();   // unblocks the reader
        if (worker != null && worker.IsAlive && Thread.CurrentThread != worker) worker.Join(1000);
        worker = null;
        Application.quitting -= Disconnect;
    }

    /// Switch port (null or empty: auto-detect) and connect again.
    public void Reconnect(string portName)
    {
        Disconnect();
        PortName = portName;
        Connect();
    }

    public void Reconnect() { Reconnect(PortName); }

    public override void GetEEGDataStream() { }       // EEG is recorded by the collector
    public override void GetQualityStream() { }       // no quality message on the link yet

    public override void GetInferenceStream()
    {
        InferenceResult result;
        while (inferences.TryDequeue(out result)) OnInferenceReceived.Invoke(result);
    }

    public override void SendMarker(int marker)
    {
        if (marker < 0 || marker > ushort.MaxValue)
        {
            Debug.LogWarning("bci_link: marker " + marker + " out of range, not sent");
            return;
        }
        if (!Send(transport, BciLinkType.Trigger, BciLink.TriggerPayload((ushort)marker)))
        {
            Debug.LogWarning("bci_link: no engine connected, marker " + marker + " dropped");
            return;
        }
#if UNITY_EDITOR
        Debug.Log("bci_link: sent marker " + marker);
#endif
    }

    // --- worker thread

    void Run()
    {
        while (running)
        {
            var parser = new BciLinkParser();
            ILinkTransport opened = string.IsNullOrEmpty(PortName) ? Search(parser) : OpenManual(PortName);

            if (opened == null)
            {
                if (!AutoReconnect && !string.IsNullOrEmpty(PortName)) break;
                Sleep(RetryIntervalMs);
                continue;
            }

            connectedPort = opened.Name;
            transport = opened;
            Debug.Log("bci_link: connected on " + opened.Name);

            try { ReadLoop(opened, parser); }
            catch (Exception e)
            {
                if (running) Debug.LogWarning("bci_link: lost " + opened.Name + " (" + e.Message + ")");
            }
            CloseTransport();

            if (!AutoReconnect) break;
            Sleep(RetryIntervalMs);
        }
        running = false;
    }

    void ReadLoop(ILinkTransport link, BciLinkParser parser)
    {
        var buffer = new byte[1024];
        while (running)
        {
            int count = link.Read(buffer, 0, buffer.Length);
            for (int i = 0; i < count; i++)
            {
                BciLinkMessage message;
                if (parser.Push(buffer[i], out message)) Handle(message);
            }
        }
    }

    void Handle(BciLinkMessage message)
    {
        Interlocked.Exchange(ref lastFrameTicks, Clock.ElapsedTicks);

        if (expectedSequence >= 0 && message.Sequence != expectedSequence)
            lostFrames += (message.Sequence - expectedSequence) & 0xFF;
        expectedSequence = (message.Sequence + 1) & 0xFF;

        switch (message.Type)
        {
            case BciLinkType.Status:
            {
                var status = BciLink.ReadStatus(message.Payload);
                lock (statusLock) lastStatus = status;
                if (status.State != engineState)
                    Debug.Log("bci_link: engine " + BciLink.StateName(status.State));
                engineState = status.State;
                break;
            }
            case BciLinkType.Inference:
            {
                var inference = BciLink.ReadInference(message.Payload);
                inferences.Enqueue(new InferenceResult
                {
                    // wire labels are 1-based (as in the CSV); LSLDevice gives 0-based
                    selectedClass = inference.PredictedLabel - 1,
                    targetClass = inference.TrueLabel - 1,
                    confidence = inference.Confidence,
                    probabilities = inference.Probabilities,
                    timestamp = inference.TimestampUs / 1e6,   // engine seconds
                });
                break;
            }
            case BciLinkType.Log:
                Debug.Log("engine: " + BciLink.ReadLog(message.Payload));
                break;
            default:   // EEG, trigger echoes, pongs: not used here
                break;
        }
    }

    // --- opening

    ILinkTransport OpenManual(string portName)
    {
        ILinkTransport link = TryOpen(portName);
        if (link == null) return null;
        if (!Send(link, BciLinkType.Ack, BciLink.AckPayload(BciLinkRole.Presenter)))
        {
            link.Dispose();
            return null;
        }
        return link;   // no link-id check: the user chose this port
    }

    ILinkTransport Search(BciLinkParser parser)
    {
        foreach (string portName in PortLister())
        {
            if (!running) return null;
            parser.Reset();
            ILinkTransport link = Probe(portName, parser);
            if (link != null) return link;
        }
        return null;
    }

    // Open, get ACK, and wait for the engine's STATUS reply.
    ILinkTransport Probe(string portName, BciLinkParser parser)
    {
        ILinkTransport link = TryOpen(portName);
        if (link == null) return null;

        try
        {
            if (!Send(link, BciLinkType.Ack, BciLink.AckPayload(BciLinkRole.Presenter)))
                throw new IOException("write failed");

            var buffer = new byte[256];
            long start = Clock.ElapsedTicks;
            while (running && SecondsSince(start) * 1000 < ProbeTimeoutMs)
            {
                int count = link.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < count; i++)
                {
                    BciLinkMessage message;
                    if (!parser.Push(buffer[i], out message) || message.Type != BciLinkType.Status) continue;

                    var status = BciLink.ReadStatus(message.Payload);
                    if (status.LinkId < 0)
                        Debug.LogWarning("bci_link: engine firmware doesn't report its link id; " +
                                         "taking " + portName + " (it may be link B)");
                    else if (AcceptedLinkId >= 0 && status.LinkId != AcceptedLinkId)
                    {
                        Debug.Log("bci_link: " + portName + " is link " + LinkName(status.LinkId) + ", skipping");
                        link.Dispose();
                        return null;
                    }
                    Handle(message);
                    return link;
                }
            }
        }
        catch (Exception) { }

        link.Dispose();   // not an engine, or no answer in time
        return null;
    }

    ILinkTransport TryOpen(string portName)
    {
        try { return TransportFactory(portName); }
        catch (Exception) { return null; }   // missing, busy (collector), or no permission
    }

    // --- helpers

    bool Send(ILinkTransport link, BciLinkType type, byte[] payload)
    {
        if (link == null) return false;
        try
        {
            lock (writeLock) link.Write(BciLink.Encode(type, txSequence++, payload));
            return true;
        }
        catch (Exception) { return false; }   // the reader notices the lost link
    }

    void CloseTransport()
    {
        ILinkTransport link = transport;
        transport = null;
        connectedPort = null;
        expectedSequence = -1;
        if (link != null) link.Dispose();
    }

    void Sleep(int milliseconds)
    {
        for (int waited = 0; running && waited < milliseconds; waited += 50) Thread.Sleep(50);
    }

    static string LinkName(int linkId)
    {
        return linkId == BciLink.LinkA ? "A" : linkId == BciLink.LinkB ? "B" : "#" + linkId;
    }
}