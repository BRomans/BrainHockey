using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    private Rect _rectTxt = new Rect(0, 0, 200, 80);
    private string _txt = "";

    void OnGUI()
    {
        try
        {
            GUI.TextArea(_rectTxt, _txt);
        }
        catch
        {
            //do nothing
        }
        
    }

    void Start()
    {

    }

    void Update()
    { 
        try
        {
            int fps = (int)(1.0f / Time.unscaledDeltaTime);
            _txt = string.Format("FPS: {0}\nScreen: {1}\nVSync Count: {2}", fps, Screen.currentResolution, QualitySettings.vSyncCount);
            GUI.TextArea(_rectTxt, _txt);
        }
        catch
        {
            // do nothing
        }
    }
}
