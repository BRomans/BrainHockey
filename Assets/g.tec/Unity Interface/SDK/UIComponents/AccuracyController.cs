using System;
using System.Collections.Generic;
using UnityEngine;
using Gtec.Chain.Common.Algorithms;
using Gtec.Chain.Common.Templates.Utilities;

namespace Gtec.UnityInterface
{

    public class AccuracyController : MonoBehaviour
    {
        public Color NoClassifier;
        public Color VeryGoodClassifier;
        public Color GoodClassifier;
        public Color BadClassifier;

        private enum ClassifierAccuracyLabel { NA, VeryGood, Good, Bad }

        private bool _update;
        private ClassifierAccuracyLabel _classifierAccuracy;
        private SpriteRenderer _sprite;

        void Start()
        {
            _sprite = gameObject.GetComponent<SpriteRenderer>();
            _classifierAccuracy = ClassifierAccuracyLabel.NA;           
        }

        public void UpdateClassifierAccuracy(CalibrationResult calibrationResult)
        {
            var classifierAccuracy = calibrationResult.CalibrationQuality.ToString();
            switch (classifierAccuracy)
            {
                case "Good":
                    {
                        _classifierAccuracy = ClassifierAccuracyLabel.VeryGood;
                        break;
                    }
                case "Ok":
                    {
                        _classifierAccuracy = ClassifierAccuracyLabel.Good;
                        break;
                    }
                case "Bad":
                    {
                        _classifierAccuracy = ClassifierAccuracyLabel.Bad;
                        break;
                    }
                default:
                    {
                        _classifierAccuracy = ClassifierAccuracyLabel.NA;
                        break;
                    }
            }
            _update = true;
        }

        void Update()
        {
            if (_update)
            {
                switch (_classifierAccuracy)
                {
                    case ClassifierAccuracyLabel.NA:
                        {
                            _sprite.color = NoClassifier;
                            break;
                        }
                    case ClassifierAccuracyLabel.VeryGood:
                        {
                            _sprite.color = VeryGoodClassifier;
                            break;
                        }
                    case ClassifierAccuracyLabel.Good:
                        {
                            _sprite.color = GoodClassifier;
                            break;
                        }
                    case ClassifierAccuracyLabel.Bad:
                        {
                            _sprite.color = BadClassifier;
                            break;
                        }
                }
                _update = false;
            }
        }
    }
}