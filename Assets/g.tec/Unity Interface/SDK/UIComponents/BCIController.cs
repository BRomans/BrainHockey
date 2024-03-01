using System;
using System.Collections.Generic;
using Gtec.Chain.Common.Templates.Utilities;
using Gtec.UnityInterface;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BCIController : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The BCI manager.")]
    private BCIManager _BCIManager;

    [SerializeField]
    [Tooltip("If the Unicorn Input simulator should be used.")]
    private bool _simulateInput = false;

    [SerializeField]
    [UnicornRange(0, 10)]
    [Tooltip("The duration of the simulated input in ms.")]
    private float simulatedInputDuration = 1;

    [SerializeField]
    [Tooltip("The dropdown list of available devices.")]
    private TMP_Dropdown _devices;

    [SerializeField]
    [Tooltip("The connect button.")]
    private Button _connectButton;

    [SerializeField]
    [Tooltip("The disconnect button.")]
    private Button _disconnectButton;

    [SerializeField]
    [Tooltip("The start training button.")]
    private Button _startTrainingButton;

    [SerializeField]
    [Tooltip("The stop training button.")]
    private Button _stopTrainingButton;

    [SerializeField]
    [Tooltip("The ON button to enter application mode.")]
    private Button _appOnButton;

    [SerializeField]
    [Tooltip("The OFF button to exit application mode.")]
    private Button _appOffButton;


    [SerializeField]
    [Tooltip("The button to show/hide the BCI controller.")]
    private Button _showHideButton;

    // Start is called before the first frame update
    void Start()
    {
        if (_BCIManager == null)
            _BCIManager = FindObjectOfType<BCIManager>();
        _connectButton.onClick.AddListener(OnBtnConnectClicked);
        _disconnectButton.onClick.AddListener(OnBtnDisconnectClicked);
        _startTrainingButton.onClick.AddListener(OnBtnStartTrainingClicked);
        _stopTrainingButton.onClick.AddListener(OnBtnStopTrainingClicked);
        _appOnButton.onClick.AddListener(OnBtnAppOnClicked);
        _appOffButton.onClick.AddListener(OnBtnAppOffClicked);
        _showHideButton.onClick.AddListener(OnBtnShowHideClicked);

        _disconnectButton.interactable = false;
        _startTrainingButton.interactable = false;
        _stopTrainingButton.interactable = false;
        _appOnButton.interactable = false;
        _appOffButton.interactable = false;

    }

    // Update is called once per frame
    void Update()
    {
        if (_simulateInput)
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
    }

    public void UpdateList(List<string> deviceList)
    {
        _devices.ClearOptions();
        if (deviceList != null && deviceList.Count > 0)
            _devices.AddOptions(deviceList);

    }

    public void OnBtnConnectClicked()
    {
        _BCIManager.Connect(_devices.options[_devices.value].text);
        _devices.interactable = false;
        _startTrainingButton.interactable = true;
        _disconnectButton.interactable = true;
        _connectButton.interactable = false;

    }

    public void OnBtnDisconnectClicked()
    {
        try
        {
            _BCIManager.Disconnect();
            _devices.interactable = true;
            _connectButton.interactable = true;
            _disconnectButton.interactable = false;
            _startTrainingButton.interactable = false;

        }
        catch (System.Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    public void OnBtnStartTrainingClicked()
    {
        try
        {
            _BCIManager.StartTrainingParadigm(0);
            _startTrainingButton.interactable = false;
            _stopTrainingButton.interactable = true;

        }
        catch (System.Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    public void OnBtnStopTrainingClicked()
    {
        try
        {
            _BCIManager.StopTrainingParadigm(0);
            _startTrainingButton.interactable = true;
            _stopTrainingButton.interactable = false;
        }
        catch (System.Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    public void OnBtnAppOnClicked()
    {
        try
        {
            _BCIManager.StartApplicationParadigm(0);
            _stopTrainingButton.interactable = false;
            _appOffButton.interactable = false;
            _startTrainingButton.interactable = false;
            _appOnButton.interactable = false;
            _appOffButton.interactable = true;
        }
        catch (System.Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    public void OnBtnAppOffClicked()
    {
        try
        {
            _BCIManager.StopApplicationParadigm(0);
            _stopTrainingButton.interactable = false;
            _appOffButton.interactable = false;
            _startTrainingButton.interactable = true;
            _appOnButton.interactable = true;
            _appOffButton.interactable = false;
        }
        catch (System.Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    public void OnParadigmStopped()
    {
        try
        {
            _stopTrainingButton.interactable = false;
            _appOffButton.interactable = false;
            _startTrainingButton.interactable = true;
            _appOnButton.interactable = true;
            _appOffButton.interactable = false;
        }
        catch (System.Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    public void OnBtnShowHideClicked()
    {
        try
        {
            _devices.gameObject.SetActive(!_devices.gameObject.activeSelf);
            _connectButton.gameObject.SetActive(!_connectButton.gameObject.activeSelf);
            _disconnectButton.gameObject.SetActive(!_disconnectButton.gameObject.activeSelf);
            _startTrainingButton.gameObject.SetActive(!_startTrainingButton.gameObject.activeSelf);
            _stopTrainingButton.gameObject.SetActive(!_stopTrainingButton.gameObject.activeSelf);
            _appOnButton.gameObject.SetActive(!_appOnButton.gameObject.activeSelf);
            _appOffButton.gameObject.SetActive(!_appOffButton.gameObject.activeSelf);

        }
        catch (System.Exception e)
        {
            Debug.Log(e.Message);
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
            NeurofeedbackTarget target = hit.collider.GetComponent<NeurofeedbackTarget>();
            if (target != null)
                ClassSelectionClick(target, release);
        }
        else
        {
            // If no 3D object is hit, check for 2D objects using Physics2D.Raycast
            RaycastHit2D hit2D = Physics2D.Raycast(ray.origin, ray.direction);


            if (hit2D.collider != null)
            {
                NeurofeedbackTarget target = hit2D.collider.GetComponent<NeurofeedbackTarget>();
                if (target != null)
                    ClassSelectionClick(target, release);
            }
        }
    }

    private void ClassSelectionClick(NeurofeedbackTarget target, bool release)
    {
        ClassSelection selectedClass = new ClassSelection
        {
            Class = target.ClassId,
            Confidence = 2.326348f,
            DurationMs = simulatedInputDuration * 1000
        };

        if (!release)
            Debug.Log("Simulated class selection:" + selectedClass.Class);
        else
        {
            Debug.Log("Simulated class release:" + selectedClass.Class);
            selectedClass.Confidence = 0.0f;
            selectedClass.DurationMs = 0;
        }

        if (target != null)
        {

            _BCIManager.InvokeClassSelection(selectedClass);

        }
    }
}
