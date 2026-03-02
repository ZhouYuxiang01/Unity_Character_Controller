using System;
using System.Linq;
using Items.Core;
using Items.Data;
using UnityEngine;
using UnityEngine.Events;

namespace Characters.Player.Expression
{
    public class PlayerInventoryController
    {
        private PlayerController _player;

        public InventorySystem MainInventory { get; private set; }
        public InventorySystem HotbarInventory { get; private set; }

        private int _currentSlotIndex = -1;

        // 缓存委托，确保可正确解绑（PlayerInputReader 使用 UnityAction）
        private UnityAction _on1;
        private UnityAction _on2;
        private UnityAction _on3;
        private UnityAction _on4;
        private UnityAction _on5;

        public PlayerInventoryController(PlayerController player)
        {
            _player = player;
            MainInventory = new InventorySystem(20);
            HotbarInventory = new InventorySystem(5);
        }

        public void Initialize()
        {
            if (_player?.InputReader != null)
            {
                _on1 = () => TryEquipSlot(0);
                _on2 = () => TryEquipSlot(1);
                _on3 = () => TryEquipSlot(2);
                _on4 = () => TryEquipSlot(3);
                _on5 = () => TryEquipSlot(4);

                _player.InputReader.OnNumber1Pressed += _on1;
                _player.InputReader.OnNumber2Pressed += _on2;
                _player.InputReader.OnNumber3Pressed += _on3;
                _player.InputReader.OnNumber4Pressed += _on4;
                _player.InputReader.OnNumber5Pressed += _on5;
            }

            if (_player != null)
            {
                _player.OnEquipmentChanged += OnEquipmentChanged;
            }
        }

        public void Dispose()
        {
            if (_player == null) return;

            _player.OnEquipmentChanged -= OnEquipmentChanged;

            if (_player.InputReader != null)
            {
                if (_on1 != null) _player.InputReader.OnNumber1Pressed -= _on1;
                if (_on2 != null) _player.InputReader.OnNumber2Pressed -= _on2;
                if (_on3 != null) _player.InputReader.OnNumber3Pressed -= _on3;
                if (_on4 != null) _player.InputReader.OnNumber4Pressed -= _on4;
                if (_on5 != null) _player.InputReader.OnNumber5Pressed -= _on5;
            }

            _player = null;
        }

        public void AssignItemToSlot(int slotIndex, ItemInstance itemInstance)
        {
            if (slotIndex < 0 || slotIndex >= 5) return;
            if (itemInstance == null) return;

            // 如果该槽位有物品，则放回背包，或替换。 这里仅简单替换，原物品会被替换掉
            var oldItem = HotbarInventory.SetAt(slotIndex, itemInstance);
            if (oldItem != null)
            {
                MainInventory.TryAdd(oldItem);
            }

            Debug.Log($"[Inventory] 快捷栏[{slotIndex + 1}] 绑定: {itemInstance.BaseData.DisplayName}");
        }

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

        private void TryEquipSlot(int slotIndex)
        {
            if (_player == null) return;
            if (slotIndex < 0 || slotIndex >= 5) return;

            // 重复按下当前槽位 -> 卸载
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

            // 仅允许可装备物品实例作为当前装备意图
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

        private void Unequip()
        {
            if (_player == null) return;
            Debug.Log("[Inventory] 意图卸载");
            _player.RuntimeData.CurrentItem = null;
        }

        private void OnEquipmentChanged()
        {
            if (_player == null) return;

            var current = _player.RuntimeData.CurrentItem;
            if (current == null)
            {
                _currentSlotIndex = -1;
                return;
            }

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