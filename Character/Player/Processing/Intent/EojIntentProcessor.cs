using Characters.Player.Data;

namespace Characters.Player.Processing
{
    // 表情意图处理器 
    public class EojIntentProcessor
    {
        private readonly PlayerRuntimeData _data;

        public EojIntentProcessor(PlayerRuntimeData data)
        {
            _data = data;
        }

        // 使用元组(Tuple)返回需要消耗的按键状态，交由上层管家处理
        public (bool c1, bool c2, bool c3, bool c4) Update(in ProcessedInputData input)
        {
            bool c1 = false, c2 = false, c3 = false, c4 = false;

            if (input.Expression1Pressed)
            {
                _data.WantsExpression1 = true;
                c1 = true; // 记录消耗
            }
            if (input.Expression2Pressed)
            {
                _data.WantsExpression2 = true;
                c2 = true;
            }
            if (input.Expression3Pressed)
            {
                _data.WantsExpression3 = true;
                c3 = true;
            }
            if (input.Expression4Pressed)
            {
                _data.WantsExpression4 = true;
                c4 = true;
            }

            return (c1, c2, c3, c4);
        }
    }
}