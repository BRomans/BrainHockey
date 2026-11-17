using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public struct InferenceResult
{
    public int selectedClass;
    public float confidence;
    public float[] probabilities;
    public float targetClass;
    public double timestamp;
}

public abstract class NeuroDevice
{
    public enum Device {LSL, USB}
    public UnityEvent<double[], double> OnDataReceived;
    public UnityEvent<double[], double> OnQualityReceived;
    public UnityEvent<InferenceResult> OnInferenceReceived;
    public UnityEvent<List<string>> OnSourcesUpdated = new UnityEvent<List<string>>();
    public List<String> sources;

    public abstract void GetEEGDataStream();
    public abstract void GetQualityStream();
    public abstract void GetInferenceStream();
    public abstract void SendMarker(int marker);
    public abstract void Connect();
    public abstract void Disconnect();
    public abstract void GetAvailableSources();
}