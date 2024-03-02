using System;
using System.Collections.Generic;
using Gtec.Chain.Common.Templates.Utilities;
using UnityEngine;

namespace Gtec.UnityInterface
{
    public class DataLostController : MonoBehaviour
    {
        public Color BackgroundColor;
        public Color ForegroundColor;

        private bool _update;
        private int _updateCnt;
        private SpriteRenderer _background;
        private List<SpriteRenderer> _foreground;
        void Start()
        {
            _foreground = new List<SpriteRenderer>();
            SpriteRenderer[] sprites = gameObject.GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer sprite in sprites)
            {
                if (sprite.name.Contains("background"))
                    _background = sprite;
                if (sprite.name.Contains("pt"))
                    _foreground.Add(sprite);
            }

            _background.color = BackgroundColor;
            foreach (SpriteRenderer foregroundElement in _foreground)
                foregroundElement.color = ForegroundColor;

            _background.gameObject.SetActive(false);

            _update = false;
            _updateCnt = 0;
        }

        public void UpdateDataLost(DataEventArgs e)
        {
            _update = true;
        }

        void Update()
        {
            if (_update)
            {
                if (!_background.gameObject.activeSelf)
                    _background.gameObject.SetActive(true);

                _updateCnt++;
                if (_updateCnt > 20)
                {
                    _background.gameObject.SetActive(false);
                    _updateCnt = 0;
                    _update = false;
                }
            }
        }
    }
}