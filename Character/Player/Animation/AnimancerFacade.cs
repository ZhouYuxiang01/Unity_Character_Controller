// 文件路径: Characters/Player/Animation/AnimancerFacade.cs
using System.Collections.Generic;
using UnityEngine;
using Animancer;

namespace Characters.Player.Animation
{
    [RequireComponent(typeof(AnimancerComponent))]
    public class AnimancerFacade : MonoBehaviour, IAnimationFacade
    {
        private AnimancerComponent _animancer;

        // 用字典分层管理结束回调 彻底杜绝多层回调串线
        private Dictionary<int, System.Action> _layerOnEndActions = new Dictionary<int, System.Action>();

        private void Awake()
        {
            _animancer = GetComponent<AnimancerComponent>();
        }

        private void OnDisable()
        {
            // 清理所有层的回调
            foreach (var layerIndex in _layerOnEndActions.Keys)
            {
                ClearOnEndCallback(layerIndex);
            }
            _layerOnEndActions.Clear();
        }

        public void InitializeAnimancer(AnimancerComponent animancerComponent)
        {
            if (animancerComponent != null) _animancer = animancerComponent;
        }

        public void PlayClip(AnimationClip clip, AnimPlayOptions options)
        {
            if (clip == null) return;
            int layerIndex = options.Layer;

            ClearOnEndCallback(layerIndex);
            var layer = GetLayerOrFallback(layerIndex);

            var state = options.FadeDuration >= 0
                ? layer.Play(clip, options.FadeDuration)
                : layer.Play(clip);

            state.AssertOwnership(this);
            ApplyOptions(state, options);
            RebindOnEndIfNeeded(layerIndex, state);
        }

        public void PlayTransition(object transitionObj, AnimPlayOptions options)
        {
            var transition = transitionObj as ITransition;
            if (transition == null) return;

            int layerIndex = options.Layer;
            ClearOnEndCallback(layerIndex);

            var layer = GetLayerOrFallback(layerIndex);
            var state = options.FadeDuration >= 0
                ? layer.Play(transition, options.FadeDuration)
                : layer.Play(transition);

            state.AssertOwnership(this);
            ApplyOptions(state, options);
            RebindOnEndIfNeeded(layerIndex, state);
        }

        // ✨ 核心修复：精准获取目标层级的 CurrentState，而不是被全局变量误导！
        public void SetMixerParameter(Vector2 parameter, int layerIndex = 0)
        {
            var state = GetLayerOrFallback(layerIndex).CurrentState;
            if (state == null) return;

            if (state is MixerState<Vector2> mixer2D)
            {
                mixer2D.Parameter = parameter;
            }
            else if (state is MixerState<float> mixer1D)
            {
                mixer1D.Parameter = parameter.x;
            }
        }

        public void SetOnEndCallback(System.Action onEndAction, int layerIndex = 0)
        {
            var state = GetLayerOrFallback(layerIndex).CurrentState;

            if (onEndAction == null)
            {
                _layerOnEndActions.Remove(layerIndex);
                if (state != null) state.Events(this).OnEnd = null;
                return;
            }

            System.Action wrapper = null;
            wrapper = () =>
            {
                if (state != null)
                {
                    try { state.Events(this).OnEnd = null; state.Events(this).Clear(); } catch { }
                }

                _layerOnEndActions.Remove(layerIndex);
                try { onEndAction.Invoke(); } catch { }
            };

            _layerOnEndActions[layerIndex] = wrapper;
            if (state != null) state.Events(this).OnEnd = wrapper;
        }

        public void SetLayerWeight(int layerIndex, float weight, float fadeDuration = 0f)
        {
            var layer = GetLayerOrFallback(layerIndex);
            if (layer == null) return;

            if (fadeDuration > 0f) layer.StartFade(weight, fadeDuration);
            else layer.Weight = weight;
        }

        public void SetLayerMask(int layerIndex, AvatarMask mask)
        {
            var layer = GetLayerOrFallback(layerIndex);
            if (layer != null) layer.Mask = mask;
        }

        public void ClearOnEndCallback(int layerIndex = 0)
        {
            var state = GetLayerOrFallback(layerIndex).CurrentState;
            if (state != null)
            {
                state.Events(this).OnEnd = null;
                state.Events(this).Clear();
            }
            _layerOnEndActions.Remove(layerIndex);
        }

        public void AddCallback(float normalizedTime, System.Action callback, int layerIndex = 0)
        {
            var state = GetLayerOrFallback(layerIndex).CurrentState;
            if (state == null || callback == null) return;
            state.Events(this).Add(normalizedTime, callback);
        }

        private void RebindOnEndIfNeeded(int layerIndex, AnimancerState state)
        {
            if (state == null) return;

            try
            {
                state.Events(this).OnEnd = null;
                if (_layerOnEndActions.TryGetValue(layerIndex, out var action))
                {
                    state.Events(this).OnEnd = action;
                }
            }
            catch { }
        }

        private static void ApplyOptions(AnimancerState state, AnimPlayOptions options)
        {
            if (state == null) return;
            if (options.Speed > 0f) state.Speed = options.Speed;
            if (options.NormalizedTime >= 0) state.NormalizedTime = options.NormalizedTime;
        }

        private AnimancerLayer GetLayerOrFallback(int layerIndex)
        {
            var layers = _animancer.Layers;
            if ((uint)layerIndex < (uint)layers.Count) return layers[layerIndex];

            return layers[0];
        }

        // 兼容旧属性，默认只读取基础移动层 (Layer 0) 的时间
        public float CurrentTime => GetLayerTime(0);
        public float CurrentNormalizedTime => GetLayerNormalizedTime(0);

        // 新增分层读取方法
        public float GetLayerTime(int layerIndex)
            => GetLayerOrFallback(layerIndex).CurrentState?.Time ?? 0f;

        public float GetLayerNormalizedTime(int layerIndex)
            => GetLayerOrFallback(layerIndex).CurrentState?.NormalizedTime ?? 0f;
    }
}