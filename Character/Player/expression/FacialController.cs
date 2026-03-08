using UnityEngine;
using Animancer;
using Characters.Player.Data;

namespace Characters.Player.Expression
{
    // 面部表情分层控制器
    // 它负责在指定动画层上播放基础表情和瞬时表情
    // 根据运行时黑板触发短期表情并在结束后恢复基础表情
    // 通过 Animancer 的层与事件系统管理播放与回调，保护旧的回调不影响新的瞬时表情
    public class       FacialController
    {
        private readonly AnimancerLayer _layer;
        private readonly PlayerSO _config;
        private readonly PlayerRuntimeData _data;

        // 当前基础表情
        private ClipTransition _currentBaseExpression;

        // 是否有瞬时表情正在播放
        private bool _isPlayingTransient;

        // 播放令牌 用于标识最近一次瞬时表情 避免旧回调影响新播放
        private int _transientPlayToken;

        public FacialController(AnimancerComponent animancer, PlayerSO config, PlayerRuntimeData runtimeData = null)
        {
            _config = config;
            _data = runtimeData;

            // 使用动画层 2 作为面部层
            _layer = animancer.Layers[2];

            // 从 Core 模块注入遮罩
            if (_config != null && _config.Core != null)
                _layer.Mask = _config.Core.FacialMask;

            // 确保层权重为 1
            _layer.Weight = 1f;

            // 默认基础表情：优先使用 Emj.BaseExpression，其次回退到 Core.BlinkAnim
            if (_config != null && _config.Emj != null && _config.Emj.BaseExpression != null && _config.Emj.BaseExpression.Clip != null)
                _currentBaseExpression = _config.Emj.BaseExpression;
            else if (_config != null && _config.Core != null)
                _currentBaseExpression = _config.Core.BlinkAnim;

            PlayBaseExpression();
        }

        // 每帧由 PlayerController 调用
        // 检查运行时黑板中的表情意图并触发相应的瞬时表情
        public void Update()
        {
            if (_data == null || _config == null) return;
            if (_config.Emj == null) return;

            // 允许瞬时表情相互打断，不在播放时直接返回
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

        // 播放瞬时表情，使用播放令牌保护旧的中断回调
        // expressionClip 为空时不做任何操作
        public void PlayTransientExpression(ClipTransition expressionClip, float fadeDuration = 0.1f)
        {
            if (expressionClip == null || expressionClip.Clip == null) return;

            // 增加令牌，使之前的瞬时回调失效
            _transientPlayToken++;
            var token = _transientPlayToken;

            _isPlayingTransient = true;

            var state = _layer.Play(expressionClip, fadeDuration);

            state.Events(this).OnEnd = () =>
            {
                // 仅处理最近一次启动的瞬时表情的结束回调
                if (token != _transientPlayToken) return;

                _isPlayingTransient = false;
                PlayBaseExpression(0.2f);
            };
        }

        // 播放当前设置的基础表情
        private void PlayBaseExpression(float fadeDuration = 0.25f)
        {
            if (_currentBaseExpression != null && _currentBaseExpression.Clip != null)
            {
                _layer.Play(_currentBaseExpression, fadeDuration);
            }
        }
    }
}
