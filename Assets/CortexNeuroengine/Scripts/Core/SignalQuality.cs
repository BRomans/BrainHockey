using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class SignalQuality : MonoBehaviour
{

    public GameObject templateIndicator;
    public GameObject[] signalQualityIndicators;
    public Color goodQualityColor;
    public Color mediumQualityColor;
    public Color badQualityColor;
    

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetQualityIndicators(double[] quality, double timestamp)
    {
        if (signalQualityIndicators == null || signalQualityIndicators.Length != quality.Length)
        {
            signalQualityIndicators = new GameObject[quality.Length];
            float x_pos = -0.1f * (quality.Length);
            for (int i = 0; i < quality.Length; i++)
            {
                signalQualityIndicators[i] = Instantiate(templateIndicator, new Vector3(0, 0, 0), Quaternion.identity);
                signalQualityIndicators[i].transform.GetComponent<SpriteRenderer>().color = Color.white;
                signalQualityIndicators[i].transform.SetParent(this.transform);
                x_pos += 0.2f;
                signalQualityIndicators[i].transform.localPosition = new Vector3(x_pos, 0, 0);
            }
        }


        for (int i = 0; i < quality.Length; i++)
        {
            if (quality[i] == 1)
            {
                signalQualityIndicators[i].GetComponent<SpriteRenderer>().color = goodQualityColor;
            }
            else if (quality[i] == 0.5)
            {
                signalQualityIndicators[i].GetComponent<SpriteRenderer>().color = mediumQualityColor;
            }
            else
            {
                signalQualityIndicators[i].GetComponent<SpriteRenderer>().color = badQualityColor;
            }
        }
    }
}
