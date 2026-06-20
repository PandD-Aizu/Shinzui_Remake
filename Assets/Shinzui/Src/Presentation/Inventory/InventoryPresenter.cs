using System;
using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.View.Inventory;
using VContainer.Unity;

namespace Shinzui.Presentation.Inventory
{
    public class InventoryPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly InventoryUseCase _useCase;
        private readonly InventoryView _view;
        private readonly IInputService _inputService;

        private readonly ReactiveProperty<int> _selectedSlotIndex = new(-1);
        private IDisposable _disposable;

        public InventoryPresenter(
            InventoryUseCase useCase,
            InventoryView view,
            IInputService inputService)
        {
            _useCase = useCase;
            _view = view;
            _inputService = inputService;
        }

        public void Initialize()
        {
            // 最初にインベントリ容量に合わせてViewのスロットを自動生成する
            _view.InitializeSlots(_useCase.Capacity);

            var builder = Disposable.CreateBuilder();

            // 開閉状態の同期 (UseCase.IsOpen -> View.Show/Hide & Input Block)
            _useCase.IsOpen
                .Subscribe(isOpen =>
                {
                    if (isOpen)
                    {
                        _view.Show();
                        _inputService.SetBlocked(true); // 移動・視点移動の入力をブロック
                    }
                    else
                    {
                        _view.Hide();
                        _inputService.SetBlocked(false); // 入力ブロックを解除
                    }
                    
                    if (!isOpen)
                    {
                        _selectedSlotIndex.Value = -1;
                        _view.HideContextMenu(); // 閉じたらコンテキストメニューも非表示
                    }
                })
                .AddTo(ref builder);

            // 各スロットのデータ同期 (UseCase -> View)
            for (int i = 0; i < _useCase.Capacity; i++)
            {
                int index = i;
                var slotView = _view.GetSlotView(index);
                if (slotView == null) continue;

                _useCase.GetSlotDto(index)
                    .Subscribe(dto =>
                    {
                        if (dto.HasItem)
                        {
                            slotView.SetItem(dto.IconAssetAddress, dto.Quantity);
                        }
                        else
                        {
                            slotView.ClearSlot();
                        }
                    })
                    .AddTo(ref builder);

                // スロットクリックイベントの監視
                slotView.OnClick
                    .Subscribe(clickedIndex =>
                    {
                        SelectSlot(clickedIndex);

                        var dto = _useCase.GetSlotDto(clickedIndex).CurrentValue;
                        if (dto.HasItem)
                        {
                            _view.SetContextMenuButtons(dto.IsConsumable, dto.IsEquipment);
                            _view.ShowContextMenu(slotView.transform.position);
                        }
                        else
                        {
                            _view.HideContextMenu();
                        }
                    })
                    .AddTo(ref builder);

                // スロットドラッグ＆ドロップの監視
                slotView.OnDragDrop
                    .Subscribe(async dragData =>
                    {
                        await _useCase.SwapOrMergeSlotsAsync(dragData.fromIndex, dragData.toIndex);
                    })
                    .AddTo(ref builder);
            }

            // 選択表示の切り替え
            _selectedSlotIndex
                .Subscribe(index =>
                {
                    for (int i = 0; i < _view.SlotCount; i++)
                    {
                        var slotView = _view.GetSlotView(i);
                        if (slotView != null)
                        {
                            slotView.SetSelection(i == index);
                        }
                    }
                })
                .AddTo(ref builder);

            // 選択されたスロットに対応する詳細表示の更新
            _selectedSlotIndex
                .Subscribe(index =>
                {
                    UpdateSelectedItemDetails(index);
                })
                .AddTo(ref builder);

            // 選択中のスロットのデータが変化した場合も詳細表示を再更新する
            for (int i = 0; i < _useCase.Capacity; i++)
            {
                int index = i;
                _useCase.GetSlotDto(index)
                    .Subscribe(dto =>
                    {
                        if (_selectedSlotIndex.Value == index)
                        {
                            UpdateSelectedItemDetails(index);
                        }
                    })
                    .AddTo(ref builder);
            }

            // コンテキストメニューの「使用する」ボタンクリック時の処理
            OnButtonClicked(_view.UseButton)
                .Subscribe(async _ =>
                {
                    _view.HideContextMenu();
                    int selected = _selectedSlotIndex.Value;
                    if (selected >= 0)
                    {
                        var dto = _useCase.GetSlotDto(selected).CurrentValue;
                        if (dto.HasItem && dto.IsConsumable)
                        {
                            await _useCase.UseItemAsync(selected);
                        }
                    }
                })
                .AddTo(ref builder);

            // コンテキストメニューの「装備する」ボタンクリック時の処理
            if (_view.EquipButton != null)
            {
                OnButtonClicked(_view.EquipButton)
                    .Subscribe(_ =>
                    {
                        _view.HideContextMenu();
                        int selected = _selectedSlotIndex.Value;
                        if (selected >= 0)
                        {
                            var dto = _useCase.GetSlotDto(selected).CurrentValue;
                            if (dto.HasItem && dto.IsEquipment)
                            {
                                _useCase.EquipItem(selected);
                            }
                        }
                    })
                    .AddTo(ref builder);
            }

            // 装備中スロットの同期 (UseCase -> View)
            _useCase.EquippedSlotIndex
                .Subscribe(equippedIndex =>
                {
                    for (int i = 0; i < _view.SlotCount; i++)
                    {
                        var slotView = _view.GetSlotView(i);
                        if (slotView != null)
                        {
                            slotView.SetEquipped(i == equippedIndex);
                        }
                    }
                })
                .AddTo(ref builder);

            // コンテキストメニューの背景クリックで閉じる処理
            OnButtonClicked(_view.ContextMenuBackgroundButton)
                .Subscribe(_ =>
                {
                    _view.HideContextMenu();
                })
                .AddTo(ref builder);

            _disposable = builder.Build();

            // 初期状態は非表示
            _view.Hide();
            LoadInitialData();
        }

        // 毎フレーム入力を監視し、Tab押下時にトグル、またはEキー押下時にアイテム使用
        public void Tick()
        {
            // Tabキー押下による開閉トグル
            if (_inputService.InventoryTogglePressed)
            {
                _useCase.ToggleOpen();
            }

            // インベントリが開いている場合のみEキーの入力を検出
            if (_useCase.IsOpen.CurrentValue && _inputService.ItemUsePressed)
            {
                _ = TryUseSelectedSlotAsync();
            }
        }

        private async System.Threading.Tasks.Task TryUseSelectedSlotAsync()
        {
            int selected = _selectedSlotIndex.Value;
            if (selected >= 0)
            {
                var dto = _useCase.GetSlotDto(selected).CurrentValue;
                // 消費アイテムである場合のみ実行
                if (dto.HasItem && dto.IsConsumable)
                {
                    bool success = await _useCase.UseItemAsync(selected);
                    if (success)
                    {
                        // 使用成功時の演出・SE再生等をここで行う
                    }
                }
            }
        }

        private async void LoadInitialData()
        {
            await _useCase.LoadAsync();
        }

        private void SelectSlot(int index)
        {
            _selectedSlotIndex.Value = index;
        }

        private void UpdateSelectedItemDetails(int index)
        {
            if (index < 0 || index >= _useCase.Capacity)
            {
                _view.ClearDetailedInfo();
                return;
            }

            var dto = _useCase.GetSlotDto(index).CurrentValue;
            if (dto != null && dto.HasItem)
            {
                _view.UpdateDetailedInfo(dto.ItemName, dto.Description, dto.IconAssetAddress);
            }
            else
            {
                _view.ClearDetailedInfo();
            }
        }

        private Observable<Unit> OnButtonClicked(UnityEngine.UI.Button button)
        {
            return Observable.Create<Unit>(observer =>
            {
                UnityEngine.Events.UnityAction action = () => observer.OnNext(Unit.Default);
                button.onClick.AddListener(action);
                return Disposable.Create(() => button.onClick.RemoveListener(action));
            });
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
