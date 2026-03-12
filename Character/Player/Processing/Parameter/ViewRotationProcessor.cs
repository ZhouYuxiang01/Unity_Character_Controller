using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.Processing
{
    public class ViewRotationProcessor
    {
        private readonly PlayerRuntimeData _data;
        private readonly PlayerSO _config;

        public ViewRotationProcessor(PlayerRuntimeData data, PlayerSO config)
        {
            _data = data;
            _config = config;
        }

        public void Update(in ProcessedInputData input)
        {
            // 拿到这一帧的原始增量 (Mouse Delta 或 摇杆输入)
            Vector2 lookInput = input.Look;

            if (lookInput.sqrMagnitude > 0.000001f)
            {
                // 【核心修复】：直接把增量累加进 Yaw 和 Pitch 里！绝对不要去减去 lastLook！
                _data.ViewYaw += lookInput.x * _config.Core.LookSensitivity.x;
                _data.ViewPitch += lookInput.y * _config.Core.LookSensitivity.y;

                // 钳制 Pitch (上下看) 并让 Yaw (左右转) 在 360 度内循环
                _data.ViewPitch = Mathf.Clamp(_data.ViewPitch, _config.Core.PitchLimits.x, _config.Core.PitchLimits.y);
                _data.ViewYaw = Mathf.Repeat(_data.ViewYaw, 360f);
            }

            // 更新黑板
            _data.LookInput = lookInput;
            _data.MoveInput = input.Move;
            _data.AuthorityYaw = _data.ViewYaw;
            _data.AuthorityPitch = _data.ViewPitch;
            _data.AuthorityRotation = Quaternion.Euler(_data.AuthorityPitch, _data.AuthorityYaw, 0f);
        }
    }
}