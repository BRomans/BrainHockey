using UnityEngine;
using System;
using System.Collections;

namespace Gtec.UnityInterface
{
    public class MotionObject2D : FlashObject2D
    {

        public enum Axis
        {
            X,
            Y,
            Z
        }

        [SerializeField]
        [Tooltip("Toggle the visual flash.")]
        private bool _visualFlash = false;

        [SerializeField]
        [UnicornRange(0.1f, 2f)]
        [Tooltip("The amplitude of the oscillation.")]
        private float _oscillationAmplitude = 2.0f;  // Distance to move up and down

        [SerializeField]
        [UnicornRange(1, 40)]
        [Tooltip("The frequency of the oscillation.")]
        private float _oscillationSpeed = 2.0f;     // Speed of the movement

        [SerializeField]
        private Axis _oscillationAxis = Axis.Y;

        [SerializeField]
        [Tooltip("Invert the direction of the oscillation.")]
        private bool _invertDirection = false;

        private Vector3 _originalPosition;
        private Vector3 targetPosition;

        private Vector3 targetOppositePosition;

        public MotionObject2D(int classId) : base(classId)
        {
            _classId = classId;
        }

        public float OscillationAmplitude
        {
            get { return _oscillationAmplitude; }
            set { _oscillationAmplitude = value; }
        }

        public float OscillationSpeed
        {
            get { return _oscillationSpeed; }
            set { _oscillationSpeed = value; }
        }

        public Axis OscillationAxis
        {
            get { return _oscillationAxis; }
            set { _oscillationAxis = value; }
        }

        void Start()
        {
            // Record the original position of the object
            _originalPosition = transform.localPosition;

            // Calculate the target position based on the move distance
            float amplitudeX = _oscillationAxis == Axis.X ? _oscillationAmplitude : 0;
            float amplitudeY = _oscillationAxis == Axis.Y ? _oscillationAmplitude : 0;
            float amplitudeZ = _oscillationAxis == Axis.Z ? _oscillationAmplitude : 0;

            targetPosition = _invertDirection ? _originalPosition + new Vector3(amplitudeX, amplitudeY, amplitudeZ) : _originalPosition - new Vector3(amplitudeX, amplitudeY, amplitudeZ);

        }

        IEnumerator Oscillate()
        {

            // Move up
            yield return MoveToTargetPosition(targetPosition);

            // Move down
            yield return MoveToTargetPosition(_originalPosition);

        }

        IEnumerator MoveToTargetPosition(Vector3 target)
        {
            while (Vector3.Distance(transform.localPosition, target) > 0.01f)
            {
                float step = _oscillationSpeed * Time.deltaTime;
                transform.localPosition = Vector3.Lerp(transform.position, target, step);
                yield return null;
            }
        }

        public override void SetStimulusOn()
        {
            if(_visualFlash)
                base.SetStimulusOn();
            // Start the coroutine for smooth up-down motion
            StartCoroutine(Oscillate());
        }

        public override void SetStimulusOff()
        {
            //oscillation = false;
            //
            if(_visualFlash)
                base.SetStimulusOff();
            StopCoroutine(Oscillate());
        }

        public override void CheckTargetValidity()
        {
            if (_oscillationAmplitude == 0 || _oscillationSpeed == 0 || GameObject == null)
                throw new Exception("Gameobject, Amplitude, Frequence must not be null.");
            if(_visualFlash)
                base.CheckTargetValidity();
        }
    }
}