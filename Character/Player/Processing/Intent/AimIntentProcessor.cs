using Characters.Player.Data;

namespace Characters.Player.Processing
{
    // 瞄准与开火意图处理器 负责处理玩家的瞄准状态和开火意图
    public class AimIntentProcessor
    {
        private PlayerRuntimeData _data;
        private bool _isAimHeld;
        private bool _wasAimHeld;

        // 构造函数：只认黑板，剥离所有其他依赖
        public AimIntentProcessor(PlayerRuntimeData data)
        {
            _data = data;
        }

        // 返回值结构体 包含开火消耗标记
        public struct FireIntentResult
        {
            public bool shouldConsumeFire;
        }

        // 传入只读快照 (in 关键字：0拷贝，绝对安全)
        // 返回是否需要消耗开火输入（供 IntentProcessorPipeline 调用 ConsumeLeftMousePressed）
        public FireIntentResult Update(in ProcessedInputData input)
        {
            bool isAimHeldNow = input.AimHeld;
            // 修复：检查按住状态而不仅仅是瞬间按下
            // 这样玩家按住左键时就能持续开火
            bool isFireHeldOrPressed = input.LeftMouseHeld || input.LeftMousePressed;

            // 更新瞄准状态
            _data.IsAiming = isAimHeldNow;

            // 更新瞄准状态的历史记录 供外部判断状态切换
            _wasAimHeld = _isAimHeld;
            _isAimHeld = isAimHeldNow;

            // 更新开火意图到黑板
            // 支持两种情况：
            // 1. 按住左键（LeftMouseHeld）- 用于连续射击
            // 2. 瞬间按下（LeftMousePressed）- 用于精确射击
            if (isFireHeldOrPressed)
            {
                _data.WantsToFire = true;
            }

            // 返回是否应消耗开火输入
            // 仅在瞬间按下时消耗 以避免打断持续射击
            return new FireIntentResult { shouldConsumeFire = input.LeftMousePressed };
        }
    }
}
