using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SSVEPController : StimControllerBase
{
    private enum SSVEPState
    {
        Idle,
        Training,
        TrainingITI,
        Application
    }
    
    public enum FrequencyMode
    {
        SnapToFrames,   // f = refresh / round(refresh / f): exact square wave, frequency may shift
        Approximate     // keeps the requested f on average, cycle length varies by ±1 frame
    }
    
    
    // Timing parameters
    [Header("SSVEP Parameters")]
    public float[] Frequencies = new float[] { 12f, 15f, 20f }; // Hz
    [Tooltip("Phase of each class in cycles (0..1), applied in BOTH training and inference. Missing entries = 0.")]
    public float[] Phases = new float[0];
    public FrequencyMode FreqMode = FrequencyMode.SnapToFrames;
    public float DutyCycle = 0.5f; // 0.0 to 1.0 (0.5 = 50% on)
    public float TrialDuration = 5f; // seconds per stimulus in training mode
    public float ITI = 1f; // Inter-trial interval in seconds
    [Tooltip("Inference sync marker interval in seconds (0 = only the start marker)")]
    public float InferenceMarkerInterval = 1f;
    
    [Tooltip("On a dropped frame, advance the flicker by the missed frames so it stays locked to real time (and the EEG)")]
    public bool CompensateDroppedFrames = true;
    
    // Markers
    private int StartTrainingMk = 194;
    private int EndTrainingMk = 192;
    private int ApplicationStartMk = 193;
    private int ApplicationEndMk = 10; // temporary, just for the SSVEP demo
    private int EndOfTrialMk = 10;
    private bool trialMarkerPending = false; // trial marker is sent on the first drawn frame of the trial

    
    // State tracking
    private bool isTraining = false;
    private bool isApplication = false;
    private int currentTrial = 0;
    private int frameIndex = 0;
    private Dictionary<int, int> stimState = new Dictionary<int, int>();  // last state sent to each stim
    private int trialFrames = 0;
    private int droppedThisTrial = 0;
    private int droppedTotal = 0;
    private double frameClockStart;     // time at which frameIndex 0 was drawn
    private int lagFrames = 0;          // consecutive frames we've been behind
    
    // Frequency mapping and sequences
    private float refreshRate = 60f;
    private int inferenceMarkerFrames = 0;
    private Dictionary<int, double> frequencyMap = new Dictionary<int, double>();  // frequency actually shown
    private Dictionary<int, double> phaseMap = new Dictionary<int, double>();
    
    // Training mode state
    private int currentTrainingStimClass = -1;
    private double trialStartTime = 0;
    private double itiStartTime = 0;
    private bool inITI = false;
    
    // Stimuli
    private Dictionary<int, NeuroStim> Stims = new Dictionary<int, NeuroStim>();
    private int[] stimClassOrder; // Ordered list of stim classes for cycling
    
    private SSVEPState currentState = SSVEPState.Idle;
    
    void Start()
    {
        // Initialization handled in Init()
    }
    
    void Update()
    {
        if (!isTraining && !isApplication) return;

        if (StartTime == 0)
            StartTime = Time.unscaledTimeAsDouble;
        if (Time.unscaledTimeAsDouble - StartTime <= StartDelay) return;
        
        // Frame drop detection 
        float expected = 1f / refreshRate;
        if (frameIndex > 0 && Time.unscaledDeltaTime > 1.5f * expected)
        {
            int missed = Mathf.RoundToInt(Time.unscaledDeltaTime / expected) - 1;
            droppedThisTrial += missed;
            droppedTotal += missed;
            //Debug.LogWarning($"Frame drop: ~{missed} frame(s) missed at frameIndex {frameIndex}");
            if (CompensateDroppedFrames) frameIndex += missed;
        }
        
        // Execute state machine
        if (isTraining)
        {
            ProcessTrainingMode();
        }
        else if (isApplication)
        {   
            ProcessApplicationMode();
        }
        
        frameIndex++;
    }
    
    override public void Init(NeuroDevice device)
    {
        Device = device;
        InitStims();
        InitFrequencies();
        
        Debug.Log($"SSVEP Controller initialized with {nClasses} stimuli");
        LogSequences();
    }

    override public void Reinitialize()
    {
        Init(Device);
    }
    
    override public void InitStims()
    {
        Stims.Clear();
        List<NeuroStim> sceneStims = new List<NeuroStim>();
        foreach (string tag in StimTags)
        {
            sceneStims.AddRange(GameObject.FindGameObjectsWithTag(tag)
                .Select(x => x.GetComponent<NeuroStim>())
                .Where(s => s != null && !sceneStims.Contains(s)));
        }
        
        sceneStims.Sort((x, y) => x.Class.CompareTo(y.Class));
        
        if (nClasses < sceneStims.Count)
        {
            nClasses = sceneStims.Count;
            Debug.Log($"Setting nClasses to {nClasses} based on scene stimuli");
        }
        
        for (int i = 0; i < sceneStims.Count; i++)
        {
            Stims.TryAdd(i + 1, sceneStims[i]);
        }
        
        // Create ordered array of stim classes for training cycling
        stimClassOrder = Stims.Keys.OrderBy(x => x).ToArray();
        
        Debug.Log($"Initialized {Stims.Count} stimuli in order: [{string.Join(", ", stimClassOrder)}]");
    }
    
    private void InitFrequencies()
    {
        refreshRate = Mathf.Round((float)Screen.currentResolution.refreshRateRatio.value);
        if (QualitySettings.vSyncCount == 0)
            Debug.LogWarning("VSync is off: Update is not locked to the display, flicker frequencies will be wrong.");
        inferenceMarkerFrames = Mathf.RoundToInt(InferenceMarkerInterval * refreshRate);

        frequencyMap.Clear();
        phaseMap.Clear();

        if (Frequencies.Length < Stims.Count)
            Debug.LogError($"{Stims.Count} stims but only {Frequencies.Length} frequencies: some classes will share a frequency.");
        
        foreach (int stimClass in Stims.Keys.OrderBy(k => k))
        {
            double requested = Frequencies[(stimClass - 1) % Frequencies.Length];
            double f = requested;

            if (FreqMode == FrequencyMode.SnapToFrames)
            {
                int period = Math.Max(2, (int)Math.Round(refreshRate / requested));
                f = refreshRate / period;
                if (Math.Abs(f - requested) > 0.01)
                    Debug.LogWarning($"Stim{stimClass}: {requested} Hz snapped to {f:F3} Hz ({period} frames/cycle)");
            }
            if (f > refreshRate / 2.0)
                Debug.LogError($"Stim{stimClass}: {f:F2} Hz is above half the refresh rate ({refreshRate} Hz)");

            frequencyMap[stimClass] = f;
            phaseMap[stimClass] = (stimClass - 1) < Phases.Length ? Phases[stimClass - 1] : 0.0;

            // Actual duty cycle over 10 s (e.g. 20 Hz @ 60 Hz = 3 frames -> 2 on = 67%, not 50%)
            int n = (int)(refreshRate * 10), on = 0;
            for (int i = 0; i < n; i++) on += StimState(stimClass, i);
            Debug.Log($"Stim{stimClass}: {f:F3} Hz, phase {phaseMap[stimClass]:F2} cyc, duty {100.0 * on / n:F0}% " +
                      $"(requested {requested} Hz, {DutyCycle:P0})");
        }

        // Two classes on the same frequency, or one on a harmonic of another
        var fs = frequencyMap.OrderBy(k => k.Key).ToList();
        for (int i = 0; i < fs.Count; i++)
        for (int j = i + 1; j < fs.Count; j++)
        {
            double lo = Math.Min(fs[i].Value, fs[j].Value), hi = Math.Max(fs[i].Value, fs[j].Value);
            double ratio = hi / lo;
            if (hi - lo < 0.01)
                Debug.LogError($"Stim{fs[i].Key} and Stim{fs[j].Key} flicker at the same frequency ({lo:F3} Hz)");
            else if (Math.Abs(ratio - Math.Round(ratio)) < 0.01)
                Debug.LogWarning($"Stim{fs[j].Key} ({hi:F2} Hz) is a harmonic of Stim{fs[i].Key} ({lo:F2} Hz)");
        }
    }
    
    override public void InitFlashSequence()
    {
        // Handled in InitSequences()
    }
    
    private void LogSequences()
    {
        foreach (int stimClass in Stims.Keys.OrderBy(k => k))
        {
            string seq = string.Join(",", Enumerable.Range(0, 30).Select(i => StimState(stimClass, i)));
            Debug.Log($"Stim{stimClass} ({frequencyMap[stimClass]:F2}Hz): [{seq}...]");
        }
    }
    
    override public void StartTraining()
    {
        if (Stims.Count == 0)
        {
            Debug.LogError("No stimuli found! Cannot start training.");
            return;
        }
        
        if (sendStartStopMarkers) Device.SendMarker(StartTrainingMk);
        
        currentTrial = 0;
        StartTime = 0;
        isTraining = true;
        isApplication = false;
        trialFrames = Mathf.RoundToInt(TrialDuration * refreshRate);
        droppedTotal = 0;
        TurnOffAllStims();
        StartTrial();
        
        Debug.Log($"SSVEP Training started: {Trials} trials, {TrialDuration}s per trial");
    }
    
    override public void StopTraining()
    {
        isTraining = false;
        trialMarkerPending = false;
        currentState = SSVEPState.Idle;
        TurnOffAllStims();
        
        if (sendStartStopMarkers) Device.SendMarker(EndTrainingMk);
        Debug.Log($"Training completed: {currentTrial} trials finished");
        if (droppedTotal > 0) Debug.LogWarning($"{droppedTotal} dropped frame(s) this session");
    }
    
    override public void StartInference()
    {
        if (Stims.Count == 0)
        {
            Debug.LogError("No stimuli found! Cannot start application.");
            return;
        }
        TurnOffAllStims();
        
        if (sendStartStopMarkers) Device.SendMarker(ApplicationStartMk);
        
        frameIndex = 0;
        StartTime = 0;    
        currentTrainingStimClass = -1; // No specific class in application mode
        isTraining = false;
        isApplication = true;
        currentState = SSVEPState.Application;
        
        Debug.Log("SSVEP Application started: Continuous mode with phase-desynchronized stimuli");
    }
    
    override public void StopInference()
    {
        isApplication = false;
        currentState = SSVEPState.Idle;
        TurnOffAllStims();
        
        if (sendStartStopMarkers) Device.SendMarker(ApplicationEndMk);
        Debug.Log("Application mode stopped");
        if (droppedTotal > 0) Debug.LogWarning($"{droppedTotal} dropped frame(s) this session");
    }
    
    private void StartTrial()
    {
        int stimIndex = currentTrial % stimClassOrder.Length;
        currentTrainingStimClass = stimClassOrder[stimIndex];
        currentState = SSVEPState.Training;
        inITI = false;

        frameIndex = 0;
        trialMarkerPending = true;

        Debug.Log($"Trial {currentTrial + 1}/{Trials}: Stim{currentTrainingStimClass} " +
                  $"({frequencyMap[currentTrainingStimClass]}Hz) for {TrialDuration}s");
    }
    
    private void ProcessTrainingMode()
    {
        if (inITI)
        {
            Device.SendMarker(EndOfTrialMk);
            // In inter-trial interval
            if (Time.unscaledTimeAsDouble - itiStartTime < ITI) return;   // still in ITI: do nothing this frame

            currentTrial++;
            if (currentTrial >= Trials) { StopTraining(); return; }
            StartTrial(); 
        }
        if (trialMarkerPending)
        {
            Device.SendMarker(currentTrainingStimClass);   // always sent, not gated by sendStartStopMarkers
            trialStartTime = Time.unscaledTimeAsDouble;
            trialMarkerPending = false;
        }
        else if (frameIndex >= trialFrames)
        {
            if (droppedThisTrial > 0)
                Debug.LogWarning($"Trial {currentTrial + 1}: {droppedThisTrial} dropped frame(s)");
            TurnOffAllStims();
            inITI = true;
            itiStartTime = Time.unscaledTimeAsDouble;
            currentState = SSVEPState.TrainingITI;
            double elapsed = Time.unscaledTimeAsDouble - trialStartTime;
            Debug.Log($"Trial {currentTrial + 1}: {trialFrames} frames in {elapsed:F3}s " +
                      $"(expected {TrialDuration:F3}s, refresh {refreshRate} Hz), {droppedThisTrial} dropped");
            
            return;
        }

        CheckFrameDrift();
        UpdateStimulus(currentTrainingStimClass, frameIndex);
        
    }
    
    private void ProcessApplicationMode()
    {
        bool firstFrame = frameIndex == 0;
        bool periodic = inferenceMarkerFrames > 0 && frameIndex % inferenceMarkerFrames == 0;

        CheckFrameDrift();
        if (firstFrame || periodic)
        {
            Device.SendMarker(1);   // same id and ~1 s interval as before
        }

        foreach (int stimClass in Stims.Keys)
            UpdateStimulus(stimClass, frameIndex);
    }
    
    private void UpdateStimulus(int stimClass, int frame)
    {
        if (!Stims.TryGetValue(stimClass, out NeuroStim stim)) return;

        int state = StimState(stimClass, frame);
        if (stimState.TryGetValue(stimClass, out int prev) && prev == state) return;

        if (state == 1) stim.StimOn();
        else stim.StimOff();
        stimState[stimClass] = state;
    }
    
    private void TurnOffAllStims()
    {
        foreach (var kvp in Stims)
        {
            kvp.Value.StimOff();
            stimState[kvp.Key] = 0; 
        }
    }
    
    private int StimState(int stimClass, int frame)
    {
        double cycles = frequencyMap[stimClass] * frame / refreshRate + phaseMap[stimClass] + 1e-6; // guard rounding at cycle edges
        double phase = cycles - Math.Floor(cycles);
        return phase < DutyCycle ? 1 : 0;
    }
    
    override public Dictionary<int, NeuroStim> GetStims()
    {
        return Stims;
    }
    
    override public void SetStims(List<NeuroStim> stims)
    {
        TurnOffAllStims();                       
        Stims.Clear();
        stimState.Clear();
        frequencyMap.Clear();

        for (int i = 0; i < stims.Count; i++)
        {
            Stims.TryAdd(i + 1, stims[i]);
        }
        nClasses = Stims.Count;
        stimClassOrder = Stims.Keys.OrderBy(x => x).ToArray();
        
        // Reinitialize after setting new stims
        InitFrequencies();
    }
    
    private void CheckFrameDrift()
    {
        double now = Time.unscaledTimeAsDouble;
        if (frameIndex == 0) { frameClockStart = now; lagFrames = 0; return; }

        int lag = (int)Math.Round((now - frameClockStart) * refreshRate) - frameIndex;
        if (lag < 1) { lagFrames = 0; return; }           // on time, or a slow frame already caught up

        if (++lagFrames < 3) return;                      // must stay behind for 3 frames: rules out one-frame jitter

        droppedThisTrial += lag;
        droppedTotal += lag;
        if (CompensateDroppedFrames) frameIndex += lag;   // catch up to real time
        else frameClockStart += lag / (double)refreshRate; // accept the delay, don't count it again
        lagFrames = 0;
    }
}