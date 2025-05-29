using System.Collections.Generic;
using UnityEngine;
using Gtec.Benchmark;


public class TaskController : MonoBehaviour
{
    [SerializeField]
    private int nClasses;

    [SerializeField]
    public int nTrials = 2;

    [SerializeField]
    [Tooltip("Performance calculator")]
    protected List<PerformanceMetrics> _performanceCalculators;

    private int hitCounter = 0;

    private int currentClassId = 0;

    private bool timerStarted = false;

    void Start()
    {
        _performanceCalculators.ForEach(p => p.Initialize(nClasses, nTrials));
    }

    public void SetCurrentClassId(int classId)
    {
        currentClassId = classId;
    }

    public void BallHit(int cueId)
    {
        hitCounter++;
        _performanceCalculators.ForEach(p => p.AddCorrectlyClassified(cueId, currentClassId));
        if (!IsTaskRunning())
        {
            StopTaskTimer();
        }
    }

    public void BallMissed(int cueId)
    {
        hitCounter++;
        _performanceCalculators.ForEach(p => p.AddMissClassified(cueId, currentClassId));
        if (!IsTaskRunning())
        {
            StopTaskTimer();
        }
    }

    public void StartTaskTimer()
    {
        timerStarted = true;
        _performanceCalculators.ForEach(p => p.StartTimer());
    }

    public void StopTaskTimer()
    {
        hitCounter = 0;
        timerStarted = false;
        _performanceCalculators.ForEach(p => p.StopTimer());
        _performanceCalculators.ForEach(calculator => calculator.RetrieveTaskScore());
        _performanceCalculators.ForEach(calculator => calculator.Initialize(nClasses, nTrials));
    }

    public bool IsTaskRunning()
    {
        return hitCounter < nTrials && timerStarted;
    }
}