using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gtec.Benchmark
{
    public class PerformanceMetrics : MonoBehaviour
    {
        public class TaskScore
        {
            public int _nClasses;
            public int _correctlyClassified;
            public int _missClassified;
            public double _itr;
            public double _accuracy;
            public DateTime _startTime;
            public DateTime _stopTime;
            public double _elapsedTime;
        }

        private double _lastITR;
        private double _lastAccuracy;
        private List<TaskScore> _scores;
        private double _taskStartTime;
        private DateTime _taskStartTimestamp;

        private DateTime _taskStopTimestamp;
        private double _stopTime;
        private int _classes;
        private int _nTrials;
        private int _correctlyClassified = 0;
        private int _missClassified = 0;
        private DataWriter _dataWriter;

        [SerializeField]
        [Tooltip("Save data to file")]
        protected bool _saveData = false;

        [SerializeField]
        [Tooltip("Print the scores on the screen")]
        protected bool _printOnScreen = false;

        [SerializeField]
        [Tooltip("The number of scores to display on the screen")]
        protected int _scoresOnScreen = 3;

        [SerializeField]
        [Tooltip("The color of the GUI square.")]
        protected Color _backgroundColor = Color.black;

        [SerializeField]
        [Tooltip("The size of the GUI square.")]
        protected Vector2 _guiSquareSize = new Vector2(200, 200);

        [SerializeField]
        [Tooltip("The size of the GUI square.")]
        protected Vector2 _guiPosition = new Vector2(0, 0);

        [SerializeField]
        [Tooltip("The offset of the score text.")]
        protected Vector2 _scoreTextOffset = new Vector2(10, 10);

        public void Initialize(int classes, int nTrials)
        {
            _classes = classes;
            _nTrials = nTrials;
            _correctlyClassified = 0;
            _missClassified = 0;
        }

        void OnGUI()
        {
            try
            {
                GUI.backgroundColor = _backgroundColor;

                // Set the color for the text
                GUI.color = Color.white;

                // Display the ITRScores inside the GUI square
                foreach (TaskScore score in _scores)
                {

                    // Display each ITRScore as a text label inside the GUI square
                    string scoreText = $"Performance Score {_scores.IndexOf(score) + 1}:\n" +
                                       $"Start at: {score._startTime}\n" +
                                       $"Stop at: {score._stopTime}\n" +
                                       $"Classes: {score._nClasses}\n" +
                                       $"Correct: {score._correctlyClassified}\n" +
                                       $"Missed: {score._missClassified}\n" +
                                       $"Accuracy: {score._accuracy:F2}\n" +
                                       $"ITR: {score._itr:F4}\n" +
                                       $"Completion Time: {score._elapsedTime:F2}s\n";
                    if (_printOnScreen)
                        GUI.TextArea(new Rect(_guiPosition.x, _guiPosition.y * (_scores.IndexOf(score) + 1), _guiSquareSize.x, _guiSquareSize.y), scoreText);

                }
            }
            catch
            {
                //do nothing
            }

        }


        public void AddCorrectlyClassified(int cueID, int classID)
        {
            _correctlyClassified++;
            _lastAccuracy = ComputeAccuracy();
            _lastITR = ComputeITR(Time.time, _taskStartTime);
            if (_dataWriter != null)
            {
                _dataWriter.WriteTaskData(DateTime.Now, _classes, cueID, classID);
            }
        }

        public void AddMissClassified(int cueID, int classID)
        {
            _missClassified++;
            if (_dataWriter != null)
            {
                _dataWriter.WriteTaskData(DateTime.Now, _classes, cueID, classID);
            }
        }

        public void StartTimer()
        {
            _taskStartTime = Time.time;
            _taskStartTimestamp = DateTime.Now;
            Debug.Log("Task started at: " + _taskStartTimestamp.ToString("yyyy-MM-dd_HH-mm-ss"));
        }

        public void StopTimer()
        {
            _stopTime = Time.time;
            _taskStopTimestamp = DateTime.Now;
            Debug.Log("Task stopped at: " + _taskStopTimestamp.ToString("yyyy-MM-dd_HH-mm-ss"));
        }

        private double ComputeAccuracy()
        {
            return (double)_correctlyClassified / (_correctlyClassified + _missClassified);
        }

        private double ComputeITR(double stopTime, double startTime)
        {
            Debug.Log(String.Format("Calculation on system timestamp {0}", _taskStopTimestamp - _taskStartTimestamp));
            double elapsedS = stopTime - startTime;
            double n = _classes;
            double p = (double)_correctlyClassified / (_correctlyClassified + _missClassified);
            double s = 60.0 * (_nTrials) / elapsedS;
            double b = 0;
            if (p > 0 && n > 0)
            {
                if ((1 - p) / (n - 1) > 0)
                    b = Math.Log(n, 2) + p * Math.Log(p, 2) + (1 - p) * Math.Log((1 - p) / (n - 1), 2);
                else
                    b = Math.Log(n, 2) + p * Math.Log(p, 2) + (1 - p) * 0;
            }

            double itr = b * s;

            return itr;
        }

        public void RetrieveTaskScore()
        {
            _lastAccuracy = ComputeAccuracy();
            _lastITR = ComputeITR(_stopTime, _taskStartTime);
            var _score = new TaskScore
            {
                _nClasses = _classes,
                _correctlyClassified = _correctlyClassified,
                _missClassified = _missClassified,
                _itr = _lastITR,
                _accuracy = _lastAccuracy,
                _startTime = _taskStartTimestamp,
                _stopTime = _taskStopTimestamp,
                _elapsedTime = _stopTime - _taskStartTime
            };
            if(_scores.Count > _scoresOnScreen)
                _scores.Clear();
            _scores.Add(_score);
            if (_dataWriter != null)
            {
                Debug.Log("Writing ITR score to file");
                _dataWriter.WritePerformanceScore(_score);
            }
        }

        void Start()
        {
            _scores = new List<TaskScore>();
            if (_saveData)
            {
                _dataWriter = GetComponent<DataWriter>();
                _dataWriter.Initialize(DataWriter.WriterType.PerformanceMetrics);
            }
        }
    }
}