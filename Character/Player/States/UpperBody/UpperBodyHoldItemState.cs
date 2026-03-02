using Items.Core;
using UnityEngine;

namespace Characters.Player.States
{
    /// <summary>
    /// 上半身：持有物品状态
    /// 它是严厉的监工，时刻盯着黑板。只要黑板数据变了，立刻呼叫 Driver 换枪。
    /// 枪换好后，无脑把 Update 权限下放给武器实体。
    /// </summary>
    public class UpperBodyHoldItemState : UpperBodyBaseState
    {
        private IHoldableItem _currentItem; // 当前拿在手里的肉体控制器
        private ItemInstance _cachedInstance; // 缓存当前的灵魂，用于脏数据对比

        public UpperBodyHoldItemState(PlayerController p) : base(p) { }

        public override void Enter()
        {
            // 持有武器时，强制上半身动画层权重为 1
            player.AnimFacade.SetLayerWeight(1, 1f, 0.25f);

            // 刚进入状态，执行一次强制同步
            SyncEquipmentFromBlackboard();
        }

        public override void Exit()
        {
            // 离开状态（比如被打断、切回空手），让当前武器清理后事（停特效、解绑输入等）
            _currentItem?.OnForceUnequip();
        }

        protected override void UpdateStateLogic()
        {
            // 1. 脏数据检测（雷达）：黑板上的物品被人换了？！
            if (_cachedInstance != player.RuntimeData.CurrentItem)
            {
                SyncEquipmentFromBlackboard();
                return; // 换枪的这一帧，跳过原有武器的逻辑更新
            }

            // 2. 退出条件：如果发现黑板里没东西了（玩家收枪了），切回空手状态
            if (player.RuntimeData.CurrentItem == null)
            {
                player.UpperBodyCtrl.StateMachine.ChangeState(
                    player.UpperBodyCtrl.StateRegistry.GetState<UpperBodyEmptyState>()
                );
                return;
            }

            // 3. 正常运行：监工坐在旁边喝茶，让打工人(肉体)自己干活
            _currentItem?.OnUpdateLogic();
        }

        /// <summary>
        /// 核心调度方法：处理物品的装载与控制权移交
        /// </summary>
        private void SyncEquipmentFromBlackboard()
        {
            // 1. 温柔地剥夺旧武器的控制权
            _currentItem?.OnForceUnequip();

            // 2. 更新雷达缓存
            _cachedInstance = player.RuntimeData.CurrentItem;

            if (_cachedInstance != null)
            {
                // ✨ 3. 命令装配厂干活：打印模型，注入灵魂！
                player.EquipmentDriver.EquipItem(_cachedInstance);

                // ✨ 4. 拿到刚造出来的肉体的最高权限！
                _currentItem = player.EquipmentDriver.CurrentItemDirector;

                // 5. 下达开工命令（武器会在这里播放拔枪动画、宣告IK主权）
                _currentItem?.OnEquipEnter(player);
            }
            else
            {
                // 玩家把枪收起来了，销毁肉体
                player.EquipmentDriver.UnequipCurrentItem();
                _currentItem = null;
            }
        }
    }
}