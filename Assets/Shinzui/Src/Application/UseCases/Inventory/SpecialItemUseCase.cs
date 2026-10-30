using System.Threading.Tasks;
using R3;
using Shinzui.Application.DTOs.Inventory;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Domain.Entities;
using Shinzui.Domain.Entities.Inventory;
using Shinzui.Domain.ValueObjects.Inventory;

namespace Shinzui.Application.UseCases.Inventory
{
    /// <summary>
    /// 特殊アイテムの取得、交換、死亡防止効果の消費を扱うユースケース
    /// </summary>
    public class SpecialItemUseCase
    {
        private readonly SpecialItemEntity _specialItem;
        private readonly IItemCatalog _catalog;
        private readonly ISpecialItemRepository _repository;
        private readonly PlayerEntity _player;
        private readonly ReactiveProperty<SpecialItemSlotDto> _slotDto;

        public ReadOnlyReactiveProperty<SpecialItemSlotDto> SlotDto => _slotDto;

        /// <summary>身代わりの消費が成功したときだけ通知する</summary>
        public event System.Action DeathPreventionConsumed;

        public SpecialItemUseCase(
            SpecialItemEntity specialItem,
            IItemCatalog catalog,
            ISpecialItemRepository repository,
            PlayerEntity player)
        {
            _specialItem = specialItem;
            _catalog = catalog;
            _repository = repository;
            _player = player;
            _slotDto = new ReactiveProperty<SpecialItemSlotDto>(CreateSlotDto(null));

            _specialItem.HeldItem.Subscribe(ApplyHeldItem);
        }

        /// <summary>
        /// 取得時に、既存の特殊アイテムは消滅して新しいものへ置き換わる
        /// </summary>
        public async Task<SpecialItemAcquireResult> AcquireAsync(string itemId)
        {
            var item = await _catalog.GetItemAsync(itemId) as SpecialItemDefinition;
            if (item == null)
            {
                return new SpecialItemAcquireResult(false, null, null);
            }

            var replaced = _specialItem.Replace(item);
            await SaveAsync();
            return new SpecialItemAcquireResult(true, item.Id, replaced?.Id);
        }

        public async Task LoadAsync()
        {
            var saveData = await _repository.LoadSpecialItemAsync();
            if (saveData == null || string.IsNullOrEmpty(saveData.ItemId))
            {
                _specialItem.Clear();
                return;
            }

            var item = await _catalog.GetItemAsync(saveData.ItemId) as SpecialItemDefinition;
            if (item != null)
            {
                _specialItem.Replace(item);
            }
            else
            {
                _specialItem.Clear();
                await SaveAsync();
            }
        }

        /// <summary>
        /// 死亡確定の直前に呼ぶ
        /// 防止できた場合は特殊アイテムを消費してtrueを返す
        /// </summary>
        /// <returns>身代わりを消費して死亡を防いだ場合はtrue</returns>
        public bool TryConsumeDeathPrevention()
        {
            if (!_specialItem.TryConsumeDeathPrevention(out _))
            {
                return false;
            }

            // 消費が成功した時点で結果集計へ通知する
            DeathPreventionConsumed?.Invoke();

            // 消費後の所持状態を保存する
            _ = SaveAsync();
            return true;
        }

        public async Task ClearAsync()
        {
            _specialItem.Clear();
            await SaveAsync();
        }

        private Task SaveAsync()
        {
            return _repository.SaveSpecialItemAsync(CreateSaveData());
        }

        /// <summary>現在の特殊アイテムを保存用データへ変換する</summary>
        /// <returns>保持中の特殊アイテムID</returns>
        public SpecialItemSaveData CreateSaveData() => new SpecialItemSaveData
        {
            ItemId = _specialItem.HeldItem.CurrentValue?.Id ?? ""
        };

        /// <summary>特殊アイテムを復元して効果と表示へ反映する</summary>
        /// <param name="data">復元対象の特殊アイテム</param>
        /// <returns>復元完了タスク</returns>
        public async Task SetSpecialItemData(SpecialItemSaveData data)
        {
            await _repository.SaveSpecialItemAsync(data ?? new SpecialItemSaveData { ItemId = "" });
            await LoadAsync();
        }

        /// <summary>
        /// 特殊アイテムの状態をプレイヤーに反映する
        /// </summary>
        /// <param name="item"></param>
        private void ApplyHeldItem(SpecialItemDefinition item)
        {
            _player.SetSpecialItemModifiers(item?.Modifiers ?? SpecialItemModifiers.None);
            _slotDto.Value = CreateSlotDto(item);
        }

        /// <summary>
        /// 特殊アイテムの状態をDTOに変換する
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private static SpecialItemSlotDto CreateSlotDto(SpecialItemDefinition item)
        {
            if (item == null)
            {
                return new SpecialItemSlotDto(false, "", "", "", "", false);
            }

            return new SpecialItemSlotDto(true, item.Id, item.Name, item.Description, item.IconAssetAddress, item.Modifiers.PreventDeathOnce);
        }
    }
}
