using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public abstract class NeuroStim : MonoBehaviour
{
    public UnityEvent OnFocused;
    public UnityEvent OnReleased;

    public int Class;

    [SerializeField]
    [Tooltip("Highlight object to show when the object is focused")]
    private GameObject SelectionHighlight;

    private bool currentlyFocused = false;

    public abstract void StimOn();
    public abstract void StimOff();

    public void Update()
    {
        if(SelectionHighlight != null)
        {
            SelectionHighlight.SetActive(currentlyFocused);
        }
    }
    public void ClassFocused(int predictedClass, Queue<System.Action> EventsQueue)
    {
        currentlyFocused = true;
        Debug.Log("Class " + predictedClass + " focused, event enqueued");
        EventsQueue.Enqueue(() => {OnFocused.Invoke();});
    }

    public void ClassReleased(Queue<System.Action> EventsQueue)
    {
        if(currentlyFocused)
        {
            EventsQueue.Clear();
            EventsQueue.Enqueue(() => {OnReleased.Invoke();});
        }    
        currentlyFocused = false;
    }

}
