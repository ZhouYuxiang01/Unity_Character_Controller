// 文件路径: Characters/Player/Animation/IAnimationFacade.cs
using UnityEngine;
using Animancer;

namespace Characters.Player.Animation
{
    public interface IAnimationFacade
    {
        void InitializeAnimancer(AnimancerComponent animancerComponent);

        void PlayClip(AnimationClip clip, AnimPlayOptions options);

        void PlayTransition(object transitionObj, AnimPlayOptions options);

        void SetMixerParameter(Vector2 parameter, int layerIndex = 0);

        void SetOnEndCallback(System.Action onEndAction, int layerIndex = 0);
        void ClearOnEndCallback(int layerIndex = 0);

        void SetLayerWeight(int layerIndex, float weight, float fadeDuration = 0f);

        void SetLayerMask(int layerIndex, AvatarMask mask);

        void AddCallback(float normalizedTime, System.Action callback, int layerIndex = 0);

        // 默认获取 Layer 0 的时间 (兼容旧代码)
        float CurrentTime { get; }
        float CurrentNormalizedTime { get; }

        // 允许显式获取特定层的时间
        float GetLayerTime(int layerIndex);
        float GetLayerNormalizedTime(int layerIndex);
    }
}