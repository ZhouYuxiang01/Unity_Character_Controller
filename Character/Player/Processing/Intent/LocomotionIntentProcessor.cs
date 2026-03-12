using Characters.Player.Data;
using UnityEngine;

namespace Characters.Player.Processing
{
    // 运动意图处理器
    public class LocomotionIntentProcessor
    {
        private PlayerRuntimeData _data;
        private PlayerSO _config;

        public LocomotionIntentProcessor(PlayerRuntimeData data, PlayerSO config)
        {
            _data = data;
            _config = config;
        }

        // 增加 out 参数，向上级报告是否消耗了闪避和翻滚
        public void Update(in ProcessedInputData input, out bool consumeRoll, out bool consumeDodge)
        {
            ProcessMovementIntent();
            ProcessLocomotionStateAndStaminaIntent(in input, out consumeRoll, out consumeDodge);
        }

        private void ProcessMovementIntent()
        {
            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;

            if (isMoving)
            {
                Quaternion yawRot = Quaternion.Euler(0f, _data.AuthorityYaw, 0f);
                Vector3 basisForward = yawRot * Vector3.forward;
                Vector3 basisRight = yawRot * Vector3.right;
                _data.DesiredWorldMoveDir = (basisRight * _data.MoveInput.x + basisForward * _data.MoveInput.y).normalized;
                _data.QuantizedDirection = QuantizeInputDirection(_data.MoveInput);
            }
            else
            {
                _data.DesiredWorldMoveDir = Vector3.zero;
                _data.QuantizedDirection = DesiredDirection.None;
            }
        }

        private void ProcessLocomotionStateAndStaminaIntent(in ProcessedInputData input, out bool consumeRoll, out bool consumeDodge)
        {
            consumeRoll = false;
            consumeDodge = false;
            LocomotionState prestate = _data.CurrentLocomotionState;

            if (input.RollPressed && _data.IsGrounded)
            {
                _data.WantsToRoll = true;
                consumeRoll = true; // 标记消耗
            }
            if (input.DodgePressed && _data.IsGrounded)
            {
                _data.WantsToDodge = true;
                consumeDodge = true; // 标记消耗
            }

            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;

            if (_data.IsStaminaDepleted && _data.CurrentStamina > _config.Core.MaxStamina * _config.Core.StaminaRecoverThreshold)
            {
                _data.IsStaminaDepleted = false;
            }

            if (!isMoving)
            {
                _data.CurrentLocomotionState = LocomotionState.Idle;
                _data.WantToRun = false;
            }
            else if (input.SprintHeld && !_data.IsStaminaDepleted && _data.CurrentStamina > 0)
            {
                _data.CurrentLocomotionState = LocomotionState.Sprint;
                _data.WantToRun = true;
            }
            else if (input.WalkHeld)
            {
                _data.CurrentLocomotionState = LocomotionState.Walk;
                _data.WantToRun = false;
            }
            else
            {
                _data.CurrentLocomotionState = LocomotionState.Jog;
                _data.WantToRun = false;
            }

            if (_data.CurrentLocomotionState != prestate) _data.LastLocomotionState = prestate;
        }

        // QuantizeInputDirection 保持你的原样代码，这里略过以节省字数...
        private DesiredDirection QuantizeInputDirection(Vector2 input)
        {
            float threshold = 0.5f;
            bool hasForward = input.y > threshold;
            bool hasBackward = input.y < -threshold;
            bool hasRight = input.x > threshold;
            bool hasLeft = input.x < -threshold;

            if (hasForward) { if (hasLeft) return DesiredDirection.ForwardLeft; if (hasRight) return DesiredDirection.ForwardRight; return DesiredDirection.Forward; }
            if (hasBackward) { if (hasLeft) return DesiredDirection.BackwardLeft; if (hasRight) return DesiredDirection.BackwardRight; return DesiredDirection.Backward; }
            if (hasLeft) return DesiredDirection.Left;
            if (hasRight) return DesiredDirection.Right;
            return DesiredDirection.None;
        }
    }
}