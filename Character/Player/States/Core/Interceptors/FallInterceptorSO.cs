using Characters.Player.Core;
using UnityEngine;

namespace Characters.Player.States
{
    // 下落全局拦截器 
    // 负责检测下落意图 当空中时间过长自动触发下落动画 优先级较高
    [CreateAssetMenu(fileName = "FallInterceptor", menuName = "BBBNexus/Player/Interceptors/Fall")]
    public class FallInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(PlayerController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;

            // 检测下落意图 如果不在下落或翻越状态 则切换到下落状态
            if (data.WantsToFall && currentState is not PlayerFallState && currentState is not PlayerVaultState)
            {
                data.NextStatePlayOptions = player.Config.LocomotionAnims.FadeInFallOptions;
                nextState = player.StateRegistry.GetState<PlayerFallState>();
                return true;
            }

            return false;
        }
    }
}