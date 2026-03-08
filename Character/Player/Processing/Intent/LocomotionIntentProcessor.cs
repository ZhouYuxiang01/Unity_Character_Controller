using Characters.Player.Data;
using Characters.Player.Input;
using UnityEngine;

namespace Characters.Player.Processing
{
    // 运动意图处理器 它是移动决策的中枢 
    // 负责转接移动输入 计算运动状态 整理闪避与翻滚意图 
    public class LocomotionIntentProcessor
    {
        private PlayerController _player;
        private PlayerInputReader _input;
        private PlayerRuntimeData _data;
        private PlayerSO _config;

        public LocomotionIntentProcessor(PlayerController player)
        {
            _player = player;
            _input = player.InputReader;
            _data = player.RuntimeData;
            _config = player.Config;
        }

        // 每帧处理运动意图 包括方向 速度档位 闪避翻滚等 
        public void Update()
        {
            ProcessMovementIntent();
            ProcessLocomotionStateAndStaminaIntent();
        }

        // 计算平滑的世界空间移动方向 并量化出8方向意图 
        private void ProcessMovementIntent()
        {
            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;

            if (isMoving)
            {
                // 计算平滑的世界空间移动方向 
                Quaternion yawRot = Quaternion.Euler(0f, _data.AuthorityYaw, 0f);
                Vector3 basisForward = yawRot * Vector3.forward;
                Vector3 basisRight = yawRot * Vector3.right;
                _data.DesiredWorldMoveDir = (basisRight * _data.MoveInput.x + basisForward * _data.MoveInput.y).normalized;

                // 计算量化的8方向意图 供闪避等动作选择起步动画 
                _data.QuantizedDirection = QuantizeInputDirection(_data.MoveInput);
            }
            else
            {
                _data.DesiredWorldMoveDir = Vector3.zero;
                _data.QuantizedDirection = DesiredDirection.None;
            }
        }

        // 将连续的摇杆输入量化为离散的8个方向 
        // 用于选择对应的启动动画 避免无穷多个方向的动画混合 
        private DesiredDirection QuantizeInputDirection(Vector2 input)
        {
            // 使用简单的阈值来判断主方向
            float threshold = 0.5f;

            bool hasForward = input.y > threshold;
            bool hasBackward = input.y < -threshold;
            bool hasRight = input.x > threshold;
            bool hasLeft = input.x < -threshold;

            if (hasForward)
            {
                if (hasLeft) return DesiredDirection.ForwardLeft;
                if (hasRight) return DesiredDirection.ForwardRight;
                return DesiredDirection.Forward;
            }

            if (hasBackward)
            {
                if (hasLeft) return DesiredDirection.BackwardLeft;
                if (hasRight) return DesiredDirection.BackwardRight;
                return DesiredDirection.Backward;
            }

            // 如果没有前后输入 只判断左右
            if (hasLeft) return DesiredDirection.Left;
            if (hasRight) return DesiredDirection.Right;

            // 如果所有输入都在阈值内 则认为是无方向
            return DesiredDirection.None;
        }

        // 处理运动状态与体力意图 
        // 根据输入 体力 接地状态判定当前的运动档位 
        private void ProcessLocomotionStateAndStaminaIntent()
        {
            LocomotionState prestate = _data.CurrentLocomotionState;

            // 直接读取当前帧的输入状态
            var inputFrame = _player.InputReader.Current;

            // 检测翻滚和闪避按键 并消费掉 
            if (inputFrame.RollPressed && _data.IsGrounded) 
            {
                _data.WantsToRoll = true;
                _player.InputReader.ConsumeRoll();
            }
            if (inputFrame.DodgePressed && _data.IsGrounded) 
            {
                _data.WantsToDodge = true;
                _player.InputReader.ConsumeDodge();
            }

            bool isMoving = _data.MoveInput.sqrMagnitude > 0.01f;

            // 首先检查体力耗尽恢复条件
            if (_data.IsStaminaDepleted && _data.CurrentStamina > _config.Core.MaxStamina * _config.Core.StaminaRecoverThreshold)
            {
                _data.IsStaminaDepleted = false;
            }

            // 根据优先级判定最终的运动状态
            if (!isMoving)
            {
                _data.CurrentLocomotionState = LocomotionState.Idle;
                _data.WantToRun = false;
            }
            else if (inputFrame.SprintHeld && !_data.IsStaminaDepleted && _data.CurrentStamina > 0)
            {
                _data.CurrentLocomotionState = LocomotionState.Sprint;
                _data.WantToRun = true;
            }
            else if (inputFrame.WalkHeld)
            {
                _data.CurrentLocomotionState = LocomotionState.Walk;
                _data.WantToRun = false;
            }
            else
            {
                _data.CurrentLocomotionState = LocomotionState.Jog;
                _data.WantToRun = false;
            }

            if(_data.CurrentLocomotionState!=prestate)_data.LastLocomotionState = prestate;
        }
    }
}
