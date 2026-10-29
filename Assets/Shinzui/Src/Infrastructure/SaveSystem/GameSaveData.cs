using Shinzui.Application.Interfaces.Inventory;

namespace Shinzui.Infrastructure.SaveSystem
{
    /// <summary>
    /// ゲームデータのセーブに含めたい情報を保持するクラス
    /// </summary>
    [System.Serializable]
    public class GameSaveData
    {
        public int Version = 1;
        public InventorySaveData InventorySaveData = new InventorySaveData
        {
            Slots = System.Array.Empty<InventorySlotSaveData>()
        };
        public SpecialItemSaveData SpecialItemSaveData = new SpecialItemSaveData { ItemId = "" };
    }
}
