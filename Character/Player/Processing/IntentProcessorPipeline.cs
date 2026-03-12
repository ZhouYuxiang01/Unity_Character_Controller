using Characters.Player.Core;
using Characters.Player.Data; // 引入数据
using Characters.Player.Input;
using Characters.Player.Processing.Input;
using UnityEngine;

namespace Characters.Player.Processing
{
    // 意图处理器管道 它是输入意图到动画参数的完整转换链路 
    public class IntentProcessorPipeline
    {
        // 只有总管家有权持有 InputPipeline 和 RuntimeData
        private readonly InputPipeline _inputPipeline;
        private readonly PlayerRuntimeData _runtimeData;
        private readonly PlayerSO _config; // 参数转换与逻辑运算需要的配置

        // 核心处理器 (严格保持干净的依赖)
        private readonly LocomotionIntentProcessor _locomotionIntentProcessor;
        private readonly AimIntentProcessor _aimIntentProcessor;
        private readonly JumpOrVaultIntentProcessor _jumpOrVaultIntentProcessor;
        private readonly EojIntentProcessor _eojIntentProcessor;
        private readonly HotbarIntentProcessor _hotbarIntentProcessor; // 新增快捷栏意图

        // 参数生成器
        private readonly MovementParameterProcessor _movementParameterProcessor;
        private readonly ViewRotationProcessor _viewRotationProcessor;

        // 物品栏输入轮询
        private readonly PlayerController _playerController;

        public IntentProcessorPipeline(PlayerController player)
        {
            _playerController = player;
            _runtimeData = player.RuntimeData;
            _config = player.Config;

            // 创建输入管线 (使用序列化的输入源引用)
            var inputSource = player.InputSourceRef as IInputSource;
            if (inputSource == null)
            {
                Debug.LogError("[IntentProcessorPipeline] 玩家控制器的输入源未正确初始化");
            }
            _inputPipeline = new InputPipeline(inputSource, 0.05f, 0.03f, 0.2f);

            // 【核心重构】：初始化所有处理器
            _viewRotationProcessor = new ViewRotationProcessor(_runtimeData, _config);
            _aimIntentProcessor = new AimIntentProcessor(_runtimeData);
            _locomotionIntentProcessor = new LocomotionIntentProcessor(_runtimeData, _config);
            _jumpOrVaultIntentProcessor = new JumpOrVaultIntentProcessor(_runtimeData, _config, player.transform);
            _eojIntentProcessor = new EojIntentProcessor(_runtimeData);

            // 🚨 修复空指针：在这里把你的快捷栏翻译机 new 出来！
            _hotbarIntentProcessor = new HotbarIntentProcessor(_runtimeData);

            _movementParameterProcessor = new MovementParameterProcessor(_runtimeData, _config, player.transform);
        }

        public void update()
        {
            UpdateInputPipeline();
            UpdateIntentProcessors();
            UpdateParameterProcessors();
        }

        public void UpdateInputPipeline()
        {
            _inputPipeline.Update();
        }

        public void UpdateIntentProcessors()
        {
            // 【神级代码】：抓取本帧纯净快照的内存指针！绝对零拷贝！
            ref readonly ProcessedInputData inputSnapshot = ref _inputPipeline.Current.currentFrameData.Processed;

            // 1. 确定权威旋转参考系
            _viewRotationProcessor.Update(in inputSnapshot);

            // 2. 瞄准与开火意图处理 (接收开火消耗标记)
            var aimResult = _aimIntentProcessor.Update(in inputSnapshot);
            if (aimResult.shouldConsumeFire) _inputPipeline.ConsumeLeftMousePressed();

            // 3. 运动意图 (接收 out 参数反馈，若消耗了翻滚或闪避，立刻在管线中清零)
            _locomotionIntentProcessor.Update(in inputSnapshot, out bool consumeRoll, out bool consumeDodge);
            if (consumeRoll) _inputPipeline.ConsumeRollPressed();
            if (consumeDodge) _inputPipeline.ConsumeDodgePressed();

            // 4. 跳跃意图
            if (_jumpOrVaultIntentProcessor.Update(in inputSnapshot))
            {
                _inputPipeline.ConsumeJumpPressed();
            }

            // 5. 表情意图
            var eojResult = _eojIntentProcessor.Update(in inputSnapshot);
            if (eojResult.c1) _inputPipeline.ConsumeExpression1Pressed();
            if (eojResult.c2) _inputPipeline.ConsumeExpression2Pressed();
            if (eojResult.c3) _inputPipeline.ConsumeExpression3Pressed();
            if (eojResult.c4) _inputPipeline.ConsumeExpression4Pressed();

            // 6. 快捷栏装备意图
            var hbResult = _hotbarIntentProcessor.Update(in inputSnapshot);
            if (hbResult.n1) _inputPipeline.ConsumeNumber1Pressed();
            if (hbResult.n2) _inputPipeline.ConsumeNumber2Pressed();
            if (hbResult.n3) _inputPipeline.ConsumeNumber3Pressed();
            if (hbResult.n4) _inputPipeline.ConsumeNumber4Pressed();
            if (hbResult.n5) _inputPipeline.ConsumeNumber5Pressed();
        }

        public void UpdateParameterProcessors()
        {
            _movementParameterProcessor.Update();
        }

        public InputPipeline InputPipeline => _inputPipeline;
    }
}