using Characters.Player.Data;

namespace Characters.Player.Processing
{
    // 表情意图处理器 它负责转接表情输入意图 
    // 读取输入帧的表情按键状态 并写入一帧的意图标志到黑板 
    public class EojIntentProcessor
    {
        private readonly PlayerController _player;
        private readonly PlayerRuntimeData _data;

        public EojIntentProcessor(PlayerController player)
        {
            _player = player;
            _data = player.RuntimeData;
        }

        // 每帧检查表情按键 消费按键状态并写入黑板意图 
        public void Update()
        {
            // 直接读取当前帧的输入状态
            if (_player?.InputReader == null) return;

            var inputFrame = _player.InputReader.Current;

            // 根据表情按键状态设置黑板意图
            if (inputFrame.Expression1Pressed)
            {
                _data.WantsExpression1 = true;
                _player.InputReader.ConsumeExpression1();
            }
            if (inputFrame.Expression2Pressed)
            {
                _data.WantsExpression2 = true;
                _player.InputReader.ConsumeExpression2();
            }
            if (inputFrame.Expression3Pressed)
            {
                _data.WantsExpression3 = true;
                _player.InputReader.ConsumeExpression3();
            }
            if (inputFrame.Expression4Pressed)
            {
                _data.WantsExpression4 = true;
                _player.InputReader.ConsumeExpression4();
            }
        }
    }
}
