using System.Threading.Tasks;
using Shinzui.Application.Interfaces.Inventory;
using UnityEngine;

namespace Shinzui.Infrastructure.Repositories
{
    public class PlayerPrefsInventoryRepository : IInventoryRepository
    {
        private const string SaveKey = "shinzui_inventory_data";

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
    }
}
