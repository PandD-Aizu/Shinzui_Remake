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
        public bool TryConsumeDeathPrevention()
        {
            if (!_specialItem.TryConsumeDeathPrevention(out _))
            {
                return false;
            }

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
            var heldItem = _specialItem.HeldItem.CurrentValue;
            return _repository.SaveSpecialItemAsync(new SpecialItemSaveData
            {
                ItemId = heldItem?.Id ?? ""
            });
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
