using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TVEPController : StimControllerBase
{
    [Header("TVEP Settings")]
    [SerializeField]
    [Tooltip("Duration of stimulus ON time in milliseconds")]
    public float OnDuration = 150.0f;
    private Dictionary<int, NeuroStim> Stims = new Dictionary<int, NeuroStim>();
    private int StartTrainingMk = 98;
    private int EndTrainingMk = 99;
    private int InferenceStartMk = 100;
    private int InferenceEndMk = 101;
    private Dictionary<int, int[]> Sequences;
    private Dictionary<int, int> PreviousClass;
    private int[] classLabels;
    private double PreviousTs = 0;
    private int ShiftSequence;
    private int FrameIndex = 0;
    private int TrialCounter = 0;
    private static int[] FlashSequence;
    private bool TrainingStarted = false;
    private bool InferenceMode = false;

    void Start()
    {

    }

    void Update()
    {
        if (StartTime == 0)
            StartTime = Time.unscaledTimeAsDouble;

        if (TrainingStarted && TrialCounter < Trials)
        {
            //wait for defined amount of time until the algorithm starts
            if (Time.unscaledTimeAsDouble - StartTime > StartDelay)
            {
                NeurofeedbackLoop(inferenceMode: false);
            }
        }
        else if (InferenceMode)
        {
            if (Time.unscaledTimeAsDouble - StartTime > StartDelay)
            {
                NeurofeedbackLoop(inferenceMode: true);
            }
        }
        else
        {
            TrainingStarted = false;
        }
    }

    public override void Init(NeuroDevice device)
    {
        Device = device;
        InitStims();
        InitFlashSequence();
        BuildSequences();
        SetTrainingStim(Stims[classLabels[0]]);
    }

    public override void Reinitialize()
    {
        Sequences = null;
        PreviousClass = null;

        InitFlashSequence();
        BuildSequences();
        SetTrainingStim(Stims[classLabels[0]]);
    }

    public override void InitStims()
    {
        List<NeuroStim> sceneStims = GameObject.FindGameObjectsWithTag("Stim").Select(x => x.GetComponent<NeuroStim>()).ToList();
        sceneStims.Sort((x, y) => x.Class.CompareTo(y.Class));

        if (nClasses < sceneStims.Count)
        {
            nClasses = sceneStims.Count;
            Debug.Log("Number of classes is less than the number of stims in the scene. Setting nClasses to " + nClasses);
        }

        classLabels = new int[nClasses];
        for (int i = 0; i < nClasses; i++)
        {
            classLabels[i] = i + 1;
        }
        for (int i = 0; i < sceneStims.Count; i++)
        {
            Stims.TryAdd(classLabels[i], sceneStims[i]);
        }
        foreach (KeyValuePair<int, NeuroStim> kvp in Stims)
        {
            NeuroStim stim = kvp.Value;
            Debug.Log("Class " + kvp.Key + " found." + stim.Class);
        }
        Debug.Log("Found " + Stims.Count + " stims in scene. Initialized " + nClasses + " classes.");
    }

    public override void InitFlashSequence()
    {
        float frameRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
        int onSlots = Mathf.Max(1, Mathf.RoundToInt(OnDuration / 1000f * frameRate));
        Debug.Log("OnDurationS: " + onSlots);
        int totalSlots = onSlots * nClasses;          
        Debug.Log("TotalDurationSlots: " + totalSlots);
        FlashSequence = new int[totalSlots];
        for (int c = 0; c < onSlots; c++) FlashSequence[c] = 1;
        ShiftSequence = onSlots;                       
        Debug.Log("Flash sequence:" + string.Join(",", FlashSequence));
    }

    public override void StartTraining()
    {
        if (sendStartStopMarkers) Device.SendMarker(StartTrainingMk);
        StartTime = 0;
        FrameIndex = 0;
        TrialCounter = 0;
        TrainingStarted = true;
        InferenceMode = false;
        TurnOffStims(); 
    }

    public override void StopTraining()
    {
        TrainingStarted = false;
        TrialCounter = 0;
        TrainingStim?.StimOff(); 
        Invoke("SendStopTrainingMarker", 1.0f);
    }

    public void SendStopTrainingMarker()
    {
        if (sendStartStopMarkers) Device.SendMarker(EndTrainingMk);
    }

    override public void StartInference()
    {
        if (sendStartStopMarkers) Device.SendMarker(InferenceStartMk);
        StartTime = 0;
        FrameIndex = 0;
        TrainingStarted = false;
        InferenceMode = true;
        
        TurnOffStims();
        foreach (int k in PreviousClass.Keys.ToList()) PreviousClass[k] = 0;
    }

    override public void StopInference()
    {
        InferenceMode = false;
        StartTime = 0;
        FrameIndex = 0;
        if (sendStartStopMarkers) Device.SendMarker(InferenceEndMk);
        TurnOffStims();
    }

    private void NeurofeedbackLoop(bool inferenceMode)
    {
        int index = ComputeIndex();
        
        if (index == 0 && FrameIndex > 0)
        {
            TrialCounter++;
            if (!inferenceMode && TrialCounter >= Trials)
            {
                StopTraining();          // ends cleanly: no extra marker, no extra flash
                return;
            }
        }

        if (index % ShiftSequence == 0)
        {
            //int id = (int)Math.Floor((double)(index / ShiftSequence) + 1);
            int id = (index / ShiftSequence) + 1;
            Device.SendMarker(id);

        }
        double ts = Time.unscaledTimeAsDouble;
        if (!inferenceMode)
        {
            int mClass = TrainingStim.Class;
            ToggleTrainingStim(index, mClass);
            //PreviousClass[mClass] = Sequences[mClass][index];
        }
        else
        {
            ToggleApplicationStim(index);
        }
        
        PreviousTs = ts;
        FrameIndex++;
    }

    private void ToggleTrainingStim(int index, int mClass)
    {
        if (Sequences[mClass][index] != 0)
        {
            TrainingStim.StimOn();
        }
        else
        {
            TrainingStim.StimOff();
        }
    }

    private void ToggleApplicationStim(int index)
    {
        foreach (KeyValuePair<int, NeuroStim> kvp in Stims)
        {
            int mClass = kvp.Key;
            NeuroStim stim = kvp.Value;
            int previous = PreviousClass[mClass];
            int current = Sequences[mClass][index];
            if (stim.gameObject.activeSelf && current != previous)
            {
                if (Sequences[mClass][index] == 0)
                    stim.StimOff();
                else
                    stim.StimOn();
            }
            PreviousClass[mClass] = Sequences[mClass][index];
        }
    }

    public override Dictionary<int, NeuroStim> GetStims()
    {
        return Stims;
    }

    private void TurnOffStims()
    {
        foreach (KeyValuePair<int, NeuroStim> kvp in Stims)
        {
            kvp.Value.StimOff();
        }
    }

    public override void SetStims(List<NeuroStim> stims)
    {
        Stims.Clear();
        nClasses = stims.Count;
        classLabels = new int[nClasses];
        for (int i = 0; i < nClasses; i++)
        {
            classLabels[i] = i + 1;
        }

        for (int i = 0; i < stims.Count; i++)
        {
            // Get Text field from inner child and change it to class label
            TMPro.TMP_Text textComponent = stims[i].GetComponentInChildren<TMPro.TMP_Text>();
            if (textComponent != null)
            {
                textComponent.text = (i + 1).ToString();
            }
            Stims.TryAdd(classLabels[i], stims[i]);
        }
    }

    private void BuildSequences()
    {
        Sequences = new Dictionary<int, int[]>();
        PreviousClass = new Dictionary<int, int>();
        for (int i = 0; i < nClasses; i++)
        {
            int shift = i * ShiftSequence;
            var seq = new int[FlashSequence.Length];
            for (int k = 0; k < seq.Length; k++)
                seq[(k + shift) % seq.Length] = FlashSequence[k];   // same rotation, no Array.Copy maths
            Sequences[i + 1] = seq;
            PreviousClass[i + 1] = 0;
        }
    }
    
    private void SetTrainingStim(NeuroStim stim)
    {
        TrainingStim = stim;
    }

    private int ComputeIndex(string syncMode = "timestamp")
    {
        return FrameIndex % FlashSequence.Length;
    }
}