using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class StimControllerBase : MonoBehaviour
{
    public enum Paradigm
    {
        TVEP,
        P300
    }

    [Header("General Settings")]
    [SerializeField]
    [Tooltip("Stimulus used during training phase")]
    public NeuroStim TrainingStim;
    [SerializeField]
    [Tooltip("Number of classes in the paradigm")]
    public int nClasses = 4;
    [SerializeField]
    [Tooltip("Number of trials for training phase")]
    public int trials = 60 ;
    [SerializeField]
    [Tooltip("Delay before starting training or application in seconds")]
    protected float StartDelay = 1f;
    [SerializeField]
    [Tooltip("Send start/stop markers to the NeuroDevice")]
    protected bool sendStartStopMarkers = false;

    protected NeuroDevice Device;
    protected double StartTime = 0;
    
    public int Trials {get {return trials;} set {trials = value;} }
    
    public abstract void Init(NeuroDevice device);
    public abstract void Reinitialize();
    public abstract void InitStims();
    public abstract void InitFlashSequence();
    public abstract void StartTraining();
    public abstract void StopTraining();
    public abstract void StartInference();
    public abstract void StopInference();
    public abstract Dictionary<int, NeuroStim> GetStims();
    public abstract void SetStims(List<NeuroStim> stims);

    public void SetTrials(string nTrials)
    {
        try
        {
            this.Trials = int.Parse(nTrials);
        }
        catch
        {
            Debug.LogWarning("Trial number is must be an integer value");
        }
    }
}