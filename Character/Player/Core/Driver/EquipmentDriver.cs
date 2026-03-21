using UnityEngine;

namespace BBBNexus
{
    // 装备驱动器 负责生成模型注入数据驱动逻辑
    public class EquipmentDriver
    {
        // 玩家引用
        private readonly PlayerController _player;
        // 当前物品配置
        public EquippableItemSO CurrentItemData { get; private set; }
        // 当前物品实例
        public ItemInstance CurrentItemInstance { get; private set; }
        // 当前物品逻辑接口
        public IHoldableItem CurrentItemDirector { get; private set; }
        // 当前模型实例
        private GameObject _currentWeaponInstance;

        public EquipmentDriver(PlayerController player)
        {
            _player = player;
        }

        // 装配物品生成模型注入数据驱动逻辑
        public void EquipItem(ItemInstance itemInstance)
        {
            UnequipCurrentItem();
            CurrentItemInstance = itemInstance;
            CurrentItemData = itemInstance != null ? itemInstance.GetSODataAs<EquippableItemSO>() : null;
            if (CurrentItemData == null)
            {
                Debug.Log("驱动器判定当前为空手状态 正在重置表现层");
                _player?.NotifyEquipmentChanged();
                return;
            }

            if (CurrentItemData.Prefab != null && _player != null && _player.WeaponContainer != null)
            {
                var prefab = CurrentItemData.Prefab;

                if (SimpleObjectPoolSystem.Shared != null)
                {
                    _currentWeaponInstance = SimpleObjectPoolSystem.Shared.Spawn(prefab);
                    // 调用者负责父子级：武器必须挂到 WeaponContainer
                    _currentWeaponInstance.transform.SetParent(_player.WeaponContainer, false);
                    _currentWeaponInstance.transform.localPosition = Vector3.zero;
                    _currentWeaponInstance.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    _currentWeaponInstance = Object.Instantiate(prefab, _player.WeaponContainer);
                }

                // 关键：复用对象必须重置 localScale，避免上一次使用遗留缩放导致异常（甚至把角色一起缩没）。
                _currentWeaponInstance.transform.localScale = Vector3.one;

                _currentWeaponInstance.transform.localPosition = CurrentItemData.HoldPositionOffset;
                _currentWeaponInstance.transform.localRotation = CurrentItemData.HoldRotationOffset;
                if (_currentWeaponInstance.transform.localScale != Vector3.one) Debug.LogWarning("检测到预制件缩放异常 建议检查离线配置");
                CurrentItemDirector = _currentWeaponInstance.GetComponent<IHoldableItem>();
                CurrentItemDirector?.Initialize(CurrentItemInstance);
                if (CurrentItemDirector == null)
                {
                    Debug.LogWarning("生成的模型缺少控制接口 状态机将无法驱动该武器");
                }
            }
            else
            {
                Debug.LogWarning("装配失败 检查预制件引用或容器挂点是否丢失");
            }
            _player?.NotifyEquipmentChanged();
        }

        // 卸载当前物品销毁模型清理逻辑
        public void UnequipCurrentItem()
        {
            if (_currentWeaponInstance != null)
            {
                if (SimpleObjectPoolSystem.Shared != null)
                {
                    SimpleObjectPoolSystem.Shared.Despawn(_currentWeaponInstance);
                }
                else
                {
                    Object.Destroy(_currentWeaponInstance);
                }
                _currentWeaponInstance = null;
            }
            _player.NotifyEquipmentChanged();
            CurrentItemData = null;
            CurrentItemInstance = null;
            CurrentItemDirector = null;
        }
    }
}