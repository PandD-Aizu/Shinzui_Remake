using System.Linq;
using System.Threading.Tasks;
using R3;
using Shinzui.Application.DTOs.Inventory;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Domain.Entities.Inventory;
using Shinzui.Domain.ValueObjects.Inventory;

namespace Shinzui.Application.UseCases.Inventory
{
    public class InventoryUseCase
    {
        private readonly InventoryEntity _inventory;
        private readonly IItemCatalog _catalog;
        private readonly IInventoryRepository _repository;

        // 各スロットの状態変更を Presenter 向けに DTO で公開する
        private readonly ReactiveProperty<InventorySlotDto>[] _slotDtos;
        public ReadOnlyReactiveProperty<InventorySlotDto> GetSlotDto(int index) => _slotDtos[index];
        public int Capacity => _inventory.Capacity;

        // インベントリ画面の開閉状態の保持
        private readonly ReactiveProperty<bool> _isOpen = new(false);
        public ReadOnlyReactiveProperty<bool> IsOpen => _isOpen;

        // 装備状態の保持
        private readonly ReactiveProperty<int> _equippedSlotIndex = new(-1);
        public ReadOnlyReactiveProperty<int> EquippedSlotIndex => _equippedSlotIndex;
        public ReadOnlyReactiveProperty<string> EquippedItemId { get; }

        public InventoryUseCase(
            InventoryEntity inventory,
            IItemCatalog catalog,
            IInventoryRepository repository)
        {
            _inventory = inventory;
            _catalog = catalog;
            _repository = repository;

            // 装備アイテムIDのストリームを構築
            EquippedItemId = _equippedSlotIndex
                .Select(index => index >= 0 
                    ? _inventory.GetSlot(index).Select(stack => stack?.Item.Id) 
                    : Observable.Return<string>(null))
                .Switch()
                .ToReadOnlyReactiveProperty();

            // 各スロットを DTO 変換ストリームとして監視・同期
            _slotDtos = new ReactiveProperty<InventorySlotDto>[_inventory.Capacity];
            for (int i = 0; i < _inventory.Capacity; i++)
            {
                int index = i;
                _slotDtos[i] = new ReactiveProperty<InventorySlotDto>(CreateDto(index, null));
                
                // Domain のスロット変更を検知して DTO を自動更新
                _inventory.GetSlot(index)
                    .Subscribe(stack =>
                    {
                        _slotDtos[index].Value = CreateDto(index, stack);

                        // 装備中のスロットが空になったか、装備品でなくなった場合は装備を解除
                        if (_equippedSlotIndex.Value == index)
                        {
                            if (stack == null || stack.Item.Type != ItemType.Equipment)
                            {
                                _equippedSlotIndex.Value = -1;
                            }
                        }
                    });
            }
        }

        // 開閉トグルロジック
        public void ToggleOpen()
        {
            _isOpen.Value = !_isOpen.Value;
        }

        // 強制的な開閉
        public void SetOpen(bool open)
        {
            _isOpen.Value = open;
        }

        private InventorySlotDto CreateDto(int index, ItemStack stack)
        {
            if (stack == null)
            {
                return new InventorySlotDto(index, false, "", "", "", "", 0, 0, false, false);
            }
            return new InventorySlotDto(
                index,
                true,
                stack.Item.Id,
                stack.Item.Name,
                stack.Item.Description,
                stack.Item.IconAssetAddress,
                stack.Quantity,
                stack.Item.MaxStackSize,
                stack.Item.IsConsumable,
                stack.Item.Type == ItemType.Equipment
            );
        }

        public void EquipItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Capacity)
            {
                _equippedSlotIndex.Value = -1;
                return;
            }

            var slot = _inventory.GetSlot(slotIndex).CurrentValue;
            if (slot == null || slot.Item.Type != ItemType.Equipment)
            {
                _equippedSlotIndex.Value = -1;
                return;
            }

            _equippedSlotIndex.Value = slotIndex;
        }

        public void UnequipItem()
        {
            _equippedSlotIndex.Value = -1;
        }

        public async Task<bool> ConsumeEquippedItemAsync()
        {
            int index = _equippedSlotIndex.Value;
            if (index < 0) return false;

            var slot = _inventory.GetSlot(index).CurrentValue;
            if (slot == null) return false;

            bool success = _inventory.TryRemoveItem(index, 1);
            if (success)
            {
                await SaveAsync();
            }
            return success;
        }

        // UIアクション：アイテムの追加
        public async Task<bool> AddItemAsync(string itemId, int quantity)
        {
            var item = await _catalog.GetItemAsync(itemId);
            if (item == null) return false;

            bool success = _inventory.TryAddItem(item, quantity, out int remaining);
            if (success)
            {
                await SaveAsync();
            }
            return success;
        }

        // UIアクション：スロット間の並び替え/マージ
        public async Task SwapOrMergeSlotsAsync(int fromIndex, int toIndex)
        {
            _inventory.SwapOrMergeSlots(fromIndex, toIndex);
            await SaveAsync();
        }

        // UIアクション：アイテムの使用
        public async Task<bool> UseItemAsync(int slotIndex)
        {
            var slot = _inventory.GetSlot(slotIndex).CurrentValue;
            if (slot == null || !slot.Item.IsConsumable) return false;

            // ※実際のゲームではここでHP回復などの使用効果（別ユースケースなど）を呼び出します。
            
            // 使用したため個数を1減らす
            bool success = _inventory.TryRemoveItem(slotIndex, 1);
            if (success)
            {
                await SaveAsync();
            }
            return success;
        }

        // セーブ処理
        private async Task SaveAsync()
        {
            var saveData = new InventorySaveData
            {
                Slots = Enumerable.Range(0, Capacity)
                    .Select(i => _inventory.GetSlot(i).CurrentValue)
                    .Select((stack, i) => new InventorySlotSaveData
                    {
                        SlotIndex = i,
                        ItemId = stack?.Item.Id ?? "",
                        Quantity = stack?.Quantity ?? 0
                    })
                    .Where(x => !string.IsNullOrEmpty(x.ItemId))
                    .ToArray()
            };
            await _repository.SaveInventoryAsync(saveData);
        }

        // ロード処理
        public async Task LoadAsync()
        {
            var saveData = await _repository.LoadInventoryAsync();
            if (saveData == null || saveData.Slots == null) return;

            // 一度クリア
            for (int i = 0; i < Capacity; i++)
            {
                _inventory.SetSlotForce(i, null);
            }

            foreach (var slotData in saveData.Slots)
            {
                var item = await _catalog.GetItemAsync(slotData.ItemId);
                if (item != null)
                {
                    _inventory.SetSlotForce(slotData.SlotIndex, new ItemStack(item, slotData.Quantity));
                }
            }
        }
    }
}
