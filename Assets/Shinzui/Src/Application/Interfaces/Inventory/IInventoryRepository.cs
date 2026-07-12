using System.Threading.Tasks;
using Shinzui.Domain.ValueObjects.Inventory;

namespace Shinzui.Application.Interfaces.Inventory
{
    // アイテムマスタ参照用
    public interface IItemCatalog
    {
        Task<ItemDefinition> GetItemAsync(string itemId);
    }

    // インベントリのセーブ/ロード用
    public interface IInventoryRepository
    {
        Task SaveInventoryAsync(InventorySaveData data);
        Task<InventorySaveData> LoadInventoryAsync();
    }

    // 特殊アイテム専用スロットのセーブ/ロード用
    public interface ISpecialItemRepository
    {
        Task SaveSpecialItemAsync(SpecialItemSaveData data);
        Task<SpecialItemSaveData> LoadSpecialItemAsync();
    }

    // 保存用シリアライズデータ構造
    [System.Serializable]
    public class InventorySaveData
    {
        public InventorySlotSaveData[] Slots;
    }

    [System.Serializable]
    public class InventorySlotSaveData
    {
        public int SlotIndex;
        public string ItemId;
        public int Quantity;
    }

    [System.Serializable]
    public class SpecialItemSaveData
    {
        public string ItemId;
    }
}
