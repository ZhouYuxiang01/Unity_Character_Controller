using Items.Core;
using Items.Data;
using UnityEngine;

namespace Characters.Player.Core
{
    /// <summary>
    /// 装备驱动器 (无情的 3D 打印机)
    /// 唯一职责：根据传入的“灵魂(ItemInstance)”，在玩家手上打印出“肉体(Prefab)”，并完成instance的依赖注入。
    /// </summary>
    public class EquipmentDriver
    {
        private readonly PlayerController _player;

        // --- 运行时暴露给外部的只读缓存 ---
        public EquippableItemSO CurrentItemData { get; private set; }
        public ItemInstance CurrentItemInstance { get; private set; }
        public IHoldableItem CurrentItemDirector { get; private set; }

        private GameObject _currentWeaponInstance;

        public EquipmentDriver(PlayerController player)
        {
            _player = player;
        }

        /// <summary>
        /// 核心方法：装配新物品
        /// </summary>
        /// <param name="itemInstance">来自背包/黑板的唯一物品灵魂</param>
        public void EquipItem(ItemInstance itemInstance)
        {
            // 1. 销毁旧肉体
            UnequipCurrentItem();

            // 2. 缓存新灵魂
            CurrentItemInstance = itemInstance;
            CurrentItemData = itemInstance != null ? itemInstance.GetSODataAs<EquippableItemSO>() : null;

            if (CurrentItemData == null)
            {
                // 空手状态，或者该物品不可被装备 (没有 EquippableItemSO)
                _player?.NotifyEquipmentChanged();
                return;
            }

            // 3. 打印新肉体
            if (CurrentItemData.Prefab != null && _player != null && _player.WeaponContainer != null)
            {
                _currentWeaponInstance = Object.Instantiate(CurrentItemData.Prefab, _player.WeaponContainer);

                _currentWeaponInstance.transform.localPosition = CurrentItemData.HoldPositionOffset;
                _currentWeaponInstance.transform.localRotation = CurrentItemData.HoldRotationOffset;
                // unity在实例化prefab时是默认带上“创建时的世界坐标的”
                // 这里必须用覆盖 如果单纯+=位置偏移 就会产生位置错误
                if (_currentWeaponInstance.transform.localScale != Vector3.one) Debug.LogWarning("当前物品的预制件缩放不为1");

                // 5. 提取控制接口
                CurrentItemDirector = _currentWeaponInstance.GetComponent<IHoldableItem>();

                // ✨ 6. 灵魂注入！把实例数据强行塞进生成的模型脚本中！
                CurrentItemDirector?.Initialize(CurrentItemInstance);
            }

            // 通知外部 UI 等系统更新
            _player?.NotifyEquipmentChanged();
        }

        /// <summary>
        /// 销毁当前手里的物品
        /// </summary>
        public void UnequipCurrentItem()
        {
            if (_currentWeaponInstance != null)
            {
                Object.Destroy(_currentWeaponInstance);
                _currentWeaponInstance = null;
            }

            _player?.NotifyEquipmentChanged();//如果不通知 会出现下一次重复按一个快捷栏无法呼出装备的问题
            CurrentItemData = null;
            CurrentItemInstance = null;
            CurrentItemDirector = null;
        }
    }
}