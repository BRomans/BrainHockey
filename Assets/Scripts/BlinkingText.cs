using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlinkingText : MonoBehaviour
{
    public float blinkSpeed = 1f;
    private TMPro.TextMeshProUGUI text;

    private bool visible = true;
    // Start is called before the first frame update
    void Start()
    {
        text = GetComponent<TMPro.TextMeshProUGUI>();
    }

    // Update is called once per frame
    void Update()
    {
        if (visible)
        {
            text.color = new Color(text.color.r, text.color.g, text.color.b, Mathf.PingPong(Time.time * blinkSpeed, 1));
        }
        else
        {
            text.color = new Color(text.color.r, text.color.g, text.color.b, 0);
        }
    }

    public void ChangeVisibility()
    {
        visible = !visible;
    }
}
