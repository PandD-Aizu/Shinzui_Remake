using System.Threading.Tasks;
using Shinzui.Application.Title;
using Shinzui.Application.UseCases.Inventory;

namespace Shinzui.Infrastructure.SaveSystem
{
    /// <summary>持ち物の取得、ファイル保存、ゲームへの復元を管理する</summary>
    public class SaveManager
    {
        private readonly InventoryUseCase _inventoryUseCase;
        private readonly SpecialItemUseCase _specialItemUseCase;
        private readonly SaveFileStore _store;
        public int SaveSlotNum => SaveFileStore.SlotCount;
        public int CurrentSlot { get; private set; }

        /// <summary>保存処理の依存を設定する</summary>
        /// <param name="inventoryUseCase">持ち物の操作</param>
        /// <param name="specialItemUseCase">特殊アイテムの操作</param>
        /// <param name="store">保存ファイルの操作</param>
        public SaveManager(InventoryUseCase inventoryUseCase, SpecialItemUseCase specialItemUseCase, SaveFileStore store)
        {
            _inventoryUseCase = inventoryUseCase;
            _specialItemUseCase = specialItemUseCase;
            _store = store;
            CurrentSlot = GameStartRequest.Slot ?? 0;
        }

        /// <summary>現在の持ち物を指定スロットに保存する</summary>
        /// <param name="idx">保存スロット番号</param>
        /// <returns>保存完了タスク</returns>
        public Task Save(int idx)
        {
            _store.Save(idx, new GameSaveData
            {
                InventorySaveData = _inventoryUseCase.CreateSaveData(),
                SpecialItemSaveData = _specialItemUseCase.CreateSaveData()
            });
            CurrentSlot = idx;
            return Task.CompletedTask;
        }

        /// <summary>指定スロットの持ち物をゲームへ復元する</summary>
        /// <param name="idx">保存スロット番号</param>
        /// <returns>復元した保存データ</returns>
        public async Task<GameSaveData> Load(int idx)
        {
            var data = _store.Load(idx);
            await Apply(data);
            CurrentSlot = idx;
            return data;
        }

        /// <summary>指定スロットのファイルを削除する</summary>
        /// <param name="idx">保存スロット番号</param>
        /// <returns>削除完了タスク</returns>
        public Task DeleteSaveData(int idx)
        {
            _store.Delete(idx);
            return Task.CompletedTask;
        }

        /// <summary>新規開始または選択スロットの初期状態を反映する</summary>
        /// <returns>初期化完了タスク</returns>
        public async Task InitializeAsync()
        {
            if (GameStartRequest.Slot.HasValue) await Load(GameStartRequest.Slot.Value);
            else await Apply(new GameSaveData());
        }

        /// <summary>通常アイテムと特殊アイテムを順に復元する</summary>
        /// <param name="data">復元する保存データ</param>
        /// <returns>反映完了タスク</returns>
        private async Task Apply(GameSaveData data)
        {
            await _inventoryUseCase.SetInventoryData(data.InventorySaveData);
            await _specialItemUseCase.SetSpecialItemData(data.SpecialItemSaveData);
        }
    }
}
