using UnityEngine;
using System;

namespace Gtec.UnityInterface
{
    [Serializable]
    public class TonalAudioObject2D: FlashObject2D
    {
        [SerializeField]
        [Tooltip("Toggle the visual flash.")]
        private bool _visualFlash = false;

        [SerializeField]
        [Tooltip("The audio clip to play when the object is flashing.")]
        private AudioSource _audioSource;

        [SerializeField]
        [Tooltip("(Optional) The visual cue to show when the object is flashing.")]
        private GameObject _visualCue;

        #region Public members

        public AudioSource AudioSource
        {
            get { return _audioSource; }
            set { _audioSource = value; }
        }

        #endregion

        public TonalAudioObject2D(int classId) : base(classId)
        {
            _classId = classId;
            _audioSource.Stop();
        }

        void Start()
        {
        
        }

        public override void SetStimulusOn()
        {
            _audioSource.Play();
            if(_visualCue != null)
                _visualCue.SetActive(true);
            if(_visualFlash)
                base.SetStimulusOn();
        }

        public override void SetStimulusOff()
        {
            _audioSource.Stop();
            if (_visualCue != null)
                _visualCue.SetActive(false);
            if (_visualFlash)
                base.SetStimulusOff();
        }

        public override void CheckTargetValidity()
        {
            if (AudioSource == null || GameObject == null)
                throw new Exception("Gameobject, AudioSource, must not be null.");
            if(_visualFlash)
                base.CheckTargetValidity();
        }

    }
}