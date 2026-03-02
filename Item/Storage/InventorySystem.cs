using System;
using System.Collections.Generic;
using System.Linq;
using Items.Data;
using UnityEngine;

namespace Items.Core
{
    /// <summary>
    /// 通用背包系统（新 ItemInstance 体系）。
    /// - 内部存储 ItemInstance（或其子类）。
    /// - 堆叠依据 ItemDefinitionSO.MaxStack 与 ItemInstance.CurrentAmount。
    /// </summary>
    public class InventorySystem
    {
        private readonly ItemInstance[] _items;
        private readonly int _capacity;

        public event Action OnInventoryUpdated;

        public InventorySystem(int capacity)
        {
            _capacity = capacity;
            _items = new ItemInstance[capacity];
        }

        /// <summary>
        /// 尝试添加一个物品实例到背包（自动寻找空位或堆叠）
        /// 支持将大数量实例拆分到多个槽位（原子性：若没有足够空位则不修改背包）
        /// </summary>
        public bool TryAdd(ItemInstance instance)
        {
            if (instance == null || instance.CurrentAmount <= 0) return false;

            var definition = instance.BaseData;
            int remaining = instance.CurrentAmount;

            // --- 1) 先模拟堆叠，计算剩余数量（不修改数据结构） ---
            if (definition.MaxStack > 1)
            {
                for (int i = 0; i < _capacity && remaining > 0; i++)
                {
                    var existing = _items[i];
                    if (existing == null || existing.BaseData != definition) continue;

                    int space = Mathf.Max(0, existing.BaseData.MaxStack - existing.CurrentAmount);
                    if (space <= 0) continue;

                    int add = Mathf.Min(space, remaining);
                    remaining -= add;
                }
            }

            // 如果全部可以通过堆叠解决，直接提交（堆叠阶段会在提交时真正修改）
            if (remaining <= 0)
            {
                // 提交：真正把数量堆入已有堆栈
                int toStack = instance.CurrentAmount;
                for (int i = 0; i < _capacity && toStack > 0; i++)
                {
                    var existing = _items[i];
                    if (existing == null || existing.BaseData != definition) continue;

                    int space = Mathf.Max(0, existing.BaseData.MaxStack - existing.CurrentAmount);
                    if (space <= 0) continue;

                    int add = Mathf.Min(space, toStack);
                    existing.CurrentAmount += add;
                    toStack -= add;
                }

                // 原实例已全部消耗
                instance.CurrentAmount = 0;
                NotifyUpdate();
                return true;
            }

            // --- 2) 计算空槽数量并判断是否有足够槽位来放下剩余数量 ---
            int emptyCount = 0;
            for (int i = 0; i < _capacity; i++)
            {
                if (_items[i] == null) emptyCount++;
            }

            int requiredSlots;
            if (definition.MaxStack > 1)
            {
                requiredSlots = (remaining + definition.MaxStack - 1) / definition.MaxStack; // ceil
            }
            else
            {
                requiredSlots = remaining; // 每个物品占一个槽
            }

            if (emptyCount < requiredSlots)
            {
                Debug.LogWarning("[InventorySystem] 背包空位不足，无法添加整个物品实例。");
                return false; // 原子：不做任何修改
            }

            // --- 3) 提交：先堆叠到已有槽，再把剩余拆分到空槽 ---
            remaining = instance.CurrentAmount;

            // 堆叠提交
            if (definition.MaxStack > 1)
            {
                for (int i = 0; i < _capacity && remaining > 0; i++)
                {
                    var existing = _items[i];
                    if (existing == null || existing.BaseData != definition) continue;

                    int space = Mathf.Max(0, existing.BaseData.MaxStack - existing.CurrentAmount);
                    if (space <= 0) continue;

                    int add = Mathf.Min(space, remaining);
                    existing.CurrentAmount += add;
                    remaining -= add;
                }
            }

            // 拆分到空槽
            bool originalPlaced = false;
            for (int i = 0; i < _capacity && remaining > 0; i++)
            {
                if (_items[i] != null) continue;

                int put = definition.MaxStack > 1 ? Mathf.Min(remaining, definition.MaxStack) : 1;

                if (!originalPlaced)
                {
                    // 将原始实例放入第一个空槽，保留其 InstanceID
                    instance.CurrentAmount = put;
                    _items[i] = instance;
                    originalPlaced = true;
                }
                else
                {
                    // 创建新的实例以填充剩余堆栈
                    _items[i] = new ItemInstance(definition, put);
                }

                remaining -= put;
            }

            // 提交成功：剩余应为 0
            if (remaining != 0)
            {
                Debug.LogWarning("[InventorySystem] 意外：拆分后仍有剩余，背包状态可能不一致。");
            }

            NotifyUpdate();
            return true;
        }

        /// <summary>
        /// 移除指定槽位的物品（完全移除）
        /// </summary>
        public ItemInstance RemoveAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _capacity) return null;
            var item = _items[slotIndex];
            _items[slotIndex] = null;
            if (item != null) NotifyUpdate();
            return item;
        }

        /// <summary>
        /// 将物品实例放置到指定槽位
        /// 如果该槽位已有物品，则替换并返回原物品
        /// </summary>
        public ItemInstance SetAt(int slotIndex, ItemInstance instance)
        {
            if (slotIndex < 0 || slotIndex >= _capacity) return instance;
            var oldItem = _items[slotIndex];
            _items[slotIndex] = instance;
            NotifyUpdate();
            return oldItem;
        }

        public ItemInstance GetAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _capacity) return null;
            return _items[slotIndex];
        }

        public void Remove(ItemDefinitionSO definition, int amount = 1)
        {
            if (definition == null || amount <= 0) return;

            for (int i = _capacity - 1; i >= 0 && amount > 0; i--)
            {
                var inst = _items[i];
                if (inst == null || inst.BaseData != definition) continue;

                int toRemove = Mathf.Min(amount, inst.CurrentAmount);
                inst.CurrentAmount -= toRemove;
                amount -= toRemove;

                if (inst.CurrentAmount <= 0)
                {
                    _items[i] = null;
                }
            }

            NotifyUpdate();
        }

        public bool Has(ItemDefinitionSO definition, int amount = 1)
        {
            return GetCount(definition) >= amount;
        }

        public int GetCount(ItemDefinitionSO definition)
        {
            if (definition == null) return 0;
            int sum = 0;
            for (int i = 0; i < _capacity; i++)
            {
                var inst = _items[i];
                if (inst != null && inst.BaseData == definition)
                    sum += inst.CurrentAmount;
            }
            return sum;
        }

        public ItemInstance FindFirst(ItemDefinitionSO definition)
        {
            if (definition == null) return null;
            for (int i = 0; i < _capacity; i++)
            {
                var inst = _items[i];
                if (inst != null && inst.BaseData == definition)
                    return inst;
            }
            return null;
        }

        public IReadOnlyList<ItemInstance> GetAllItems()
        {
            return _items.Where(i => i != null).ToList();
        }

        private void NotifyUpdate()
        {
            OnInventoryUpdated?.Invoke();
        }
    }
}
