using System.Collections;
using System.Collections.Generic;
using Gtec.UnityInterface;
using UnityEngine;

public class SQBarSelector : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Signal quality controller for the Unicorn")]
    private SQController unicornSQ;

    [SerializeField]
    [Tooltip("Signal quality controller for the UnicornHeadband")] 
    private SQController unicornHeadbandSQ;

    public void ActivateDevice(string serial)
    {
        if(serial.Contains("UN"))
        {
            unicornSQ.gameObject.SetActive(true);
            unicornHeadbandSQ.gameObject.SetActive(false);
        }
        else if(serial.Contains("UH"))
        {
            unicornSQ.gameObject.SetActive(false);
            unicornHeadbandSQ.gameObject.SetActive(true);
        }
        
    }
}
