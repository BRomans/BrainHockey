using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CortexSystem : MonoBehaviour
{
    

    public NeuroDevice.Device DeviceType = NeuroDevice.Device.LSL;
    public float confidenceThreshold = 0.95f;
    public TMPro.TextMeshProUGUI predictedClassText;
    public StimControllerBase[] Controllers;
    public int activeControllerIndex = 0; 
    public UnityEvent<double[], double> OnDataReceived;
    public UnityEvent<double[], double> OnQualityReceived;
    public UnityEvent<InferenceResult> OnInferenceReceived;
    public Queue<Action> EventsQueue;
    public bool IsInferenceRunning { get; private set; }
    public StimControllerBase ActiveController
    {
        get
        {
            if (Controllers == null || Controllers.Length == 0)
            {
                Debug.LogError("CortexSystem '" + name + "' has no Controllers assigned " +
                               "(is the button pointing at the scene instance?)", this);
                return null;
            }
            return Controllers[Mathf.Clamp(activeControllerIndex, 0, Controllers.Length - 1)];
        }
    }

    // Private members
    private  NeuroDevice Device;

    
    public CortexSystem()
    {
        EventsQueue = new Queue<Action>();
    }
    // Start is called before the first frame update
    void Start()
    {
        switch (DeviceType)
        {
            case NeuroDevice.Device.LSL:
                Device = new LSLDevice();
                break;
            case NeuroDevice.Device.USB:
                Device = new TinyBCILinkDevice();
                break;
            default:
                Debug.LogError("No device selected");
                break;
        }
        Device.OnDataReceived.AddListener(ReceiveEEGData);
        Device.OnQualityReceived.AddListener(ReceiveQuality);
        Device.OnInferenceReceived.AddListener(ReceiveInference);
        foreach (var controller in Controllers)
        {
            controller.Init(Device);
        }
        ApplyActiveController();
    }

    public void Update()
    {
        while (EventsQueue.Count > 0)
        {
            EventsQueue.Dequeue()?.Invoke();
        }
        Device.GetEEGDataStream();
        Device.GetQualityStream();
        Device.GetInferenceStream();
    }

    void ReceiveEEGData(double[] data, double timestamp)
    {
        //Debug.Log("Received sample: " + string.Join(", ", data));
        OnDataReceived.Invoke(data, timestamp);
    }

    void ReceiveQuality(double[] data, double timestamp)
    {
        //Debug.Log("Received quality: " + string.Join(", ", data));
        OnQualityReceived.Invoke(data, timestamp);
    }

    public void ReceiveInference(InferenceResult result)
    {
        Debug.Log("Received inference: " + string.Join(", ", result.selectedClass));
        int selectedClass = result.selectedClass;
        float confidence = result.confidence;
        double timestamp = result.timestamp;
        if (predictedClassText)
            predictedClassText.text = selectedClass.ToString();        
        int classId = selectedClass + 1;
        EventsQueue.Enqueue(() => OnInferenceReceived.Invoke(result));
        try
        {
            StimControllerBase stimController = ActiveController;
            foreach (KeyValuePair<int, NeuroStim> kvp in stimController.GetStims())
            {
                NeuroStim target = kvp.Value;
                if (kvp.Key == classId && confidence >= confidenceThreshold)
                    target.ClassFocused(classId, EventsQueue);
                else
                    target.ClassReleased(EventsQueue);
            }
        } catch (Exception e)
        {
            Debug.LogError("Error: " + e.Message);
        }
    }

    public void Connect()
    {
        Device.Connect();
    }

    public void Disconnect()
    {
        Device.Disconnect();
    }

    public void SendMarker(int marker)
    {
        Device.SendMarker(marker);
    }
    
    public void SetLocalSide(int side)
    {
        if (Controllers == null || Controllers.Length == 0) return;
        int index = Mathf.Clamp(side - 1, 0, Controllers.Length - 1);
        if (index == activeControllerIndex && Device != null) return;

        bool wasRunning = IsInferenceRunning;
        if (wasRunning) StopInference();        // stop the old side's flicker

        activeControllerIndex = index;
        ApplyActiveController();
        var active = ActiveController;
        if (Device != null && active != null) active.Reinitialize();

        if (wasRunning) StartInference();       // continue on the new side
    }
    
    private void ApplyActiveController()
    {
        // disabled controllers don't Update -> no flicker, no markers from the other side
        if (Controllers == null) return;
        for (int i = 0; i < Controllers.Length; i++)
            Controllers[i].enabled = i == activeControllerIndex;
    }
    
    public void StartTraining()  { ActiveController.StartTraining(); }
    public void StopTraining()   { ActiveController.StopTraining(); }
    public void StartInference()
    {
        if (IsInferenceRunning) return;
        var c = ActiveController;
        if (c == null) return;
        c.StartInference();
        IsInferenceRunning = true;
    }
    public void StopInference()
    {
        if (!IsInferenceRunning) return;
        var c = ActiveController;
        if (c != null) c.StopInference();
        IsInferenceRunning = false;
    }

}
