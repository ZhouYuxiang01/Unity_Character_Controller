using Characters.Player.Core;
using Characters.Player.Data;
using Characters.Player.States;
using UnityEngine;

namespace Characters.Player.Core.Interceptors
{
    [CreateAssetMenu(fileName = "EnterUnavailableInterceptor", menuName = "BBBNexus/Player/Interceptors/UpperBody/EnterUnavailable")]
    public class EnterUnavailableInterceptorSO : UpperBodyInterceptorSO
    {
        public override bool TryIntercept(PlayerController player, UpperBodyBaseState currentState, out UpperBodyBaseState nextState)
        {
            nextState = null;

            // 1. 如果当前已经是 Unavailable，则不需要重复打断
            if (currentState != null && currentState is UpperBodyUnavailableState)
            {
                return false;
            }

            // 2. 获取下半身的当前状态（根据你的框架，通常存在 RuntimeData 或 StateMachine 里）
            // 注意：这里假设你的下半身状态存放在 player.RuntimeData.CurrentState，如有偏差请自行替换
            var playerbasestate = player.StateMachine.CurrentState;

            // 3. 核心判断：处于 Vault 或 Fall
            if (playerbasestate is PlayerVaultState || playerbasestate is PlayerFallState)
            {
                // 获取并切入 Unavailable 状态
                nextState = player.UpperBodyCtrl.StateRegistry.GetState<UpperBodyUnavailableState>();
                return true;
            }

            return false;
        }
    }
}