using System.Threading.Tasks;
using UnityEngine;
using Shinzui.DI;
using Shinzui.Application.UseCases.Inventory;
using VContainer;

namespace Shinzui.Temp
{
    /// <summary>
    /// 脱出用の鍵（古びた鍵）を持ってるか調べたり使ったりする
    /// </summary>
    public static class TempEscapeKey
    {
        public const string ItemId = "key_old";

        /// <summary>
        /// 鍵を持っているか
        /// </summary>
        public static bool HasKey()
        {
            return FindKeySlot(ResolveInventory()) >= 0;
        }

        /// <summary>
        /// 鍵を1本使う
        /// </summary>
        public static async Task UseKeyAsync()
        {
            InventoryUseCase inventory = ResolveInventory();
            int slot = FindKeySlot(inventory);
            if (slot < 0)
            {
                return;
            }

            // 消費アイテム以外を減らす口が無いので、一瞬だけ装備して消費する
            int equipped = inventory.EquippedSlotIndex.CurrentValue;
            inventory.EquipItem(slot);
            await inventory.ConsumeEquippedItemAsync();
            inventory.EquipItem(equipped);
        }

        private static InventoryUseCase ResolveInventory()
        {
            var playerScope = Object.FindAnyObjectByType<PlayerLifetimeScope>();
            if (playerScope == null || playerScope.Container == null)
            {
                return null;
            }
            return playerScope.Container.Resolve<InventoryUseCase>();
        }

        private static int FindKeySlot(InventoryUseCase inventory)
        {
            if (inventory == null)
            {
                return -1;
            }

            for (int i = 0; i < inventory.Capacity; i++)
            {
                var slot = inventory.GetSlotDto(i).CurrentValue;
                if (slot.HasItem && slot.ItemId == ItemId)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
