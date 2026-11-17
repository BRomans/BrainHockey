using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using LSL;
using System;
using System.Collections.Generic;
using System.Linq;

public class LSLDevice : NeuroDevice
{
    // LSL Inlet
    public StreamInlet eegInlet;
    public StreamInlet inferenceInlet;
    public StreamInlet qualityInlet;
    // LSL Outlet
    public StreamOutlet markerOutlet;
    public string EEGStreamIn = "CortexEEG";
    public string MarkersStreamOut = "Tiny_BCI_Triggers";
    public string InferenceStreamIn = "Tiny_BCI_Inferences";
    public string QualityStreamIn = "CortexQuality";
    private StreamInfo[] inStreamInfo;
    private const string OutStreamType = "Triggers";
    private const string MarkerStreamID = "tiny_bci_ssvep_experiment_triggers";
    private const int ChannelCount = 1; // Single channel for markers
    private const double SampleRate = 0; // Irregular sampling rate
    private const channel_format_t ChannelFormat = channel_format_t.cf_int16;
    private double[] sample;
    private double[] qualitySample;
    private double[] inferenceSample;
    private double ts = 0;
    private double prev_ts = 0;
    private double delta = 0;
    private int[] trigger = new int[1];
    private int nClasses = -1;
    
    private Thread resolverThread;
    private readonly object streamLock = new object();
    private volatile bool disconnected = false;
    volatile List<string> pendingSources;


    public LSLDevice()
    {
        OnDataReceived = new UnityEvent<double[], double>();
        OnQualityReceived = new UnityEvent<double[], double>();
        OnInferenceReceived = new UnityEvent<InferenceResult>();
        Connect();
    }

    public override void Connect()
    {
        lock (streamLock)
        {
            disconnected = false;                   
            if (markerOutlet == null) InitMarkerStream();
        }
        Application.quitting -= Disconnect;         
        Application.quitting += Disconnect;
        
        Thread ThreadResolver = new Thread(() =>
        {
            InitDataStream();
            InitInferenceStream();
            InitQualityStream();
        }) { IsBackground = true, Name = "LSL resolver" };
        ThreadResolver.Start();
    }
    
    private StreamInlet ResolveInlet(string name)
    {
        try
        {
            StreamInfo[] infos = LSL.LSL.resolve_stream("name", name, timeout: 1);
            if (infos.Length == 0) { Debug.LogWarning("No LSL stream found with the name: " + name); return null; }
            Debug.Log("Found LSL stream with name: " + name);
            return new StreamInlet(infos[0], postproc_flags: processing_options_t.proc_clocksync | processing_options_t.proc_dejitter);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error opening LSL stream {name}: {e.Message}");
            return null;
        }
    }
    
    private void InitDataStream()
    {
        StreamInlet inlet = ResolveInlet(EEGStreamIn);
        if (inlet == null) return;
        lock (streamLock)
        {
            if (disconnected) { inlet.Dispose(); return; }
            sample = new double[inlet.info().channel_count()];
            eegInlet = inlet;
        }
    }


    private void InitMarkerStream()
    {
        try
        {
            StreamInfo streamInfo = new StreamInfo(MarkersStreamOut, OutStreamType, ChannelCount, SampleRate, ChannelFormat, MarkerStreamID);
            // add channel metadata
            var chns = streamInfo.desc().append_child("channels").append_child("channel");
            chns.append_child_value("label", "Marker");
            chns.append_child_value("type", "Trigger");
            markerOutlet = new StreamOutlet(streamInfo);
            Debug.Log("Started LSL stream with name: " + MarkersStreamOut);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error opening LSL stream: " + e.Message);
        }
    }

    private void InitInferenceStream()
    {
        StreamInlet inlet = ResolveInlet(InferenceStreamIn);
        if (inlet == null) return;

        lock (streamLock)
        {
            if (disconnected) { inlet.Dispose(); return; }
            if (disconnected) { inlet.Dispose(); return; }
            inferenceSample = new double[inlet.info().channel_count()];
            inferenceInlet = inlet;
        }
    }
    
    private void InitQualityStream()
    {
        StreamInlet inlet = ResolveInlet(QualityStreamIn);
        if (inlet == null) return;
        lock (streamLock)
        {
            if (disconnected) { inlet.Dispose(); return; }
            qualitySample = new double[inlet.info().channel_count()];  
            qualityInlet = inlet;
        }
    }

    public override void GetEEGDataStream()
    {
        if (eegInlet == null) return;
        double ts;
        while ((ts = eegInlet.pull_sample(sample, 0.0)) > 0)   // drain everything since last frame
            OnDataReceived.Invoke(sample, ts);
    }
    
    public override void GetQualityStream()
    {
        if (qualityInlet == null) return;
        double ts;
        while ((ts = qualityInlet.pull_sample(qualitySample, 0.0)) > 0)
            OnQualityReceived.Invoke(qualitySample, ts);
    }

    public override void GetInferenceStream()
    {
        if (inferenceInlet == null) return;
        EnsureLayout();

        double ts = inferenceInlet.pull_sample(inferenceSample, 0.0);
        if (ts == 0) return;

        var result = new InferenceResult
        {
            selectedClass = (int)inferenceSample[0],
            probabilities = new float[nClasses],
            confidence = (float)inferenceSample[^1],
            timestamp = ts
        };
        for (int i = 0; i < nClasses; i++)
            result.probabilities[i] = (float)inferenceSample[i + 1];

        OnInferenceReceived.Invoke(result);
    }

    
    void EnsureLayout()
    {
        if (nClasses >= 0) return;
        nClasses = inferenceSample.Length - 2; 
    }
    
    public override void SendMarker(int marker)
    {
        if (disconnected) return;                  // never reopen the outlet during shutdown
        if (markerOutlet == null) InitMarkerStream();
        if (markerOutlet == null) return;
        
        ts = LSL.LSL.local_clock();
        delta = ts - prev_ts;
        trigger[0] = marker;
        prev_ts = ts;
        markerOutlet.push_sample(trigger, ts);

    #if UNITY_EDITOR
        Debug.Log("Sent marker: " + marker + " with delta: " + (delta * 1000).ToString("F4")  + " ms");
    #endif
    }

    public override void Disconnect()
    {
        lock (streamLock)
        {
            if (disconnected) return;      
            disconnected = true;

            CloseInlet(ref eegInlet);
            CloseInlet(ref inferenceInlet);
            CloseInlet(ref qualityInlet);

            if (markerOutlet != null)
            {
                markerOutlet.Dispose();
                markerOutlet.Close();
                markerOutlet = null;
            }
        }
        Application.quitting -= Disconnect;
        Debug.Log("LSL streams closed");
    }

    public override void GetAvailableSources()
    {
        new Thread(() =>
        {
            var names = LSL.LSL.resolve_streams(1.0).Select(s => s.name()).Distinct().ToList();
            pendingSources = names;   // handed to the main thread below
        }) { IsBackground = true }.Start();
    }
    
    void DeliverSources()
    {
        var names = pendingSources;
        if (names == null) return;
        pendingSources = null;
        sources = names;
        OnSourcesUpdated.Invoke(names);
    }

    private static void CloseInlet(ref StreamInlet inlet)
    {
        if (inlet == null) return;
        try { inlet.close_stream(); } catch (Exception) { }  // drop the connection
        inlet.Dispose();                                       // free the native handle
        inlet.Close();
        inlet = null;
    }
}
