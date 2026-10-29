using System.Threading.Tasks;
using Shinzui.Application.Interfaces.Inventory;

namespace Shinzui.Infrastructure.Repositories
{
    /// <summary>セーブ操作までの持ち物をメモリ上で保持する</summary>
    public sealed class MemoryInventoryRepository : IInventoryRepository, ISpecialItemRepository
    {
        private InventorySaveData _inventory;
        private SpecialItemSaveData _specialItem;

        /// <summary>現在のインベントリを更新する</summary>
        /// <param name="data">現在の持ち物</param>
        /// <returns>完了済みタスク</returns>
        public Task SaveInventoryAsync(InventorySaveData data)
        {
            _inventory = data;
            return Task.CompletedTask;
        }

        /// <summary>現在のインベントリを取得する</summary>
        /// <returns>保持中の持ち物</returns>
        public Task<InventorySaveData> LoadInventoryAsync() => Task.FromResult(_inventory);

        /// <summary>現在の特殊アイテムを更新する</summary>
        /// <param name="data">現在の特殊アイテム</param>
        /// <returns>完了済みタスク</returns>
        public Task SaveSpecialItemAsync(SpecialItemSaveData data)
        {
            _specialItem = data;
            return Task.CompletedTask;
        }

        /// <summary>現在の特殊アイテムを取得する</summary>
        /// <returns>保持中の特殊アイテム</returns>
        public Task<SpecialItemSaveData> LoadSpecialItemAsync() => Task.FromResult(_specialItem);
    }
}
