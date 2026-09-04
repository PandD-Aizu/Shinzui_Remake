using UnityEngine;
using System.Threading.Tasks;
using Shinzui.Application.Interfaces.Inventory;

namespace Shinzui.Infrastructure.SaveData
{
    /// <summary>
    /// ゲームのセーブ&ロード処理を行う
    /// </summary>
    public class SaveManager
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly ISpecialItemRepository _specialItemRepository;
    
        private const string SaveKey = "shinzui_savedata_";
        private const int SaveDataSlot = 3;
        public int SaveSlotNum => SaveDataSlot;

        public SaveManager(
            IInventoryRepository inventoryRepository,
            ISpecialItemRepository specialItemRepository)
        {
            _inventoryRepository = inventoryRepository;
            _specialItemRepository = specialItemRepository;
        }

        /// <summary>
        /// セーブ
        /// </summary>
        /// <param name="saveIdx">セーブスロットのインデックス</param>
        public async void Save(int saveIdx)
        {
            GameSaveData data = await CreateGameSaveDataAsync();
            Task saveTask = SaveGameData(saveIdx, data);
        }

        /// <summary>
        /// ロード
        /// </summary>
        /// <param name="loadIdx">セーブスロットのインデックス</param>
        /// <returns>ロードしたゲームデータ</returns>
        public async Task<GameSaveData> Load(int loadIdx)
        {
            GameSaveData data = await LoadGameDataAsync(loadIdx);
            return data;
        }
    
        /// <summary>
        /// セーブデータをjsonファイルに変換しPlayerPrefsに保存する
        /// </summary>
        /// <param name="saveIdx">セーブスロットのインデックス</param>
        /// <param name="data">セーブデータ</param>
        /// <returns></returns>
        private Task SaveGameData(int saveIdx, GameSaveData data)
        {
            if (saveIdx > SaveDataSlot)
            {
                Debug.LogError($"[SaveManager] セーブスロットの指定が範囲外です");
                return Task.CompletedTask;
            }
            
            try
            {
                string json = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(SaveKey + saveIdx, json);
                PlayerPrefs.Save();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveManager] セーブに失敗しました: {ex.Message}");
            }
            return Task.CompletedTask;
        
        }

        /// <summary>
        /// ロードするゲームデータをjsonからGameSaveDataに変換する
        /// </summary>
        /// <param name="loadIdx">セーブスロットのインデックス</param>
        /// <returns>指定したセーブスロットのゲームデータ</returns>
        private Task<GameSaveData> LoadGameDataAsync(int loadIdx)
        {
            if (!PlayerPrefs.HasKey(SaveKey + loadIdx))
            {
                return Task.FromResult<GameSaveData>(null);
            }

            try
            {
                string json = PlayerPrefs.GetString(SaveKey);
                var data = JsonUtility.FromJson<GameSaveData>(json);
                return Task.FromResult(data);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveManager] ロードに失敗しました: {ex.Message}");
                return Task.FromResult<GameSaveData>(null);
            }
        }

        /// <summary>
        /// セーブデータに含めたい情報を取得し、まとめてGameSaveDataに変換する
        /// </summary>
        /// <returns>作成したゲームデータ</returns>
        private async Task<GameSaveData> CreateGameSaveDataAsync()
        {
            GameSaveData data = new GameSaveData();
            
            data.InventorySaveData = await _inventoryRepository.LoadInventoryAsync();
            data.SpecialItemSaveData = await _specialItemRepository.LoadSpecialItemAsync();
            
            return data;
        }
    }

}