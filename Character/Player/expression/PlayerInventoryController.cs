using System;
using System.Linq;
using Items.Core;
using Items.Data;
using UnityEngine;

namespace Characters.Player.Expression
{
    // 物品栏管理器
    // 管理主背包与快捷栏，负责将输入映射为装备意图
    // 在初始化时轮询数字键输入，并在更新时检测按下状态
    public class PlayerInventoryController
    {
        private PlayerController _player;

        // 主背包 容量20 存储非快捷装备的物品
        public InventorySystem MainInventory { get; private set; }
        // 快捷栏 容量5 与数字键1-5直接对应 这5个槽位的切换由此类管理
        public InventorySystem HotbarInventory { get; private set; }

        // 缓存当前快捷栏选中的槽位 用于判断重复按键时的卸载动作
        private int _currentSlotIndex = -1;

        public PlayerInventoryController(PlayerController player)
        {
            _player = player;
            MainInventory = new InventorySystem(20);
            HotbarInventory = new InventorySystem(5);
        }

        // 在 PlayerController.Start 时调用
        public void Initialize()
        {
            // 监听装备切换事件 保持当前槽位索引与实际装备的同步
            if (_player != null)
            {
                _player.OnEquipmentChanged += OnEquipmentChanged;
            }
        }

        // 清理绑定 防止角色销毁后事件委托仍然活跃导致的内存和逻辑错误
        public void Dispose()
        {
            if (_player == null) return;

            _player.OnEquipmentChanged -= OnEquipmentChanged;

            _player = null;
        }

        // 直接将物品分配到快捷栏的指定槽位 通常用于初始化装备或Shift点击快捷栏
        // 原槽位的物品会移回背包 如果背包满了就悄悄丢掉 这是简化设计
        public void AssignItemToSlot(int slotIndex, ItemInstance itemInstance)
        {
            if (slotIndex < 0 || slotIndex >= 5) return;
            if (itemInstance == null) return;

            // 该槽位有旧物品的话尝试放回背包 背包满了的话旧物品就会消失
            var oldItem = HotbarInventory.SetAt(slotIndex, itemInstance);
            if (oldItem != null)
            {
                MainInventory.TryAdd(oldItem);
            }

            Debug.Log($"[Inventory] 快捷栏[{slotIndex + 1}] 绑定: {itemInstance.BaseData.DisplayName}");
        }

        // 将物品从源背包移动到快捷栏 如果快捷栏位置被占则对换
        public bool MoveToHotbar(InventorySystem source, int sourceSlot, int hotbarSlot)
        {
            if (source == null || sourceSlot < 0 || hotbarSlot < 0 || hotbarSlot >= 5) return false;

            var itemToMove = source.RemoveAt(sourceSlot);
            if (itemToMove == null) return false;

            var oldItem = HotbarInventory.SetAt(hotbarSlot, itemToMove);
            if (oldItem != null)
            {
                source.SetAt(sourceSlot, oldItem);
            }

            return true;
        }

        // 将物品从源背包移动到主背包 如果背包位置被占则对换
        public bool MoveToInventory(InventorySystem source, int sourceSlot, int inventorySlot)
        {
            if (source == null || sourceSlot < 0 || inventorySlot < 0 || inventorySlot >= 20) return false;

            var itemToMove = source.RemoveAt(sourceSlot);
            if (itemToMove == null) return false;

            var oldItem = MainInventory.SetAt(inventorySlot, itemToMove);
            if (oldItem != null)
            {
                source.SetAt(sourceSlot, oldItem);
            }

            return true;
        }

        /// <summary>
        /// 每帧轮询数字键输入。由 PlayerController 的意图管线调用。
        /// </summary>
        public void UpdateNumberKeyInput()
        {
            if (_player?.InputReader == null) return;

            var inputFrame = _player.InputReader.Current;

            // 检测数字键 1-5 是否按下
            if (inputFrame.Number1Pressed)
            {
                TryEquipSlot(0);
                _player.InputReader.ConsumeNumber1();
            }
            if (inputFrame.Number2Pressed)
            {
                TryEquipSlot(1);
                _player.InputReader.ConsumeNumber2();
            }
            if (inputFrame.Number3Pressed)
            {
                TryEquipSlot(2);
                _player.InputReader.ConsumeNumber3();
            }
            if (inputFrame.Number4Pressed)
            {
                TryEquipSlot(3);
                _player.InputReader.ConsumeNumber4();
            }
            if (inputFrame.Number5Pressed)
            {
                TryEquipSlot(4);
                _player.InputReader.ConsumeNumber5();
            }
        }

        // 尝试装备指定快捷栏槽位的物品 这是数字键的响应方法
        // 重复按同一槽位则卸载当前装备 这是一个便利设计
        private void TryEquipSlot(int slotIndex)
        {
            if (_player == null) return;
            if (slotIndex < 0 || slotIndex >= 5) return;

            // 重复按下当前槽位 作为快速卸载的开关
            if (_currentSlotIndex == slotIndex)
            {
                Unequip();
                return;
            }

            var targetInstance = HotbarInventory.GetAt(slotIndex);
            if (targetInstance == null)
            {
                Debug.Log($"[Inventory] 槽位 {slotIndex + 1} 为空 -> 卸载");
                Unequip();
                return;
            }

            // 仅允许可装备物品 其他类型物品被按下时会被忽略
            if (targetInstance.BaseData is EquippableItemSO)
            {
                Debug.Log($"[Inventory] 意图切换 -> {targetInstance.BaseData.DisplayName}");
                _player.RuntimeData.CurrentItem = targetInstance;
            }
            else
            {
                Debug.Log($"[Inventory] 槽位 {slotIndex + 1} 非可装备物品 -> 忽略");
            }
        }

        // 清空当前装备意图 这会触发状态机的装备卸载流程
        private void Unequip()
        {
            if (_player == null) return;
            Debug.Log("[Inventory] 意图卸载");
            _player.RuntimeData.CurrentItem = null;
        }

        // 装备改变时的回调 用于同步当前槽位指针 保证UI显示与实际装备状态一致
        private void OnEquipmentChanged()
        {
            if (_player == null) return;

            var current = _player.RuntimeData.CurrentItem;
            if (current == null)
            {
                _currentSlotIndex = -1;
                return;
            }

            // 查找当前装备在哪个快捷栏槽位 没找到就设为-1
            for (int i = 0; i < 5; i++)
            {
                var hotbarSlot = HotbarInventory.GetAt(i);
                if (hotbarSlot != null && hotbarSlot.InstanceID == current.InstanceID)
                {
                    _currentSlotIndex = i;
                    return;
                }
            }

            _currentSlotIndex = -1;
        }
    }
}