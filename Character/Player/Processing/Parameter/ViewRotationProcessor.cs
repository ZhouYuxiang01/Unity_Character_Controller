using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.Processing
{
    // 视角旋转处理器 它是权威方向源生成器 
    // 负责从鼠标右摇杆增量累加得到 ViewYaw ViewPitch 
    // 并同步为 AuthorityYaw AuthorityPitch 计算 AuthorityRotation
    public class ViewRotationProcessor
    {
        private readonly PlayerController _player;
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;

        public ViewRotationProcessor(PlayerController player)
        {
            _player = player;
            _data = player.RuntimeData;
            _config = player.Config;
        }

        // 每帧消费输入增量并更新权威方向 
        // 这个管线应该是除了有后坐力的物品之外的唯一权威方向来源！ 
        public void Update()
        {
            // 读取并消费输入 权威方向源只应由这里维护
            Vector2 lookDelta = _data.LookInput;
            _data.LookInput = Vector2.zero;

            if (lookDelta.sqrMagnitude > 0.000001f)
            {
                // 注意 LookInput 绑定的是 Mouse delta 每帧增量
                _data.ViewYaw += lookDelta.x * _config.Core.LookSensitivity.x;

                _data.ViewPitch += lookDelta.y * _config.Core.LookSensitivity.y;
                _data.ViewPitch = Mathf.Clamp(_data.ViewPitch, _config.Core.PitchLimits.x, _config.Core.PitchLimits.y);

                _data.ViewYaw = Mathf.Repeat(_data.ViewYaw, 360f);
            }

            // 权威方向源 始终等于 View
            _data.AuthorityYaw = _data.ViewYaw;
            _data.AuthorityPitch = _data.ViewPitch;
            _data.AuthorityRotation = Quaternion.Euler(_data.AuthorityPitch, _data.AuthorityYaw, 0f);
        }
    }
}
