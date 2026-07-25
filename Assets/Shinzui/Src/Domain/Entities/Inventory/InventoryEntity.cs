using System;
using System.Linq;
using FMOD;
using R3;
using Shinzui.Domain.ValueObjects.Inventory;
using UnityEditor.Search;

namespace Shinzui.Domain.Entities.Inventory
{
    public class InventoryEntity
    {
        private readonly ReactiveProperty<ItemStack>[] _slots;
        public int Capacity => _slots.Length;

        // PresenterやUseCaseからは、インデックス付きの変更可能なストリームとして提供
        public ReadOnlyReactiveProperty<ItemStack> GetSlot(int index) => _slots[index];

        public InventoryEntity(int capacity)
        {
            _slots = new ReactiveProperty<ItemStack>[capacity];
            for (int i = 0; i < capacity; i++)
            {
                _slots[i] = new ReactiveProperty<ItemStack>(null);
            }
        }

        // 初期化用(セーブデータからのロード時など)
        public void SetSlotForce(int index, ItemStack stack)
        {
            if (index < 0 || index >= Capacity) return;
            _slots[index].Value = stack;
        }

        /// <summary>
        /// アイテムをインベントリに追加する(自動スタック・空きスロットへの配置)
        /// </summary>
        public bool TryAddItem(ItemDefinition item, int quantity, out int remaining)
        {
            remaining = quantity;

            // 1. 既存の同一アイテムスタックへの追加を試みる
            for (int i = 0; i < Capacity; i++)
            {
                var slot = _slots[i].Value;
                if (slot != null && slot.Item.Id == item.Id && slot.Quantity < slot.Item.MaxStackSize)
                {
                    var updated = slot.Add(remaining, out int overflow);
                    _slots[i].Value = updated;
                    remaining = overflow;
                    if (remaining == 0) return true;
                }
            }

            // 2. 空きスロットへ新規スタックとして配置する
            for (int i = 0; i < Capacity; i++)
            {
                if (_slots[i].Value == null)
                {
                    int addQuantity = Math.Min(remaining, item.MaxStackSize);
                    _slots[i].Value = new ItemStack(item, addQuantity);
                    remaining -= addQuantity;
                    if (remaining == 0) return true;
                }
            }

            return remaining < quantity; // 一部でも追加できたらtrue
        }

        /// <summary>
        /// 特定スロットからアイテムを指定数消費・削除する
        /// </summary>
        public bool TryRemoveItem(int slotIndex, int amount)
        {
            if (slotIndex < 0 || slotIndex >= Capacity) return false;
            var slot = _slots[slotIndex].Value;
            if (slot == null) return false;

            var updated = slot.Remove(amount, out int deficit);
            _slots[slotIndex].Value = updated; // updatedがnullならスロットは空になる
            return deficit == 0;
        }

        /// <summary>
        /// スロット同士を入れ替える(またはスタックをマージする)
        /// </summary>
        public void SwapOrMergeSlots(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= Capacity) return;
            if (toIndex < 0 || toIndex >= Capacity) return;
            if (fromIndex == toIndex) return;

            var source = _slots[fromIndex].Value;
            var target = _slots[toIndex].Value;

            if (source == null) return;

            // 同一アイテムかつスタックに空きがある場合はマージを試みる
            if (target != null && source.Item.Id == target.Item.Id)
            {
                int room = target.Item.MaxStackSize - target.Quantity;
                if (room > 0)
                {
                    var newTarget = target.Add(source.Quantity, out int overflow);
                    _slots[toIndex].Value = newTarget;

                    if (overflow > 0)
                    {
                        _slots[fromIndex].Value = source with { Quantity = overflow };
                    }
                    else
                    {
                        _slots[fromIndex].Value = null;
                    }
                    return;
                }
            }

            // 単純入れ替え
            _slots[fromIndex].Value = target;
            _slots[toIndex].Value = source;
        }

        /// <summary>
        /// 指定したアイテムのスロットインデックスを返す
        /// 持っていない場合は-1を返す
        /// </summary>
        /// <param name="item">調査対象のアイテム</param>
        /// <returns></returns>
        public int CheckItemSlotIndex(ItemDefinition item)
        {
            var idx = Array.FindIndex(_slots, s => s != null && s.Value != null && s.Value.Item != null && s.Value.Item == item);
            return idx;
        }
    }
}
