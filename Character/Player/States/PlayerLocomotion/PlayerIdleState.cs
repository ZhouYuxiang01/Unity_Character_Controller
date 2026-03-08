using UnityEngine;
using System;
using Characters.Player.Data;
using Characters.Player.Animation;

namespace Characters.Player.States
{
    // 玩家空闲状态 
    // 负责播放空闲动画 并检测移动 跳跃等输入意图触发状态切换 
    [Serializable]
    public class PlayerIdleState : PlayerBaseState
    {
        public PlayerIdleState(PlayerController player) : base(player) { }

        // 进入状态 播放空闲动画 设置平滑淡入时长避免动画跳变
        public override void Enter()
        {
            ChooseOptionsAndPlay(config.LocomotionAnims.IdleAnim);
        }

        // 更新状态逻辑 检测移动 跳跃意图 触发状态切换
        protected override void UpdateStateLogic()
        {
            if (data.CurrentLocomotionState != LocomotionState.Idle)
            {
                switch (data.CurrentLocomotionState)
                {
                    case LocomotionState.Walk:
                        data.NextStatePlayOptions = config.LocomotionAnims.FadeInWalkStartOptions;
                        break;
                    case LocomotionState.Jog:
                        data.NextStatePlayOptions = config.LocomotionAnims.FadeInRunStartOptions;
                        break;
                    case LocomotionState.Sprint:
                        data.NextStatePlayOptions = config.LocomotionAnims.FadeInSprintStartOptions;
                        break;
                    default:
                        data.NextStatePlayOptions = AnimPlayOptions.Default;
                        break;
                }

                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveStartState>());
                return;
            }

            if (data.WantsToJump)
            {
                data.NextStatePlayOptions = config.LocomotionAnims.FadeInJumpOptions;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerJumpState>());
            }
        }

        // 物理更新 空闲状态仍需驱动运动逻辑 防止角色浮空 接地状态异常
        public override void PhysicsUpdate()
        {
            // 即使在空闲状态 也需要调用MotionDriver更新运动 重力 接地检测等
            player.MotionDriver.UpdateMotion();
        }

        // 退出状态 空闲状态退出时无额外清理逻辑
        public override void Exit()
        {
        }
    }
}
