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

            // 强制卸载“当前已装备的实体”，确保被打断时物品能正确清理。
            // 注意：不要在这里改 RuntimeData.CurrentItem。
            // RuntimeData.CurrentItem 表达的是“玩家的装备意图/背包选择”，
            // Fall/Unavailable 只应该让上半身暂时不可用，而不是清空玩家的装备选择。
            if (player != null && player.EquipmentDriver != null)
            {
                // EquipmentDriver.UnequipCurrentItem 内部会调用 OnForceUnequip，避免重复调用。
                player.EquipmentDriver.UnequipCurrentItem();
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