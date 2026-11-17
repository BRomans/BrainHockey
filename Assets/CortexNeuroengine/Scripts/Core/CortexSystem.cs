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
    public UnityEvent<double[], double> OnDataReceived;
    public UnityEvent<double[], double> OnQualityReceived;
    public UnityEvent<InferenceResult> OnInferenceReceived;
    public Queue<Action> EventsQueue;
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
            StimControllerBase stimController = Controllers[0];
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

}
