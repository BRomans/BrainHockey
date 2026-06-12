using UnityEngine;
using UnityEngine.UIElements;

public class FPS : MonoBehaviour
{
    private Rect FPSBackground = new Rect(0, 0, 40, 25);
    private Rect Background = new Rect(40, 0, 250, 25);
    private string fpsText = "";
    private string resText = "";
    private string vsyncText = "";

    void OnGUI()
    {
        try
        {
            resText = "" + Screen.currentResolution;
            vsyncText = "VSync: " + QualitySettings.vSyncCount;
            string fullText = resText + " " + vsyncText;
            GUI.TextArea(Background, fullText);

            int fps = Mathf.FloorToInt(1.0f / Time.unscaledDeltaTime);
            fpsText = fps.ToString();
            if (fps < 30)
            {
                GUI.color = Color.red;
            }
            else if (fps < 58)
            {
                GUI.color = Color.yellow;
            }
            else
            {
                GUI.color = Color.green;
            }
            GUI.TextArea(FPSBackground, fpsText);
           
        }
        catch
        {
            //do nothing
        }

    }
}
