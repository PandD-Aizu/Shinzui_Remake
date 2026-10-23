using UnityEngine;
using System.Threading.Tasks;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Application.UseCases.Inventory;

namespace Shinzui.Infrastructure.SaveSystem
{
    /// <summary>
    /// ゲームのセーブ & ロード処理を行う
    /// </summary>
    public class SaveManager
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly ISpecialItemRepository _specialItemRepository;
        private readonly InventoryUseCase _inventoryUseCase;
        private readonly SpecialItemUseCase _specialItemUseCase;
    
        private const string SaveKey = "shinzui_savedata_";
        private const int SaveDataSlotNum = 3;
        public int SaveSlotNum => SaveDataSlotNum;

        public SaveManager(
            IInventoryRepository inventoryRepository,
            ISpecialItemRepository specialItemRepository,
            InventoryUseCase inventoryUseCase,
            SpecialItemUseCase specialItemUseCase)
        {
            _inventoryRepository = inventoryRepository;
            _specialItemRepository = specialItemRepository;
            _inventoryUseCase = inventoryUseCase;
            _specialItemUseCase = specialItemUseCase;
        }

        /// <summary>
        /// セーブスロットを指定してセーブ
        /// </summary>
        /// <param name="idx">セーブスロットのインデックス</param>
        public async void Save(int idx)
        {
            GameSaveData data = await CreateGameSaveDataAsync();
            await SaveGameDataAsync(idx, data);
        }

        /// <summary>
        /// セーブスロットを指定してロード
        /// </summary>
        /// <param name="idx">セーブスロットのインデックス</param>
        /// <returns>ロードしたゲームデータ</returns>
        public async Task<GameSaveData> Load(int idx)
        {
            GameSaveData data = await LoadGameDataAsync(idx);
            await _inventoryUseCase.SetInventoryData(data.InventorySaveData);
            await _specialItemUseCase.SetSpecialItemData(data.SpecialItemSaveData);
            return data;
        }
    
        /// <summary>
        /// セーブデータをjsonファイルに変換しPlayerPrefsに保存する
        /// </summary>
        /// <param name="idx">セーブスロットのインデックス</param>
        /// <param name="data">セーブデータ</param>
        /// <returns></returns>
        private Task SaveGameDataAsync(int idx, GameSaveData data)
        {
            if (idx > SaveDataSlotNum)
            {
                Debug.LogError($"[SaveManager] セーブスロットの指定が範囲外です");
                return Task.CompletedTask;
            }
            
            try
            {
                string json = JsonUtility.ToJson(data);
                Debug.Log($"SaveManager Save: key={SaveKey + idx}, data={json}");
                PlayerPrefs.SetString(SaveKey + idx, json);
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
        /// <param name="idx">セーブスロットのインデックス</param>
        /// <returns>指定したセーブスロットのゲームデータ</returns>
        private Task<GameSaveData> LoadGameDataAsync(int idx)
        {
            if (!PlayerPrefs.HasKey(SaveKey + idx))
            {
                Debug.Log("SaveManager Load: " + SaveKey + idx + " not found");
                return Task.FromResult<GameSaveData>(null);
            }

            try
            {
                string json = PlayerPrefs.GetString(SaveKey + idx);
                Debug.Log($"SaveManager Load: key={SaveKey + idx}, data={json}");
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