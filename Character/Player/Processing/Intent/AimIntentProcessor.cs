using UnityEngine;
using Characters.Player.Data;

namespace Characters.Player.Processing
{
    // 瞄准意图处理器 它负责转接瞄准输入意图 
    // 按住右键进入瞄准 松开后保持一小段时间再退出 
    public class AimIntentProcessor
    {
        private PlayerController _player;
        private PlayerRuntimeData _data;
        private PlayerSO _config;

        // 上一帧瞄准键是否按住 用于边沿检测 
        private bool _wasAimHeld;

        public AimIntentProcessor(PlayerController player)
        {
            _player = player;
            _data = player.RuntimeData;
            _config = player.Config;
        }

        // 每帧由 PlayerController 调用 直接读取 InputReader.Current 的 AimHeld 字段 
        // 然后把瞄准状态写入黑板 
        public void Update()
        {
            // 直接读取当前帧的输入状态
            bool isAimHeldNow = _player.InputReader.Current.AimHeld;

            // 更新黑板中的瞄准状态
            _data.IsAiming = isAimHeldNow;

            _wasAimHeld = isAimHeldNow;
        }
    }
}
