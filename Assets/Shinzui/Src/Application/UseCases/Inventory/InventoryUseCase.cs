using System.Linq;
using System.Threading.Tasks;
using R3;
using Shinzui.Application.DTOs.Inventory;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Domain.Entities;
using Shinzui.Domain.Entities.Flashlight;
using Shinzui.Domain.Entities.Inventory;
using Shinzui.Domain.ValueObjects.Inventory;

namespace Shinzui.Application.UseCases.Inventory
{
    public class InventoryUseCase
    {
        private readonly InventoryEntity _inventory;
        private readonly IItemCatalog _catalog;
        private readonly IInventoryRepository _repository;
        private readonly PlayerEntity _playerEntity;
        private readonly IPenlightPlacementService _penlightPlacement;
        private readonly FlashlightEntity _flashlight;

        private readonly ReactiveProperty<InventorySlotDto>[] _slotDtos;
        public ReadOnlyReactiveProperty<InventorySlotDto> GetSlotDto(int index) => _slotDtos[index];
        public int Capacity => _inventory.Capacity;

        // アイテム獲得通知ストリーム
        private readonly Subject<ItemGetDto> _onItemGot = new();
        public Observable<ItemGetDto> OnItemGot => _onItemGot;

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
            IInventoryRepository repository,
            PlayerEntity playerEntity,
            IPenlightPlacementService penlightPlacement = null,
            FlashlightEntity flashlight = null)
        {
            _inventory = inventory;
            _catalog = catalog;
            _repository = repository;
            _playerEntity = playerEntity;
            _penlightPlacement = penlightPlacement;
            _flashlight = flashlight;

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

                        // 装備中のスロットが空になった場合は装備を解除
                        if (_equippedSlotIndex.Value == index)
                        {
                            if (stack == null)
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

        /// <summary>
        /// スロットインデックスとアイテムスタックから DTO を生成する
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <param name="stack">アイテムスタック</param>
        /// <returns></returns>
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

        /// <summary>
        /// 装備アイテムを指定スロットに設定する
        /// </summary>
        /// <param name="slotIndex">スロットインデックス</param>
        public void EquipItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Capacity)
            {
                _equippedSlotIndex.Value = -1;
                return;
            }

            var slot = _inventory.GetSlot(slotIndex).CurrentValue;
            if (slot == null || slot.Item.Type == ItemType.Special)
            {
                _equippedSlotIndex.Value = -1;
                return;
            }

            _equippedSlotIndex.Value = slotIndex;
        }

        /// <summary>
        /// 装備アイテムを解除する
        /// </summary>
        public void UnequipItem()
        {
            _equippedSlotIndex.Value = -1;
        }

        /// <summary>
        /// 装備中のアイテムを消費する
        /// </summary>
        /// <returns></returns>
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
        
        /// <summary>
        /// アイテムの追加
        /// </summary>
        /// <param name="itemId">アイテムID</param>
        /// <param name="quantity">数量</param>
        /// <returns></returns>
        public async Task<bool> AddItemAsync(string itemId, int quantity)
        {
            var item = await _catalog.GetItemAsync(itemId);
            if (item == null) return false;
            if (item.Type == ItemType.Special) return false;

            bool success = _inventory.TryAddItem(item, quantity, out int remaining);
            if (success)
            {
                await SaveAsync();
                _onItemGot.OnNext(new ItemGetDto(item.Name, item.IconAssetAddress));
            }
            
            return success;
        }
        
        /// <summary>
        /// スロット間の並び替えまたはマージを行う
        /// </summary>
        /// <param name="fromIndex">移動元のスロットインデックス</param>
        /// <param name="toIndex">移動先のスロットインデックス</param>
        public async Task SwapOrMergeSlotsAsync(int fromIndex, int toIndex)
        {
            _inventory.SwapOrMergeSlots(fromIndex, toIndex);
            await SaveAsync();
        }

        /// <summary>
        /// アイテムの使用
        /// </summary>
        /// <param name="slotIndex">スロットインデックス</param>
        /// <returns></returns>
        public async Task<bool> UseItemAsync(int slotIndex)
        {
            var slot = _inventory.GetSlot(slotIndex).CurrentValue;
            if (slot == null || !slot.Item.IsConsumable) return false;

            // 乾電池は懐中電灯の電池残量が満タンなら使わない
            bool isBattery = slot.Item.Id == "FlashlightBattery";
            if (isBattery && (_flashlight == null || _flashlight.StrobeEnergy.CurrentValue >= 1f)) return false;

            // 使用したため個数を1減らす
            bool success = _inventory.TryRemoveItem(slotIndex, 1);
            if (success)
            {
                // 乾電池を交換してストロボの電池残量を満タンにする
                if (isBattery)
                {
                    _flashlight.TryRefillStrobeEnergy();
                }

                // ぼたもちを食べた時だけ全回復し、5秒間の消費停止を付与する
                if (slot.Item.Id == "potion_botamochi")
                {
                    _playerEntity?.RestoreStaminaWithProtection(5f);
                }

                await SaveAsync();
            }
            
            return success;
        }

        /// <summary>
        /// 装備中のペンライトを設置し、成功時だけ1本消費する
        /// </summary>
        /// <returns>設置と消費が成功したか</returns>
        public async Task<bool> PlaceEquippedPenlightAsync()
        {
            int index = _equippedSlotIndex.Value;
            if (index < 0 || _inventory.GetSlot(index).CurrentValue?.Item.Id != "penlight"
                || _penlightPlacement == null || !_penlightPlacement.TryPlace()) return false;

            // 設置から消費までは待機を挟まず、最後の1本なら装備も解除する
            _inventory.TryRemoveItem(index, 1);
            await SaveAsync();
            return true;
        }

        /// <summary>
        /// セーブ処理
        /// </summary>
        /// <returns>メモリ上の持ち物更新の完了タスク</returns>
        private Task SaveAsync() => _repository.SaveInventoryAsync(CreateSaveData());

        /// <summary>現在の持ち物と装備スロットを保存用データへ変換する</summary>
        /// <returns>現在のインベントリのスナップショット</returns>
        public InventorySaveData CreateSaveData()
        {
            return new InventorySaveData
            {
                EquippedSlotIndex = _equippedSlotIndex.Value,
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
        }

        /// <summary>
        /// ロード処理
        /// </summary>
        public async Task LoadAsync()
        {
            var saveData = await _repository.LoadInventoryAsync();
            await SetInventoryData(saveData ?? new InventorySaveData { Slots = System.Array.Empty<InventorySlotSaveData>() });
        }

        /// <summary>
        /// InventorySaveDataの内容をインベントリに反映する
        /// </summary>
        /// <param name="data">復元する持ち物と装備</param>
        /// <returns>復元完了タスク</returns>
        public async Task SetInventoryData(InventorySaveData data)
        {
            // 復元前にスロット範囲を検証する
            if (data?.Slots == null) throw new System.IO.InvalidDataException("持ち物データが存在しない");
            if (data.Slots.Any(slot => slot.SlotIndex < 0 || slot.SlotIndex >= Capacity))
                throw new System.IO.InvalidDataException("持ち物のスロット番号が範囲外");

            // 一度クリア
            UnequipItem();
            for (int i = 0; i < Capacity; i++)
            {
                _inventory.SetSlotForce(i, null);
            }

            foreach (var slotData in data.Slots)
            {
                var item = await _catalog.GetItemAsync(slotData.ItemId);
                if (item != null)
                {
                    _inventory.SetSlotForce(slotData.SlotIndex, new ItemStack(item, slotData.Quantity));
                }
            }

            // 装備と作業中の持ち物を同期する
            EquipItem(data.EquippedSlotIndex);
            await SaveAsync();
        }
        
        /// <summary>
        /// インベントリ内にあるアイテムの合計個数を数える
        /// </summary>
        /// <param name="itemId">数えるアイテムID</param>
        /// <returns>全スロットの合計個数</returns>
        public int CountItem(string itemId)
        {
            int count = 0;
            foreach (var slot in _slotDtos)
            {
                if (slot.CurrentValue is { HasItem: true } dto && dto.ItemId == itemId) count += dto.Quantity;
            }

            return count;
        }

        /// <summary>
        /// インベントリ内のアイテムの情報取得
        /// </summary>
        /// <param name="itemId">調査対象のアイテムID</param>
        /// <returns></returns>
        public int CheckItem(string itemId)
        {
            //　TODO: (必要なら)カタログからitemIdで取得するように変更する
            var item =  new ItemDefinition("FlashlightBattery", "乾電池", "使い捨て電池、ストロボで使用する", "Sprite/Medicine", ItemType.Consumable, 99);
            var idx = _inventory.CheckItemSlotIndex(item);
            
            return idx;
        }
    }
}
