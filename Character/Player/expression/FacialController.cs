using Animancer;
using Characters.Player.Data;
using Characters.Player.Processing;
using UnityEngine;

namespace Characters.Player.Expression
{
    // 面部表情分层控制器
    // 架构定位 表现层子系统 负责在指定动画层上独立播放面部表情
    // 数据流向 读取运行时黑板意图 -> 消费 InputPipeline 缓存 -> 驱动 Animancer 层
    // 通过 Animancer 的层与事件系统管理播放与回调 利用播放令牌保护新旧动画的上下文切换
    public class FacialController
    {
        private readonly AnimancerLayer _layer;
        private readonly PlayerSO _config;
        private readonly PlayerRuntimeData _data;
        private readonly InputPipeline _inputPipeline;

        // 当前基础表情 默认状态下的常驻动画
        private ClipTransition _currentBaseExpression;

        // 是否有瞬时表情正在播放
        private bool _isPlayingTransient;

        // 播放令牌 用于标识最近一次瞬时表情 避免旧动画的 OnEnd 回调切断新动画
        private int _transientPlayToken;

        // 构造注入 仅依赖总管家 PlayerController 从中提取所需的底层引用
        public FacialController(PlayerController player)
        {
            _config = player.Config;
            _data = player.RuntimeData;
            _inputPipeline = player.InputPipeline;

            // 使用动画层 2 作为独立的面部层
            _layer = player.Animancer.Layers[2];

            // 从 Core 模块注入骨骼遮罩 确保面部动画不影响身体
            if (_config != null && _config.Core != null)
            {
                _layer.Mask = _config.Core.FacialMask;
            }

            // 确保层权重为满状态
            _layer.Weight = 1f;

            // 初始化基础表情 优先使用表情模块的专属设置 其次回退到核心模块的眨眼动画
            if (_config != null && _config.Emj != null && _config.Emj.BaseExpression != null && _config.Emj.BaseExpression.Clip != null)
            {
                _currentBaseExpression = _config.Emj.BaseExpression;
            }
            else if (_config != null && _config.Core != null)
            {
                _currentBaseExpression = _config.Core.BlinkAnim;
            }

            PlayBaseExpression();
        }

        // 每帧由 PlayerController 的总时序管线调用
        // 检查黑板中的表情意图 触发动画并完成原子级按键核销
        public void Update()
        {
            if (_data == null || _config == null || _config.Emj == null) return;

            // 允许瞬时表情相互打断 直接覆盖播放并核销输入缓存
            if (_data.WantsExpression1)
            {
                _inputPipeline.ConsumeExpression1Pressed();
                PlayTransientExpression(_config.Emj.SpecialExpression1, 0.1f);
            }
            else if (_data.WantsExpression2)
            {
                _inputPipeline.ConsumeExpression2Pressed();
                PlayTransientExpression(_config.Emj.SpecialExpression2, 0.1f);
            }
            else if (_data.WantsExpression3)
            {
                _inputPipeline.ConsumeExpression3Pressed();
                PlayTransientExpression(_config.Emj.SpecialExpression3, 0.1f);
            }
            else if (_data.WantsExpression4)
            {
                _inputPipeline.ConsumeExpression4Pressed();
                PlayTransientExpression(_config.Emj.SpecialExpression4, 0.1f);
            }
        }

        // 触发瞬时表情
        // 使用自增的播放令牌机制 彻底解决动画交叉淡入淡出时的回调地狱问题
        public void PlayTransientExpression(ClipTransition expressionClip, float fadeDuration = 0.1f)
        {
            if (expressionClip == null || expressionClip.Clip == null) return;

            // 增加令牌 使之前所有未执行完毕的动画 OnEnd 回调全部成为废票
            _transientPlayToken++;
            var token = _transientPlayToken;

            _isPlayingTransient = true;

            var state = _layer.Play(expressionClip, fadeDuration);

            // 绑定动画结束事件
            state.Events(this).OnEnd = () =>
            {
                // 仅当当前令牌与触发时的令牌一致时 才允许恢复基础表情
                // 防止新表情刚播放 旧表情的结束事件就把它强行切走
                if (token != _transientPlayToken) return;

                _isPlayingTransient = false;
                PlayBaseExpression(0.2f);
            };
        }

        // 播放当前挂载的基础面部动画
        private void PlayBaseExpression(float fadeDuration = 0.25f)
        {
            if (_currentBaseExpression != null && _currentBaseExpression.Clip != null)
            {
                _layer.Play(_currentBaseExpression, fadeDuration);
            }
        }
    }
}