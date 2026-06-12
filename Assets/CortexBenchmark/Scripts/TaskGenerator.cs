using System.Collections.Generic;
using UnityEngine;


public class TaskGenerator : MonoBehaviour
{
    [SerializeField]
    protected StimControllerBase _controller;

    [SerializeField]
    [Tooltip("Performance calculator")]
    protected List<PerformanceMetrics> _performanceCalculators;

    [SerializeField]
    [Tooltip("The target to instantiate")]
    protected GameObject prefab; // The prefab to instantiate

    [SerializeField]
    [Tooltip("Input classes from UI")]
    protected TMPro.TMP_InputField _inputClasses;

    [SerializeField]
    [Tooltip("Number of elements in the grid")]
    protected int _classes = 4; // Number of rows in the grid

    [SerializeField]
    [Tooltip("Time of focus on the target to trigger OnActivated event")]
    protected float _focusTime = 0.0f;

    [SerializeField]
    [Tooltip("Spacing between objects")]
    protected float spacing = 2f; // Spacing between objects

    [SerializeField]
    [Tooltip("Number of columns in the grid")]
    protected int columns = 4; // Number of columns in the grid

    [SerializeField]
    [Tooltip("Cue prefab to be moved")]
    protected GameObject cuePrefab; // Cue prefab to be moved

    protected int rows = 1; // Number of rows in the grid
    protected Transform cueTransform; // Transform of the cue object
    protected Vector3 cueStartPosition; // Initial position of the cue object
    public int currentElementIndex = 0; // Index of the current element in the grid

    protected List<GameObject> anchors = new List<GameObject>(); // List of anchors for the target object

    protected GameObject gridContainer;

    protected bool _itrTimer = false;

    protected bool _isSelecting = false;

    protected int _lastElementClass = 0;

    protected int _nTrials;

    protected int _diffBetweenCueAndTarget = 1;

    public List<NeuroStim> Targets { get; private set; } = new List<NeuroStim>();

    void Start()
    {

    }

    public void GenerateTask()
    {
        if (_inputClasses != null && _inputClasses.text != "")
        {
            _classes = int.Parse(_inputClasses.text);
        }
        currentElementIndex = 0;
        _nTrials = _classes;
        CleanGrid();
        GenerateGrid();
        CreateCueObject();

        if (_performanceCalculators.Count != 0)
        {
            _performanceCalculators.ForEach(calculator => calculator.Initialize(_classes, _nTrials));
        }
    }

    void CleanGrid()
    {
        if (gridContainer != null)
        {
            Destroy(gridContainer);
        }
        foreach (var anchor in anchors)
        {
            Destroy(anchor);
        }
        foreach (var target in Targets)
        {
            Destroy(target.gameObject);
        }
        anchors.Clear();
        Targets.Clear();
    }

    protected virtual void GenerateGrid()
    {
        rows = Mathf.CeilToInt((float)_classes / columns);
        gridContainer = new GameObject("GridContainer");

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                // Calculate the position for each element in the grid
                Vector3 position = new Vector3(col * spacing, row * (-spacing), 0);
                GameObject anchor = new GameObject("Anchor_" + (row * columns + col));
                anchor.transform.position = position;
                anchor.transform.parent = gridContainer.transform;
                anchors.Add(anchor);
            }
        }

        for (int i = 0; i < _classes; i++)
        {
            GameObject target = Instantiate(prefab, anchors[i].transform);
            target.transform.localPosition = Vector3.zero;
            target.transform.localScale = Vector3.one;
            target.transform.localRotation = Quaternion.identity;
            target.transform.parent = anchors[i].transform;
            NeuroStim taskTarget = target.GetComponent<NeuroStim>();
            //taskTarget.FocusActivationTime = _focusTime;
            taskTarget.Class = i + _diffBetweenCueAndTarget;
            Targets.Add(taskTarget);
            taskTarget.OnFocused.AddListener(() => SelectionTask(taskTarget.Class));
            taskTarget.OnReleased.AddListener(() => ReleaseSelection());
        }
        _lastElementClass = Targets[Targets.Count - 1].Class;

        gridContainer.transform.position = transform.position; // Set the position to the parent's position
        gridContainer.transform.parent = transform; // Make it a child of the current GameObject
        _controller.SetStims(Targets);
        _controller.Reinitialize();
    }

    protected void CreateCueObject()
    {
        // Instantiate the cue object at the position of the first element
        cueTransform = Instantiate(cuePrefab, gridContainer.transform).transform;
        cueStartPosition = cueTransform.position;
        MoveCueToElement(currentElementIndex);
    }


    protected void SelectionTask(int targetIndex)
    {
        if (!_itrTimer && targetIndex - _diffBetweenCueAndTarget == currentElementIndex)
        {
            _itrTimer = true;
            _performanceCalculators.ForEach(calculator => calculator.StartTimer());
        }

        Debug.Log("Target index: " + targetIndex + " Current index: " + currentElementIndex);

        if (!_isSelecting)
        {
            _isSelecting = true;

            if (targetIndex - _diffBetweenCueAndTarget == currentElementIndex)
            {
                _performanceCalculators.ForEach(calculator => calculator.AddCorrectlyClassified(targetIndex, currentElementIndex));

                if (targetIndex != _lastElementClass)
                {
                    // Move to the next element in the grid on click
                    currentElementIndex++;
                    MoveCueToElement(currentElementIndex);
                }
                else
                {
                    currentElementIndex = 0;
                    _itrTimer = false;
                    _performanceCalculators.ForEach(calculator => calculator.StopTimer());
                    _performanceCalculators.ForEach(calculator => calculator.RetrieveTaskScore());
                    _performanceCalculators.ForEach(calculator => calculator.Initialize(_classes, _nTrials));
                    MoveCueToElement(currentElementIndex);
                }
            }
            else
            {
                _performanceCalculators.ForEach(calculator => calculator.AddMissClassified(targetIndex, currentElementIndex));
            }
            
        }
    }

    public void ReleaseSelection()
    {
        _isSelecting = false;
    }
    public void MoveCueToElement(int index)
    {
        // Calculate the position of the current element based on its index
        Vector3 targetPosition = anchors[index].transform.localPosition;
        targetPosition = new Vector3(targetPosition.x, targetPosition.y, 0.01f);
        // Move the cue object to the target position
        cueTransform.localPosition = targetPosition;

    }
}
