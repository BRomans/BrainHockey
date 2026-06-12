using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputSimulator : MonoBehaviour
{
    private CortexSystem _cortexSystem;

    // Start is called before the first frame update
    void Start()
    {
        _cortexSystem = FindObjectOfType<CortexSystem>();
        if (_cortexSystem == null)
        {
            Debug.LogError("CortexSystem not found in the scene.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            HandleClick(false);
        }
        if (Input.GetMouseButtonUp(0))
        {
            HandleClick(true);
        }
    }

    private void HandleClick(bool release)
    {
        // Cast a ray from the camera to the mouse position
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Check if the ray hits an object
        if (Physics.Raycast(ray, out hit))
        {
            // Check if the hit object has a ClickableObject script
            NeuroStim target = hit.collider.GetComponent<NeuroStim>();
            if (target != null)
                ClassSelectionClick(target, release);
        }
        else
        {
            // If no 3D object is hit, check for 2D objects using Physics2D.Raycast
            RaycastHit2D hit2D = Physics2D.Raycast(ray.origin, ray.direction);


            if (hit2D.collider != null)
            {
                NeuroStim target = hit2D.collider.GetComponent<NeuroStim>();
                if (target != null)
                    ClassSelectionClick(target, release);
            }
        }
    }

    private void ClassSelectionClick(NeuroStim target, bool release)
    {
        InferenceResult inferenceResult = new InferenceResult
        {
            selectedClass = target.Class,
            probabilities = new float[] { 0.0f, 0.0f, 0.0f, 0.0f },
            confidence = release ? 0.0f : 1.0f,
            timestamp = Time.time
        };

        if (!release)
            Debug.Log("Simulated class selection:" + inferenceResult.selectedClass);
        else
        {
            Debug.Log("Simulated class release:" + inferenceResult.selectedClass);
        }

        if (target != null && _cortexSystem != null)
        {
            _cortexSystem.ReceiveInference(inferenceResult);
        }
    }

}
