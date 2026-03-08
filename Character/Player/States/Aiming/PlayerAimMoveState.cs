using Characters.Player.Data;
using Characters.Player.Animation;
using Core.StateMachine;
using UnityEngine;

namespace Characters.Player.States
{
    // 玩家瞄准移动状态 
    // 在瞄准模式下持续移动 使用混合树控制动画参数 启用LookAtIK
    public class PlayerAimMoveState : PlayerBaseState
    {
        public PlayerAimMoveState(PlayerController player) : base(player) { }

        // 进入状态 播放瞄准移动混合树 启用LookAtIK目标追踪
        public override void Enter()
        {
            var options = AnimPlayOptions.Default;
            options.FadeDuration = 0.2f;
            AnimFacade.PlayTransition(config.Aiming.AimLocomotionMixer, options);

            data.WantsLookAtIK = true;
        }

        // 状态逻辑 检测松开瞄准 跳跃 或停止输入
        protected override void UpdateStateLogic()
        {
            if (!data.IsAiming)
            {
                player.StateMachine.ChangeState(
                    data.CurrentLocomotionState == LocomotionState.Idle
                        ? (BaseState)player.StateRegistry.GetState<PlayerIdleState>()
                        : player.StateRegistry.GetState<PlayerMoveLoopState>());
                return;
            }

            if (data.WantsDoubleJump)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerDoubleJumpState>());
                return;
            }

            if (data.WantsToJump)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerJumpState>());
                return;
            }

            if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerAimIdleState>());
                return;
            }

            // 每帧更新混合树参数 驱动瞄准动画的方向与强度
            AnimFacade.SetMixerParameter(new Vector2(data.CurrentAnimBlendX, data.CurrentAnimBlendY));
        }

        // 物理更新 在瞄准时仍需处理重力
        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateMotion(null, 0f);
        }

        // 退出状态 无额外清理逻辑
        public override void Exit()
        {
        }
    }
}
