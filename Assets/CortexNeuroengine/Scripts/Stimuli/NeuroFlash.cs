using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class NeuroFlash : NeuroStim
{
    public Sprite OnMaterial;
    public Sprite OffMaterial;
    public bool Rotation;
    private Vector3 RotationVector;
    private System.Random RotationAngle;

    void Start()
    {
        RotationVector = new Vector3(0, 0, 0);
        RotationAngle = new System.Random();
    }
    public override void StimOn()
    {
        GetComponent<SpriteRenderer>().sprite = OnMaterial;
        if (Rotation)
        {
            GetComponent<SpriteRenderer>().transform.Rotate(RotationVector, RotationAngle.Next(0, 360));
        }
    }

    public override void StimOff()
    {
        GetComponent<SpriteRenderer>().sprite = OffMaterial;
    }
}
