using System;
using System.Collections.Generic;
using UnityEngine;
using static Gtec.Chain.Common.Nodes.InputNodes.ChannelQuality;

namespace Gtec.UnityInterface
{
    public class SQController : MonoBehaviour
    {
        public Color ChannelGood;
        public Color ChannelBad;

        private List<SpriteRenderer> _channelSprites;
        private int _numberOfChannels;
        private bool[] _channelBad;
        private bool _update;
        void Awake()
        {
            _channelSprites = new List<SpriteRenderer>();
            SpriteRenderer[] sprites = gameObject.GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer sprite in sprites)
            {
                if (sprite.name.Contains("ch"))
                    _channelSprites.Add(sprite);
            }

            _numberOfChannels = _channelSprites.Count;
            _channelBad = new bool[_numberOfChannels];
            for (int i = 0; i < _numberOfChannels; i++)
                _channelBad[i] = true;
            _update = true;
        }

        void Update()
        {
            if (_update)
            {
                for (int i = 0; i < _numberOfChannels; i++)
                {
                    if (_channelBad[i])
                        _channelSprites[i].color = ChannelBad;
                    else
                        _channelSprites[i].color = ChannelGood;
                }
                _update = false;
            }
        }


        public void UpdateChannelStates(List<ChannelStates> channelStates)
        {
            if (channelStates.Count != _numberOfChannels)
                _numberOfChannels = channelStates.Count;

            for (int i = 0; i < _numberOfChannels; i++)
            {
                if (channelStates[i] == ChannelStates.BadFloating || channelStates[i] == ChannelStates.BadGrounded)
                    _channelBad[i] = true;
                else
                    _channelBad[i] = false;
            }

            _update = true;
        }
    }
}