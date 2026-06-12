using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class NeuroSound : NeuroStim
{
    [Header("Audio Settings")]

    [SerializeField]
    [Tooltip("Audio clip to play when stimulus is on")]
    private AudioClip audioClip;
    [SerializeField]
    [Tooltip("Volume of the audio clip")]
    private float maxVolume = 1.0f;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        if (audioClip != null)
            audioSource.clip = audioClip;
        else
            Debug.LogError("AudioClip is not assigned in NeuroSound component.");
    }
    public override void StimOn()
    {
        if (audioSource != null)
        {
            Debug.Log("Playing audio clip for stimulus class " + Class);
            // Increase volume
            audioSource.volume = maxVolume;
            audioSource.Play();
        }
    }

    public override void StimOff()
    {
        if (audioSource != null)
        {
            // Decrease volume to prevent clicks
            audioSource.volume = 0f;
            audioSource.Stop();
        }
    }

    public void PreviewSound()
    {
        StimOn();
        Invoke("StimOff", 1.0f); // Stop after 1 second
    }
}

