using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class P300Controller : StimControllerBase
{
    [Header("P300 Settings")]
    [SerializeField]
    [Tooltip("Duration of stimulus ON time in milliseconds")]
    public float OnDuration = 150.0f;
    [SerializeField]
    [Tooltip("Interval between stimuli in milliseconds")]
    public float ISI = 100f;
    [SerializeField]
    [Tooltip("Interval between trials in milliseconds")]
    public float ITI = 1000f;

    [SerializeField]
    [Tooltip("Duration of stimulus ON time in milliseconds")]
    public int nFlashesPerTrial = 5; // 1 target + 4 non-target

    // Markers
    private int StartTrainingMk = 98;
    private int EndTrainingMk = 99;
    private int TargetMk = 1;
    private int NonTargetMk = 2;

    // State tracking
    private bool isRunning = false;
    private int currentTrial = 0;
    private int currentFlash = 0;
    private int sequenceIndex = 0;

    // Frame validation
    private int lastFrameCount = -1;
    private int framesSinceSequenceStart = 0;

    // Sequences
    private int[] flashSequence; // [1,1,1,1,1,1,0,0,0,0,0,0,0,0,0,0] pattern for one flash
    private int[] itiSequence; // [0,0,0,0,0,0,0,0,0,0,0,0] pattern for ITI
    private int[] trialStimuli; // Which stimuli IDs flash in current trial [3, 1, 5, 2, 4]
    private int[] trialLabels; // 1=target, 2=non-target for each flash
    private int targetStimIndex; // Which stim is target this trial

    // Timing validation
    private double flashStartTime = 0;
    private int expectedFlashFrames = 0;
    private int expectedISIFrames = 0;
    private int expectedITIFrames = 0;

    // Stimuli dictionary
    private Dictionary<int, NeuroStim> Stims = new Dictionary<int, NeuroStim>();

    // Current state enum
    private enum P300State
    {
        Idle,
        FlashOn,
        ISI,
        ITI
    }
    private P300State currentState = P300State.Idle;

    void Start()
    {
        // Initialization handled in Init()
    }

    void Update()
    {
        if (!isRunning) return;

        if (StartTime == 0)
            StartTime = Time.unscaledTimeAsDouble;

        // Frame drop detection
        if (Time.frameCount != lastFrameCount + 1 && lastFrameCount != -1)
        {
            int droppedFrames = Time.frameCount - lastFrameCount - 1;
            Debug.LogWarning($"Frame drop detected! Dropped {droppedFrames} frame(s). " +
                           $"Trial: {currentTrial}, Flash: {currentFlash}, State: {currentState}");
        }
        lastFrameCount = Time.frameCount;

        // Execute state machine
        if (Time.unscaledTimeAsDouble - StartTime > StartDelay)
        {
            switch (currentState)
            {
                case P300State.FlashOn:
                    ProcessFlashOn();
                    break;
                case P300State.ISI:
                    ProcessISI();
                    break;
                case P300State.ITI:
                    ProcessITI();
                    break;
            }
        }
    }

    override public void Init(NeuroDevice device)
    {
        Device = device;
        InitStims();
        InitSequences();

        Debug.Log($"P300 Controller initialized with {nClasses} stimuli");
        Debug.Log($"Flash sequence length: {flashSequence.Length} frames");
        Debug.Log($"ISI sequence length: {(flashSequence.Length - expectedFlashFrames)} frames");
        Debug.Log($"ITI sequence length: {itiSequence.Length} frames");
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

        Debug.Log($"Initialized {Stims.Count} stimuli");
    }

    override public void InitFlashSequence()
    {
        float frameRate = (float)Screen.currentResolution.refreshRateRatio.value;

        // Calculate flash sequence (ON + ISI)
        expectedFlashFrames = Mathf.RoundToInt((OnDuration / 1000.0f) * frameRate);
        expectedISIFrames = Mathf.RoundToInt((ISI / 1000.0f) * frameRate);
        int totalFlashCycleFrames = expectedFlashFrames + expectedISIFrames;

        flashSequence = new int[totalFlashCycleFrames];
        for (int i = 0; i < totalFlashCycleFrames; i++)
        {
            flashSequence[i] = (i < expectedFlashFrames) ? 1 : 0;
        }

        // Calculate ITI sequence
        expectedITIFrames = Mathf.RoundToInt((ITI / 1000.0f) * frameRate);
        itiSequence = new int[expectedITIFrames];
        // ITI is all zeros (no stimulation)

        // Validation logging
        float actualOnDuration = (expectedFlashFrames / frameRate) * 1000f;
        float actualISI = (expectedISIFrames / frameRate) * 1000f;
        float actualITI = (expectedITIFrames / frameRate) * 1000f;

        Debug.Log($"Timing validation at {frameRate}Hz:");
        Debug.Log($"  OnDuration: requested {OnDuration}ms, actual {actualOnDuration:F2}ms ({expectedFlashFrames} frames)");
        Debug.Log($"  ISI: requested {ISI}ms, actual {actualISI:F2}ms ({expectedISIFrames} frames)");
        Debug.Log($"  ITI: requested {ITI}ms, actual {actualITI:F2}ms ({expectedITIFrames} frames)");

        if (Mathf.Abs(actualOnDuration - OnDuration) > 5f)
        {
            Debug.LogWarning($"OnDuration timing error > 5ms! Requested: {OnDuration}ms, Actual: {actualOnDuration:F2}ms");
        }
    }

    private void InitSequences()
    {
        InitFlashSequence();
    }

    override public void StartTraining()
    {
        foreach (var stim in Stims.Values)
        {
            stim.StimOff();
        }

        Device.SendMarker(StartTrainingMk);

        currentTrial = 0;
        currentFlash = 0;
        sequenceIndex = 0;
        isRunning = true;
        lastFrameCount = -1;
        framesSinceSequenceStart = 0;

        InitializeTrial();
        StartFlash();

        Debug.Log($"Training started: {Trials} trials, {nFlashesPerTrial} flashes per trial");
    }

    override public void StopTraining()
    {
        isRunning = false;
        currentState = P300State.Idle;
        Device.SendMarker(EndTrainingMk);

        Debug.Log($"Training completed: {currentTrial} trials finished");
    }

    private void InitializeTrial()
    {
        // Select random target
        targetStimIndex = targetStimIndex = TrainingStim.Class;

        // Select non-targets (all stimuli except target)
        List<int> nonTargets = new List<int>();
        for (int i = 1; i <= nClasses; i++)
        {
            if (i != targetStimIndex)
                nonTargets.Add(i);
        }

        // Shuffle and take first (nFlashesPerTrial - 1) non-targets
        nonTargets = nonTargets.OrderBy(x => UnityEngine.Random.value).ToList();
        List<int> selectedNonTargets = nonTargets.Take(nFlashesPerTrial - 1).ToList();
        for (int i = 0; i < nFlashesPerTrial - 1; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, nonTargets.Count);
            selectedNonTargets.Add(nonTargets[randomIndex]);
        }

        // Create trial stimuli array and labels
        trialStimuli = new int[nFlashesPerTrial];
        trialLabels = new int[nFlashesPerTrial];

        trialStimuli[0] = targetStimIndex;
        trialLabels[0] = TargetMk;

        for (int i = 0; i < selectedNonTargets.Count - 1; i++)
        {
            trialStimuli[i + 1] = selectedNonTargets[i];
            trialLabels[i + 1] = NonTargetMk;
        }

        // Randomize presentation order
        ShuffleTrialOrder();

        currentFlash = 0;

        Debug.Log($"Trial {currentTrial + 1}/{Trials} initialized. Target: Stim{targetStimIndex}");
        Debug.Log($"  Flash order: [{string.Join(", ", trialStimuli.Select((s, i) => $"Stim{s}({(trialLabels[i] == TargetMk ? "T" : "NT")})"))}]");
    }

    private void ShuffleTrialOrder()
    {
        // Fisher-Yates shuffle
        System.Random rng = new System.Random();
        int n = trialStimuli.Length;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);

            // Swap stimuli
            int tempStim = trialStimuli[k];
            trialStimuli[k] = trialStimuli[n];
            trialStimuli[n] = tempStim;

            // Swap labels
            int tempLabel = trialLabels[k];
            trialLabels[k] = trialLabels[n];
            trialLabels[n] = tempLabel;
        }
    }

    private void StartFlash()
    {
        currentState = P300State.FlashOn;
        sequenceIndex = 0;
        flashStartTime = Time.unscaledTimeAsDouble;
        framesSinceSequenceStart = 0;

        int stimID = trialStimuli[currentFlash];
        int label = trialLabels[currentFlash];

        // Send marker
        Device.SendMarker(label);

        // Log flash event
        string labelStr = (label == TargetMk) ? "TARGET" : "NON-TARGET";
        Debug.Log($"[T{currentTrial + 1}|F{currentFlash + 1}/{nFlashesPerTrial}] Flash ON: Stim{stimID} ({labelStr}) at frame {Time.frameCount}");
    }

    private void ProcessFlashOn()
    {
        if (sequenceIndex < flashSequence.Length)
        {
            // Still in flash sequence (ON or ISI part)
            if (flashSequence[sequenceIndex] == 1 && ComputeIndex() == 0)
            {
                // Flash is ON - log for debugging (in real implementation, turn on stimulus)
                Stims[trialStimuli[currentFlash]].StimOn();
            }
            else if (sequenceIndex == expectedFlashFrames)
            {
                // Transition to ISI
                Stims[trialStimuli[currentFlash]].StimOff();
                currentState = P300State.ISI;
                Debug.Log($"[T{currentTrial + 1}|F{currentFlash + 1}] Flash OFF, ISI started at frame {Time.frameCount}");
            }

            sequenceIndex++;
            framesSinceSequenceStart++;
        }
        else
        {
            // Flash sequence complete, move to next flash or ITI
            ValidateFlashTiming();
            AdvanceToNextFlash();
        }
    }

    private void ProcessISI()
    {
        // ISI is part of flashSequence (the zeros after the ones)
        // This state is just for clearer logging
        if (sequenceIndex < flashSequence.Length)
        {
            sequenceIndex++;
            framesSinceSequenceStart++;
        }
        else
        {
            ValidateFlashTiming();
            AdvanceToNextFlash();
        }
    }

    private void ProcessITI()
    {
        if (sequenceIndex < itiSequence.Length)
        {
            sequenceIndex++;
        }
        else
        {
            // ITI complete, start next trial
            ValidateITITiming();
            AdvanceToNextTrial();
        }
    }

    private void ValidateFlashTiming()
    {
        double actualDuration = (Time.unscaledTimeAsDouble - flashStartTime) * 1000.0;
        double expectedDuration = (OnDuration + ISI);
        double timingError = actualDuration - expectedDuration;

        if (Mathf.Abs((float)timingError) > 5f)
        {
            Debug.LogWarning($"Flash timing error: {timingError:F2}ms (expected {expectedDuration}ms, got {actualDuration:F2}ms, {framesSinceSequenceStart} frames)");
        }
    }

    private void ValidateITITiming()
    {
        double actualDuration = (Time.unscaledTimeAsDouble - flashStartTime) * 1000.0;
        double expectedDuration = ITI;
        double timingError = actualDuration - expectedDuration;

        if (Mathf.Abs((float)timingError) > 5f)
        {
            Debug.LogWarning($"ITI timing error: {timingError:F2}ms (expected {expectedDuration}ms, got {actualDuration:F2}ms)");
        }
    }

    private void AdvanceToNextFlash()
    {
        currentFlash++;

        if (currentFlash < nFlashesPerTrial)
        {
            // More flashes in this trial
            StartFlash();
        }
        else
        {
            // Trial complete, start ITI
            Debug.Log($"[T{currentTrial + 1}] Trial complete, starting ITI");
            currentState = P300State.ITI;
            sequenceIndex = 0;
            flashStartTime = Time.unscaledTimeAsDouble;
        }
    }

    private void AdvanceToNextTrial()
    {
        currentTrial++;

        if (currentTrial < Trials)
        {
            // Start next trial
            InitializeTrial();
            StartFlash();
        }
        else
        {
            // All trials complete
            StopTraining();
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
    }

    private int ComputeIndex(string syncMode = "timestamp")
    {
        return framesSinceSequenceStart % flashSequence.Length;
    }

    override public void StartInference()
    {
        Debug.LogWarning("Application mode not yet implemented for P300");
    }

    override public void StopInference()
    {
        Debug.LogWarning("Application mode not yet implemented for P300");
    }
}