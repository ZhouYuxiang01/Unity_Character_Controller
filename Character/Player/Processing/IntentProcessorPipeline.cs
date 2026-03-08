using Characters.Player.Core;
using Characters.Player.Processing;

namespace Characters.Player.Processing
{
    // 意图处理器管道 它是输入意图到动画参数的完整转换链路 
    // 第一阶段转换输入为逻辑意图 第二阶段转换逻辑意图为表现层参数 
    public class IntentProcessorPipeline
    {
        // 核心处理器 负责各类输入的转换
        private readonly LocomotionIntentProcessor _locomotionIntentProcessor;
        private readonly AimIntentProcessor _aimIntentProcessor;
        private readonly JumpOrVaultIntentProcessor _jumpOrVaultIntentProcessor;
        private readonly EojIntentProcessor _eojIntentProcessor;

        // 参数生成器 负责逻辑意图的编码
        private readonly MovementParameterProcessor _movementParameterProcessor;
        private readonly ViewRotationProcessor _viewRotationProcessor;

        // 物品栏输入轮询
        private readonly PlayerController _playerController;

        public IntentProcessorPipeline(PlayerController player)
        {
            _playerController = player;

            // 初始化顺序很重要 权威方向源必须最先生成 其他处理器才能消费
            _viewRotationProcessor = new ViewRotationProcessor(player);
            _aimIntentProcessor = new AimIntentProcessor(player);
            _locomotionIntentProcessor = new LocomotionIntentProcessor(player);
            _jumpOrVaultIntentProcessor = new JumpOrVaultIntentProcessor(player);
            _eojIntentProcessor = new EojIntentProcessor(player);

            _movementParameterProcessor = new MovementParameterProcessor(player);
        }

        public void update()
        {
            UpdateIntentProcessors();
            UpdateParameterProcessors();
        }

        // 第一阶段 意图生成 
        // 顺序很关键 权威旋转 装备状态 瞄准状态 运动意图必须严格按序处理 
        // 后续处理器依赖前序处理器的结果 打乱顺序会导致逻辑混乱 
        public void UpdateIntentProcessors()
        {
            // 1. 确定权威旋转参考系 AuthorityYaw Pitch 这是所有方向计算的源头
            _viewRotationProcessor.Update();

            // 2. 轮询数字快捷键输入 装备意图 
            _playerController.InventoryController?.UpdateNumberKeyInput();

            // 3. 处理瞄准状态意图 影响动画混合树的选择
            _aimIntentProcessor.Update();

            // 4. 处理最终的运动方向与行为意图 依赖上述所有状态
            _locomotionIntentProcessor.Update();

            // 5. 处理跳跃翻越意图 优先级较低但必须在运动之后
            _jumpOrVaultIntentProcessor.Update();

            // 6. 表情意图 完全独立的一条线
            _eojIntentProcessor.Update();
        }

        // 第二阶段 参数编码 
        // 将逻辑意图转换成动画系统能理解的参数 
        // 这里的计算都是纯粹的数学运算 不产生新的意图 
        public void UpdateParameterProcessors()
        {
            // 1. 根据运动意图计算动画混合参数 BlendX BlendY 
            // 这些参数直接驱动Animancer的混合树权重
            _movementParameterProcessor.Update();

            // IK 由 PlayerController 中的 IKController.Update() 单独处理
        }

        // 对外公开引用 供特殊初始化或调试使用 
        public LocomotionIntentProcessor Locomotion => _locomotionIntentProcessor;
        public AimIntentProcessor Aim => _aimIntentProcessor;
        public JumpOrVaultIntentProcessor JumpOrVault => _jumpOrVaultIntentProcessor;
        public EojIntentProcessor Eoj => _eojIntentProcessor;
    }
}