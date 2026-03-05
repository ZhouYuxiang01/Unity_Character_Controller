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

            // 1. 仅在当前为 Unavailable 上半身状态时才需要判断是否退出
            if (currentState == null || currentState is not UpperBodyUnavailableState)
            {
                return false;
            }

            // 2. 参考 EnterUnavailable 的实现：通过全局状态机判断是否仍在 Vault/Fall
            var playerBaseState = player.StateMachine.CurrentState;

            // 如果不再处于 Vault 或 Fall，则应该回到正常的上半身状态
            if (!(playerBaseState is PlayerVaultState) && !(playerBaseState is PlayerFallState))
            {
                // 3. 根据是否有装备选择回到 HoldItem 或 EmptyHands
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

            return false;
        }
    }
}