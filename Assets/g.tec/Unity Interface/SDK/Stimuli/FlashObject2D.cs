using UnityEngine;
using System;

namespace Gtec.UnityInterface
{
    [Serializable]
    public class FlashObject2D : NeurofeedbackTarget
    {
        [SerializeField]
        [Tooltip("The material when the object is flashing.")]
        protected Sprite _flashSprite;

        [SerializeField]
        [Tooltip("The material when the object is not flashing.")]
        protected Sprite _darkSprite;

        private UnityEngine.Vector3 _rotationVector;
        private System.Random _angle;


        #region Public members

        public Sprite FlashSprite
        {
            get { return _flashSprite; }
            set { _flashSprite = value; }
        }

        public Sprite DarkSprite
        {
            get { return _darkSprite; }
            set { _darkSprite = value; }
        }

        #endregion

        public FlashObject2D(int classId)
        {
            _classId = classId;
        }

        void Start()
        {
            _rotationVector = new UnityEngine.Vector3(0, 0, 1);
            _angle = new System.Random();
        }

        public override void SetStimulusOn()
        {
            SpriteRenderer renderer = GameObject.GetComponent<SpriteRenderer>();
            renderer.sprite = _flashSprite;
            if (Rotate)
                renderer.transform.Rotate(_rotationVector, _angle.Next(0, 360));
                                   
        }

        public override void SetStimulusOff()
        {
            SpriteRenderer renderer = GameObject.GetComponent<SpriteRenderer>();
            renderer.sprite = _darkSprite;
        }

        public override void CheckTargetValidity()
        {
            if (DarkSprite == null || FlashSprite == null || GameObject == null)
                throw new Exception("Gameobject, FlashSprite, DarkSprite must not be null.");
        }
    }

}