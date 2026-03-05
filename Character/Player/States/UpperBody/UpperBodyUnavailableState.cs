using Core.StateMachine;
using Items.Core;

namespace Characters.Player.States
{
    public class UpperBodyUnavailableState : UpperBodyBaseState
    {
        public UpperBodyUnavailableState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            player.AnimFacade.SetLayerWeight(1, 0f, 0.2f);

            // 强制卸载当前物品并调用物品的退出方法，确保被打断时物品能正确清理
            if (player != null && player.EquipmentDriver != null)
            {
                var director = player.EquipmentDriver.CurrentItemDirector;
                // 先通知物品进行强制退出逻辑（停特效/解绑输入等）
                director?.OnForceUnequip();

                // 然后真正卸载物品实体并清理驱动器状态
                player.EquipmentDriver.UnequipCurrentItem();

                // 清理黑板上的装备意图
                if (player.RuntimeData != null)
                    player.RuntimeData.CurrentItem = null;
            }
        }

        public override void Exit()
        {
            // 拉回上半身权重的逻辑在 HoldItem 里，不需要在这里写。
        }

        protected override void UpdateStateLogic()
        {
            // 为了安全 退出逻辑由唯一打断器接管
        }
    }
}