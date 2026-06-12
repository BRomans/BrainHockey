using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using LSL;
using System;

public class LSLDevice : NeuroDevice
{
    // LSL Inlet
    public StreamInlet eegInlet;
    public StreamInlet inferenceInlet;
    public StreamInlet qualityInlet;
    // LSL Outlet
    public StreamOutlet outlet;
    public string EEGStreamIn = "CortexEEG";
    public string MarkersStreamOut = "CortexMarkers";
    public string InferenceStreamIn = "CortexInference";
    public string QualityStreamIn = "CortexQuality";
    private StreamInfo[] inStreamInfo;
    private const string OutStreamType = "Markers";
    private const string MarkerStreamID = "CortexIrregularMarkersStream";
    private const int ChannelCount = 1; // Single channel for markers
    private const double SampleRate = 0; // Irregular sampling rate
    private const channel_format_t ChannelFormat = channel_format_t.cf_int8;
    private double[] sample;
    private double ts = 0;
    private double prev_ts = 0;
    private double delta = 0;
    private int[] trigger = new int[1];
    private int nClasses = -1;

    public LSLDevice()
    {
        OnDataReceived = new UnityEvent<double[], double>();
        OnQualityReceived = new UnityEvent<double[], double>();
        OnInferenceReceived = new UnityEvent<InferenceResult>();
        InitMarkerStream();
        Connect();

    }

    public override void Connect()
    {
        Thread ThreadResolver = new Thread(() =>
        {
            InitDataStream();
            InitInferenceStream();
            InitQualityStream();
        });
        ThreadResolver.Start();
    }
    private void InitDataStream()
    {
        inStreamInfo = LSL.LSL.resolve_stream("name", EEGStreamIn, timeout: 1);
        if (inStreamInfo.Length > 0)
        {
            Debug.Log("Found LSL stream with name: " + EEGStreamIn);
            eegInlet = new StreamInlet(inStreamInfo[0]);
            sample = new double[inStreamInfo[0].channel_count()]; // Adjust to the number of channels in your stream
        }
        else
        {
            Debug.LogWarning("No LSL stream found with the name: " + EEGStreamIn);
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
            outlet = new StreamOutlet(streamInfo);
            Debug.Log("Started LSL stream with name: " + MarkersStreamOut);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error opening LSL stream: " + e.Message);
        }
    }

    private void InitInferenceStream()
    {
        try
        {
            inStreamInfo = LSL.LSL.resolve_stream("name", InferenceStreamIn, timeout: 1);
            if (inStreamInfo.Length > 0)
            {
                Debug.Log("Found LSL stream with name: " + InferenceStreamIn);
                inferenceInlet = new StreamInlet(inStreamInfo[0]);
            }
            else
            {
                Debug.LogWarning("No LSL stream found with the name: " + InferenceStreamIn);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error opening LSL stream: " + e.Message);
        }
    }

    private void InitQualityStream()
    {
        try
        {
            inStreamInfo = LSL.LSL.resolve_stream("name", QualityStreamIn, timeout: 1);
            if (inStreamInfo.Length > 0)
            {
                Debug.Log("Found LSL stream with name: " + QualityStreamIn);
                qualityInlet = new StreamInlet(inStreamInfo[0]);
            }
            else
            {
                Debug.LogWarning("No LSL stream found with the name: " + QualityStreamIn);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error opening LSL stream: " + e.Message);
        }
    }

    public override void GetEEGDataStream()
    {
        if (eegInlet != null)
        {

            if (eegInlet.pull_sample(sample, 0.0f) > 0)
            {
                double timestamp = eegInlet.info().created_at();
                OnDataReceived.Invoke(sample, timestamp);
            }
        }
    }

    public override void GetInferenceStream()
    {
        if (inferenceInlet == null)
            return;

        EnsureLayout();

        double[] sample = new double[inferenceInlet.info().channel_count()];
        double ts = inferenceInlet.pull_sample(sample, 0.0f);
        if (ts == 0)
            return;

        var result = new InferenceResult
        {
            selectedClass = (int)sample[0],
            probabilities = new float[nClasses],
            confidence = (float)sample[^1],
            timestamp = ts
        };

        for (int i = 0; i < nClasses; i++)
            result.probabilities[i] = (float)sample[i + 1];

        OnInferenceReceived.Invoke(result);
    }

    
    void EnsureLayout()
    {
        if (nClasses >= 0)
            return;

        int ch = inferenceInlet.info().channel_count();
        nClasses = ch - 2; // [class | probs... | confidence]
    }

    public void GetInferenceStreamOld()
    {
        if (inferenceInlet != null)
        {
            double[] sample = new double[inferenceInlet.info().channel_count()];
            if (inferenceInlet.pull_sample(sample, 0.0f) > 0)
            {
                double timestamp = inferenceInlet.info().created_at();
               // OnInferenceReceived.Invoke(sample, timestamp);
            }
        }
    }

    public override void GetQualityStream()
    {
        if (qualityInlet != null)
        {
            double[] sample = new double[qualityInlet.info().channel_count()];
            if (qualityInlet.pull_sample(sample, 0.0f) > 0)
            {
                double timestamp = qualityInlet.info().created_at();
                OnQualityReceived.Invoke(sample, timestamp);
            }
        }
    }

    public override void SendMarker(int marker)
    {
        if (outlet == null)
        {
            InitMarkerStream();
        }
        ts = LSL.LSL.local_clock();
        delta = ts - prev_ts;
        trigger[0] = marker;
        prev_ts = ts;
        outlet.push_sample(trigger, ts);

    #if UNITY_EDITOR
            //Debug.Log("Sent marker: " + marker + " with delta: " + (delta * 1000).ToString("F4")  + " ms");
    #endif
    }

    public override void Disconnect()
    {
        if (eegInlet != null)
        {
            eegInlet.close_stream();
        }
        if (inferenceInlet != null)
        {
            inferenceInlet.close_stream();
        }
        if (qualityInlet != null)
        {
            qualityInlet.close_stream();
        }
    }
}
