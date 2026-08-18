using System;
using System.Threading.Tasks;
using R3;
using Shinzui.Application.DTOs.Inventory;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.View;
using Shinzui.View.Interaction;
using UnityEngine;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    /**
     * TODO: 流石に実装がひどいので後でリファクタリングしておく
     * PlayerのHUDの右下にあるQuickItemSlotのPresenter
     */
    public class PlayerQuickItemSlotPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly InventoryUseCase _inventoryUseCase;
        private readonly SpecialItemUseCase _specialItemUseCase;
        private readonly IInputService _inputService;
        private readonly PlayerQuickItemSlotView _playerQuickItemSlotView;
        private PlayerInteractionView _interactionView;

        private IDisposable _disposable;
        private bool _isUsingItem;

        public PlayerQuickItemSlotPresenter(
            InventoryUseCase inventoryUseCase,
            SpecialItemUseCase specialItemUseCase,
            IInputService inputService,
            PlayerQuickItemSlotView playerQuickItemSlotView)
        {
            _inventoryUseCase = inventoryUseCase;
            _specialItemUseCase = specialItemUseCase;
            _inputService = inputService;
            _playerQuickItemSlotView = playerQuickItemSlotView;
        }

        public void Initialize()
        {
            _interactionView = UnityEngine.Object.FindFirstObjectByType<PlayerInteractionView>();

            var builder = Disposable.CreateBuilder();
            
            _inventoryUseCase.EquippedSlotIndex
                .Subscribe(_ => RefreshNormalItem())
                .AddTo(ref builder);

            for (int i = 0; i < _inventoryUseCase.Capacity; i++)
            {
                int index = i;
                _inventoryUseCase.GetSlotDto(index)
                    .Subscribe(_ =>
                    {
                        if (_inventoryUseCase.EquippedSlotIndex.CurrentValue == index)
                        {
                            RefreshNormalItem();
                        }
                    })
                    .AddTo(ref builder);
            }

            _specialItemUseCase.SlotDto
                .Subscribe(RefreshSpecialItem)
                .AddTo(ref builder);

            _disposable = builder.Build();

            RefreshNormalItem();
            RefreshSpecialItem(_specialItemUseCase.SlotDto.CurrentValue);
            _ = _specialItemUseCase.LoadAsync();
        }

        public void Tick()
        {
            if (_inventoryUseCase.IsOpen.CurrentValue)
            {
                return;
            }

            // Eキーで通常アイテムを使用
            if (!_isUsingItem && _inputService.ItemUsePressed && !IsInteractionTargetActive())
            {
                _ = TryUseEquippedNormalItemAsync();
            }

            // 通常アイテムはCtrl + マウスホイールでアイテムを切り替え
            if (_inputService.QuickItemModifierHeld)
            {
                int scrollDelta = _inputService.QuickItemScrollDelta;
                if (scrollDelta != 0)
                {
                    SelectNextNormalItem(scrollDelta);
                }
            }
        }

        /// <summary>
        /// 装備中の通常アイテムを使用する
        /// </summary>
        private async Task TryUseEquippedNormalItemAsync()
        {
            _isUsingItem = true;

            try
            {
                int equippedIndex = _inventoryUseCase.EquippedSlotIndex.CurrentValue;
                if (equippedIndex < 0)
                {
                    return;
                }

                var dto = _inventoryUseCase.GetSlotDto(equippedIndex).CurrentValue;
                if (dto == null || !dto.HasItem || !dto.IsConsumable)
                {
                    return;
                }

                await _inventoryUseCase.UseItemAsync(equippedIndex);
            }
            finally
            {
                _isUsingItem = false;
            }
        }

        /// <summary>
        /// 次の通常アイテムを選択する
        /// </summary>
        /// <param name="direction">選択方向 (1: 次, -1: 前)</param>
        private void SelectNextNormalItem(int direction)
        {
            int nextIndex = FindNextUsableSlotIndex(direction);
            if (nextIndex >= 0)
            {
                _inventoryUseCase.EquipItem(nextIndex);
            }
        }

        /// <summary>
        /// 次に使用可能な通常アイテムのスロットインデックスを見つける
        /// </summary>
        /// <param name="direction">選択方向 (1: 次, -1: 前)</param>
        /// <returns>スロットインデックス</returns>
        private int FindNextUsableSlotIndex(int direction)
        {
            int capacity = _inventoryUseCase.Capacity;
            if (capacity <= 0)
            {
                return -1;
            }

            int step = direction > 0 ? 1 : -1;
            int currentIndex = _inventoryUseCase.EquippedSlotIndex.CurrentValue;
            int startIndex = currentIndex >= 0 ? currentIndex : (step > 0 ? -1 : capacity);

            for (int offset = 1; offset <= capacity; offset++)
            {
                int candidateIndex = (startIndex + step * offset + capacity) % capacity;
                var dto = _inventoryUseCase.GetSlotDto(candidateIndex).CurrentValue;
                if (dto != null && dto.HasItem)
                {
                    return candidateIndex;
                }
            }

            return -1;
        }

        /// <summary>
        /// 通常アイテムを表示更新
        /// </summary>
        private void RefreshNormalItem()
        {
            int equippedIndex = _inventoryUseCase.EquippedSlotIndex.CurrentValue;
            if (equippedIndex < 0)
            {
                _playerQuickItemSlotView.ClearNormalItem();
                return;
            }

            var dto = _inventoryUseCase.GetSlotDto(equippedIndex).CurrentValue;
            if (dto is { HasItem: true })
            {
                _playerQuickItemSlotView.SetNormalItem(dto.IconAssetAddress);
            }
            else
            {
                _playerQuickItemSlotView.ClearNormalItem();
            }
        }

        /// <summary>
        /// 特殊アイテムの表示更新
        /// </summary>
        /// <param name="dto"></param>
        private void RefreshSpecialItem(SpecialItemSlotDto dto)
        {
            if (dto is { HasItem: true })
            {
                _playerQuickItemSlotView.SetSpecialItem(dto.IconAssetAddress);
            }
            else
            {
                _playerQuickItemSlotView.ClearSpecialItem();
            }
        }

        /// <summary>
        /// インタラクション対象がアクティブかどうかを判定する
        /// </summary>
        /// <returns></returns>
        private bool IsInteractionTargetActive()
        {
            return _interactionView != null &&
                   _interactionView.CurrentInteractable != null &&
                   _interactionView.CurrentInteractable.CanInteract;
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
