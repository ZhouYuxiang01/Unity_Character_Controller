using UnityEngine;
using Animancer;
using Characters.Player.Data;

namespace Characters.Player.Expression
{
    /// <summary>
    /// Facial layer controller
    /// </summary>
    public class       FacialController
    {
        private readonly AnimancerLayer _layer;
        private readonly PlayerSO _config;
        private readonly PlayerRuntimeData _data;

        // current base expression
        private ClipTransition _currentBaseExpression;

        // whether a transient expression is playing
        private bool _isPlayingTransient;

        public FacialController(AnimancerComponent animancer, PlayerSO config, PlayerRuntimeData runtimeData = null)
        {
            _config = config;
            _data = runtimeData;

            // get Layer 2
            _layer = animancer.Layers[2];

            // set mask from Core module
            if (_config != null && _config.Core != null)
                _layer.Mask = _config.Core.FacialMask;

            // ensure layer weight
            _layer.Weight = 1f;

            // default base expression: Emj.BaseExpression > Core.BlinkAnim
            if (_config != null && _config.Emj != null && _config.Emj.BaseExpression != null && _config.Emj.BaseExpression.Clip != null)
                _currentBaseExpression = _config.Emj.BaseExpression;
            else if (_config != null && _config.Core != null)
                _currentBaseExpression = _config.Core.BlinkAnim;

            PlayBaseExpression();
        }

        /// <summary>
        /// Should be called each frame by PlayerController.
        /// Monitors RuntimeData intents and triggers facial expressions.
        /// </summary>
        public void Update()
        {
            if (_data == null || _config == null) return;
            if (_isPlayingTransient) return;
            if (_config.Emj == null) return;

            if (_data.WantsExpression1)
            {
                PlayTransientExpression(_config.Emj.SpecialExpression1, 0.1f);
            }
            else if (_data.WantsExpression2)
            {
                PlayTransientExpression(_config.Emj.SpecialExpression2, 0.1f);
            }
            else if (_data.WantsExpression3)
            {
                PlayTransientExpression(_config.Emj.SpecialExpression3, 0.1f);
            }
            else if (_data.WantsExpression4)
            {
                PlayTransientExpression(_config.Emj.SpecialExpression4, 0.1f);
            }
        }

        public void PlayTransientExpression(ClipTransition expressionClip, float fadeDuration = 0.1f)
        {
            if (expressionClip == null || expressionClip.Clip == null) return;

            _isPlayingTransient = true;

            var state = _layer.Play(expressionClip, fadeDuration);

            state.Events(this).OnEnd = () =>
            {
                _isPlayingTransient = false;
                PlayBaseExpression(0.2f);
            };
        }

        public void SetBaseExpression(ClipTransition newBaseExpression)
        {
            if (newBaseExpression == null) return;

            _currentBaseExpression = newBaseExpression;

            if (!_isPlayingTransient)
            {
                PlayBaseExpression(0.25f);
            }
        }

        public void PlayHurtExpression()
        {
            if (_config != null && _config.Core != null)
                PlayTransientExpression(_config.Core.HurtFaceAnim, 0.1f);
        }

        private void PlayBaseExpression(float fadeDuration = 0.25f)
        {
            if (_currentBaseExpression != null && _currentBaseExpression.Clip != null)
            {
                _layer.Play(_currentBaseExpression, fadeDuration);
            }
        }
    }
}
