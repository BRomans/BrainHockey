using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotating : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The speed of rotation in degrees per second.")]
    private float rotationSpeed = 30f;

    [SerializeField]
    [Tooltip("The axis of rotation.")] 
    private Vector3 rotationAxis; 

    void Update()
    {
        // Rotate the object based on the specified speed and axis
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
    }
}
