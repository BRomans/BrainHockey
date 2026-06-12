using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SSVEPController : StimControllerBase
{
    // Timing parameters
    [Header("SSVEP Parameters")]
    public float[] Frequencies = new float[] { 12f, 15f, 20f }; // Hz
    public float DutyCycle = 0.5f; // 0.0 to 1.0 (0.5 = 50% on)
    public float TrialDuration = 5f; // seconds per stimulus in training mode
    public float ITI = 1f; // Inter-trial interval in seconds
    public int MaxSequenceFrames = 60; // Cap sequence length for logging
    
    // Markers
    private int StartTrainingMk = 98;
    private int EndTrainingMk = 99;
    private int ApplicationStartMk = 100;
    private int ApplicationEndMk = 101;
    
    // State tracking
    private bool isTraining = false;
    private bool isApplication = false;
    private int currentTrial = 0;
    private int frameIndex = 0;
    private int lastFrameCount = -1;
    
    // Frequency mapping and sequences
    private Dictionary<int, float> frequencyMap = new Dictionary<int, float>();
    private Dictionary<int, int[]> flashSequences = new Dictionary<int, int[]>();
    private Dictionary<int, int> phaseOffsets = new Dictionary<int, int>();
    
    // Training mode state
    private int currentTrainingStimClass = -1;
    private double trialStartTime = 0;
    private double itiStartTime = 0;
    private bool inITI = false;
    
    // Stimuli
    private Dictionary<int, NeuroStim> Stims = new Dictionary<int, NeuroStim>();
    private int[] stimClassOrder; // Ordered list of stim classes for cycling
    
    // State enum
    private enum SSVEPState
    {
        Idle,
        Training,
        TrainingITI,
        Application
    }
    private SSVEPState currentState = SSVEPState.Idle;
    
    void Start()
    {
        // Initialization handled in Init()
    }
    
    void Update()
    {
        if (StartTime == 0)
            StartTime = Time.unscaledTimeAsDouble;        

        if (!isTraining && !isApplication && (Time.unscaledTimeAsDouble - StartTime > StartDelay)) return;
        
        // Frame drop detection
        if (Time.frameCount != lastFrameCount + 1 && lastFrameCount != -1)
        {
            int droppedFrames = Time.frameCount - lastFrameCount - 1;
            Debug.LogWarning($"Frame drop detected! Dropped {droppedFrames} frame(s) at frame {Time.frameCount}");
        }
        lastFrameCount = Time.frameCount;
        
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
        InitSequences();
        
        Debug.Log($"SSVEP Controller initialized with {nClasses} stimuli");
        LogSequences();
    }

    override public void Reinitialize()
    {
        Init(Device);
    }
    
    override public void InitStims()
    {
        List<NeuroStim> sceneStims = GameObject.FindGameObjectsWithTag("Stim")
            .Select(x => x.GetComponent<NeuroStim>())
            .ToList();
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
        float refreshRate = 60f; // Locked at 60Hz as per requirement
        
        foreach (var kvp in Stims)
        {
            int stimClass = kvp.Key;
            NeuroStim stim = kvp.Value;
            
            // Check if NeuroStim has frequency property set
            // Assuming NeuroStim has a public float frequency field
            float assignedFreq;
            
            // Try to get frequency from NeuroStim (you'll need to add this property to NeuroStim)
            // For now, assume it's 0 if not set
            float stimFreq = 0f; // Replace with: stim.frequency when property exists
            
            if (stimFreq == 0f)
            {
                // Assign from default array
                int index = (stimClass - 1) % Frequencies.Length;
                assignedFreq = Frequencies[index];
                Debug.LogWarning($"Stim{stimClass} frequency not set, assigned {assignedFreq}Hz");
            }
            else
            {
                assignedFreq = stimFreq;
                Debug.Log($"Stim{stimClass} using configured frequency: {assignedFreq}Hz");
            }
            
            frequencyMap[stimClass] = assignedFreq;
            
            // Validate frequency alignment with refresh rate
            float framesPerCycle = refreshRate / assignedFreq;
            int roundedFrames = Mathf.RoundToInt(framesPerCycle);
            float actualFrequency = refreshRate / roundedFrames;
            float frequencyError = Mathf.Abs(actualFrequency - assignedFreq);
            
            if (frequencyError > 0.5f)
            {
                Debug.LogWarning($"Stim{stimClass}: Frequency {assignedFreq}Hz rounds to {actualFrequency:F2}Hz " +
                               $"(error: {frequencyError:F2}Hz, {roundedFrames} frames/cycle)");
            }
            else
            {
                Debug.Log($"Stim{stimClass}: {assignedFreq}Hz → {roundedFrames} frames/cycle (actual: {actualFrequency:F2}Hz)");
            }
        }
    }
    
    private void InitSequences()
    {
        float refreshRate = 60f;
        
        // Calculate phase offsets for desynchronization in application mode
        int phaseStep = 2; // Offset each stimulus by 2 frames
        
        foreach (var kvp in frequencyMap)
        {
            int stimClass = kvp.Key;
            float frequency = kvp.Value;
            
            // Calculate frames per cycle
            float framesPerCycleFloat = refreshRate / frequency;
            int framesPerCycle = Mathf.RoundToInt(framesPerCycleFloat);
            
            // Calculate ON frames based on duty cycle
            int onFrames = Mathf.RoundToInt(framesPerCycle * DutyCycle);
            onFrames = Mathf.Max(1, onFrames); // At least 1 frame ON
            
            // Generate sequence capped at MaxSequenceFrames
            int numCycles = Mathf.CeilToInt((float)MaxSequenceFrames / framesPerCycle);
            int sequenceLength = numCycles * framesPerCycle;
            sequenceLength = Mathf.Min(sequenceLength, MaxSequenceFrames);
            
            int[] sequence = new int[sequenceLength];
            for (int i = 0; i < sequenceLength; i++)
            {
                int cycleFrame = i % framesPerCycle;
                sequence[i] = (cycleFrame < onFrames) ? 1 : 0;
            }
            
            flashSequences[stimClass] = sequence;
            
            // Assign phase offset
            int offset = ((stimClass - 1) * phaseStep) % framesPerCycle;
            phaseOffsets[stimClass] = offset;
            
            Debug.Log($"Stim{stimClass} ({frequency}Hz): {framesPerCycle} frames/cycle, " +
                     $"{onFrames} ON frames ({DutyCycle * 100:F0}% duty), " +
                     $"phase offset: {offset} frames");
        }
    }
    
    override public void InitFlashSequence()
    {
        // Handled in InitSequences()
    }
    
    private void LogSequences()
    {
        Debug.Log("=== Flash Sequences (first 30 frames) ===");
        foreach (var kvp in flashSequences)
        {
            int stimClass = kvp.Key;
            int[] sequence = kvp.Value;
            string seqStr = string.Join(",", sequence.Take(30));
            Debug.Log($"Stim{stimClass} ({frequencyMap[stimClass]}Hz): [{seqStr}...]");
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
        frameIndex = 0;
        lastFrameCount = -1;
        StartTime = 0; // Reset start time to delay start if needed
        isTraining = true;
        isApplication = false;
        inITI = false;
        
        StartTrial();
        
        Debug.Log($"SSVEP Training started: {Trials} trials, {TrialDuration}s per trial");
    }
    
    override public void StopTraining()
    {
        isTraining = false;
        currentState = SSVEPState.Idle;
        TurnOffAllStims();
        
        if (sendStartStopMarkers) Device.SendMarker(EndTrainingMk);
        Debug.Log($"Training completed: {currentTrial} trials finished");
    }
    
    override public void StartInference()
    {
        if (Stims.Count == 0)
        {
            Debug.LogError("No stimuli found! Cannot start application.");
            return;
        }
        
        if (sendStartStopMarkers) Device.SendMarker(ApplicationStartMk);
        
        frameIndex = 0;
        lastFrameCount = -1;
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
    }
    
    private void StartTrial()
    {
        // Cycle through stimuli in class order
        int stimIndex = currentTrial % stimClassOrder.Length;
        currentTrainingStimClass = stimClassOrder[stimIndex];
        
        trialStartTime = Time.unscaledTimeAsDouble;
        currentState = SSVEPState.Training;
        inITI = false;
        
        // Send marker with stimulus class
        if (sendStartStopMarkers) Device.SendMarker(currentTrainingStimClass);
        
        float freq = frequencyMap[currentTrainingStimClass];
        Debug.Log($"Trial {currentTrial + 1}/{Trials}: Stim{currentTrainingStimClass} ({freq}Hz) flashing for {TrialDuration}s");
    }
    
    private void ProcessTrainingMode()
    {
        if (inITI)
        {
            // In inter-trial interval
            double elapsed = Time.unscaledTimeAsDouble - itiStartTime;
            if (elapsed >= ITI)
            {
                // ITI complete, start next trial
                currentTrial++;
                if (currentTrial < Trials)
                {
                    StartTrial();
                }
                else
                {
                    StopTraining();
                }
            }
        }
        else
        {
            // In active trial
            double elapsed = Time.unscaledTimeAsDouble - trialStartTime;
            
            if (elapsed >= TrialDuration)
            {
                // Trial complete, start ITI
                TurnOffAllStims();
                inITI = true;
                itiStartTime = Time.unscaledTimeAsDouble;
                currentState = SSVEPState.TrainingITI;
                Debug.Log($"Trial {currentTrial + 1} complete, starting ITI ({ITI}s)");
            }
            else
            {
                // Update stimulus flash state
                UpdateStimulus(currentTrainingStimClass, frameIndex, phaseOffset: 0);
            }
        }
    }
    
    private void ProcessApplicationMode()
    {
        // Update all stimuli simultaneously with phase offsets
        foreach (var kvp in Stims)
        {
            int stimClass = kvp.Key;
            int phaseOffset = phaseOffsets[stimClass];
            UpdateStimulus(stimClass, frameIndex, phaseOffset);
        }
    }
    
    private void UpdateStimulus(int stimClass, int frame, int phaseOffset)
    {
        if (!Stims.ContainsKey(stimClass)) return;
        
        int[] sequence = flashSequences[stimClass];
        int adjustedFrame = (frame + phaseOffset) % sequence.Length;
        int state = sequence[adjustedFrame];
        
        NeuroStim stim = Stims[stimClass];
        
        // Debug logging (comment out for production)
        // if (frame % 60 == 0) // Log every 60 frames
        // {
        //     Debug.Log($"Frame {frame}: Stim{stimClass} state={state}");
        // }

        
        if (state == 1)
        {
            if (adjustedFrame == 0 && stimClass == currentTrainingStimClass)
            {
                // Cycle restart, we use the training stim to lock timing
                Debug.Log($"Stim{stimClass} cycle restart at frame {adjustedFrame} (Trial {currentTrial + 1})");
                Device.SendMarker(stimClass); 
            } else if (adjustedFrame == 0 && currentState == SSVEPState.Application && stimClass == 1)
            {
                // In application mode, send marker at cycle restart for all stims
                Debug.Log($"Stim{stimClass} cycle restart at frame {adjustedFrame}");
                Device.SendMarker(stimClass);
            }

            stim.StimOn(); // Uncomment when implementing visual feedback
        }
        else
        {
            stim.StimOff(); // Uncomment when implementing visual feedback
        }
    }
    
    private void TurnOffAllStims()
    {
        foreach (var kvp in Stims)
        {
            kvp.Value.StimOff(); // Uncomment when implementing visual feedback
        }
    }
    
    override public Dictionary<int, NeuroStim> GetStims()
    {
        return Stims;
    }
    
    override public void SetStims(List<NeuroStim> stims)
    {
        Stims.Clear();
        for (int i = 0; i < stims.Count; i++)
        {
            Stims.TryAdd(i + 1, stims[i]);
        }
        
        // Reinitialize after setting new stims
        InitFrequencies();
        InitSequences();
    }
}