using System.Threading.Tasks;
using Shinzui.Application.Interfaces.Inventory;
using UnityEngine;

namespace Shinzui.Infrastructure.Repositories
{
    public class PlayerPrefsInventoryRepository : IInventoryRepository, ISpecialItemRepository
    {
        private const string SaveKey = "shinzui_inventory_data";
        private const string SpecialItemSaveKey = "shinzui_special_item_data";

        public Task SaveInventoryAsync(InventorySaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(SaveKey, json);
                PlayerPrefs.Save();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InventoryRepository] セーブに失敗しました: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        public Task<InventorySaveData> LoadInventoryAsync()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                return Task.FromResult<InventorySaveData>(null);
            }

            try
            {
                string json = PlayerPrefs.GetString(SaveKey);
                var data = JsonUtility.FromJson<InventorySaveData>(json);
                return Task.FromResult(data);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InventoryRepository] ロードに失敗しました: {ex.Message}");
                return Task.FromResult<InventorySaveData>(null);
            }
        }

        public Task SaveSpecialItemAsync(SpecialItemSaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(SpecialItemSaveKey, json);
                PlayerPrefs.Save();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SpecialItemRepository] セーブに失敗しました: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        public Task<SpecialItemSaveData> LoadSpecialItemAsync()
        {
            if (!PlayerPrefs.HasKey(SpecialItemSaveKey))
            {
                return Task.FromResult<SpecialItemSaveData>(null);
            }

            try
            {
                string json = PlayerPrefs.GetString(SpecialItemSaveKey);
                var data = JsonUtility.FromJson<SpecialItemSaveData>(json);
                return Task.FromResult(data);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SpecialItemRepository] ロードに失敗しました: {ex.Message}");
                return Task.FromResult<SpecialItemSaveData>(null);
            }
        }
    }
}
