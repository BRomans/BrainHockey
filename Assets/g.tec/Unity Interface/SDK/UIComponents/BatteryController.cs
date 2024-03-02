using System;
using UnityEngine;

namespace Gtec.UnityInterface
{
    public class BatteryController : MonoBehaviour
    {
        private SpriteRenderer _spChg;
        private Tuple<float, float> _fullBattery = new Tuple<float, float>(0f, 1.6f);
        private Tuple<float, float> _halfBattery = new Tuple<float, float>(-0.4f, 0.8f);
        private Tuple<float, float> _lowBattery = new Tuple<float, float>(-0.8f, 0f);
        private float _batteryHeight = 0.6f;
        private float _currenBatteryLevel = 0.0f;
        private bool _update;
        void Start()
        {
            SpriteRenderer[] sprites = gameObject.GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer sprite in sprites)
            {
                if (sprite.gameObject.name.Equals("batChg"))
                    _spChg = sprite;
            }

            _spChg.transform.localPosition = new Vector3(_lowBattery.Item1, 0);
            _spChg.transform.localScale = new Vector3(_lowBattery.Item2, _batteryHeight);
            _update = true;
        }

        public void UpdateBatteryLevel(float batteryLevel)
        {
            _currenBatteryLevel = batteryLevel;
            _update = true;
        }


        void Update()
        {
            if (_update)
            {
                if (_currenBatteryLevel > 60)
                {
                    _spChg.transform.localPosition = new Vector3(_fullBattery.Item1, 0);
                    _spChg.transform.localScale = new Vector3(_fullBattery.Item2, _batteryHeight);
                }
                else if (_currenBatteryLevel <= 60 && _currenBatteryLevel >= 13)
                {
                    _spChg.transform.localPosition = new Vector3(_halfBattery.Item1, 0);
                    _spChg.transform.localScale = new Vector3(_halfBattery.Item2, _batteryHeight);
                }
                else
                {
                    _spChg.transform.localPosition = new Vector3(_lowBattery.Item1, 0);
                    _spChg.transform.localScale = new Vector3(_lowBattery.Item2, _batteryHeight);
                }

                _update = false;
            }
        }
    }
}