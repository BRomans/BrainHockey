using UnityEngine;
using System;

namespace Gtec.UnityInterface
{
    [Serializable]
    public class FlashObject3D: NeurofeedbackTarget
    {
        [SerializeField]
        [Tooltip("The material when the object is flashing.")]
        protected Material _flashMaterial;

        [SerializeField]
        [Tooltip("The material when the object is not flashing.")]
        protected Material _darkMaterial;


        [SerializeField]
        [Tooltip("The index of the material to change.")]
        private uint _materialIndex;

        #region Public members

        public Material FlashMaterial
        {
            get { return _flashMaterial; }
            set { _flashMaterial = value; }
        }

        public Material DarkMaterial
        {
            get { return _darkMaterial; }
            set { _darkMaterial = value; }
        }

        public uint MaterialIndex
        {
            get { return _materialIndex; }
            set { _materialIndex = value; }
        }

        #endregion

        public FlashObject3D(int classId)
        {
            _classId = classId;
            
            
        }

        void Start()
        {
        
        }

        public override void SetStimulusOn()
        {
            Renderer renderer = GameObject.GetComponent<Renderer>();
            renderer.material = _flashMaterial;
            Material[] mTmp = GameObject.GetComponent<Renderer>().sharedMaterials;
            if (mTmp.Length <= _materialIndex)
                _materialIndex = (uint)mTmp.Length - 1;
            mTmp[_materialIndex] = _flashMaterial;
            renderer.sharedMaterials = mTmp;
        }

        public override void SetStimulusOff()
        {
            Renderer renderer = GameObject.GetComponent<Renderer>();
            renderer.material = _darkMaterial;
            Material[] mTmp = GameObject.GetComponent<Renderer>().sharedMaterials;
            if (mTmp.Length <= _materialIndex)
                _materialIndex = (uint)mTmp.Length - 1;
            mTmp[_materialIndex] = _darkMaterial;
            renderer.sharedMaterials = mTmp;
        }

        public override void CheckTargetValidity()
        {
            if (DarkMaterial == null || FlashMaterial == null || GameObject == null)
                throw new Exception("Gameobject, FlashMaterial, DarkMaterial must not be null.");
        }

    }
}