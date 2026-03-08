using Characters.Player.Core;
using Characters.Player.Data;
using Characters.Player.States;
using UnityEngine;

namespace Characters.Player.Core.Interceptors
{
    [CreateAssetMenu(fileName = "ExitUnavailableInterceptor", menuName = "BBBNexus/Player/Interceptors/UpperBody/ExitUnavailable")]
    public class ExitUnavailableInterceptorSO : UpperBodyInterceptorSO
    {
        public override bool TryIntercept(PlayerController player, UpperBodyBaseState currentState, out UpperBodyBaseState nextState)
        {
            nextState = null;

            // 1. 仅在当前为 Unavailable 时考虑退出
            if (currentState == null || currentState is not UpperBodyUnavailableState)
            {
                return false;
            }

            // 2. 获取外层基础状态（EnterUnavailable 中用来触发进入 Unavailable 的那些状态）
            var playerBaseState = player.StateMachine.CurrentState;

            // 如果仍处于 Vault / Fall / Roll 中，则不退出 Unavailable
            if (playerBaseState is PlayerVaultState || playerBaseState is PlayerFallState || playerBaseState is PlayerRollState)
            {
                return false;
            }

            // 3. 否则根据黑板是否有装备决定回 HoldItem 或 Empty
            if (player.RuntimeData != null && player.RuntimeData.CurrentItem != null)
            {
                nextState = player.UpperBodyCtrl.StateRegistry.GetState<UpperBodyHoldItemState>();
            }
            else
            {
                nextState = player.UpperBodyCtrl.StateRegistry.GetState<UpperBodyEmptyState>();
            }

            return true;
        }
    }
}