using Characters.Player.Data;

namespace Characters.Player.Processing
{
    // 快捷栏意图处理器 
    // 负责将数字键1-5映射为装备槽位切换的意图，并写入黑板
    public class HotbarIntentProcessor
    {
        private readonly PlayerRuntimeData _data;

        public HotbarIntentProcessor(PlayerRuntimeData data)
        {
            _data = data;
        }

        // 使用元组返回消耗结果，供总管家清理管线缓存
        public (bool n1, bool n2, bool n3, bool n4, bool n5) Update(in ProcessedInputData input)
        {
            bool n1 = false, n2 = false, n3 = false, n4 = false, n5 = false;

            if (input.Number1Pressed)
            {
                _data.WantsToEquipHotbarIndex = 0;
                n1 = true;
            }
            else if (input.Number2Pressed)
            {
                _data.WantsToEquipHotbarIndex = 1;
                n2 = true;
            }
            else if (input.Number3Pressed)
            {
                _data.WantsToEquipHotbarIndex = 2;
                n3 = true;
            }
            else if (input.Number4Pressed)
            {
                _data.WantsToEquipHotbarIndex = 3;
                n4 = true;
            }
            else if (input.Number5Pressed)
            {
                _data.WantsToEquipHotbarIndex = 4;
                n5 = true;
            }

            return (n1, n2, n3, n4, n5);
        }
    }
}