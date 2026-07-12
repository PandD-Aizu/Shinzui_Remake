using System;
using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.View;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    public class PlayerThrowPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly InventoryUseCase _inventoryUseCase;
        private readonly PlayerThrowUseCase _throwUseCase;
        private readonly PlayerThrowView _view;
        private readonly IInputService _inputService;

        private IDisposable _disposable;
        private bool _isStoneEquipped;

        public PlayerThrowPresenter(
            InventoryUseCase inventoryUseCase,
            PlayerThrowUseCase throwUseCase,
            PlayerThrowView view,
            IInputService inputService)
        {
            _inventoryUseCase = inventoryUseCase;
            _throwUseCase = throwUseCase;
            _view = view;
            _inputService = inputService;
        }

        public void Initialize()
        {
            var builder = Disposable.CreateBuilder();

            // 装備アイテムの変更を監視して、手元ビジュアルを同期する
            _inventoryUseCase.EquippedItemId
                .Subscribe(itemId =>
                {
                    _isStoneEquipped = (itemId == "stone");
                    _view.SetEquippedVisualActive(_isStoneEquipped);
                })
                .AddTo(ref builder);

            // 石の衝突を検知して効果音を再生する
            _view.OnStoneCollision
                .Subscribe(_ =>
                {
                    _throwUseCase.PlayStoneHitSound();
                })
                .AddTo(ref builder);

            _disposable = builder.Build();
        }

        public void Tick()
        {
            // インベントリが開いておらず、石が装備されていて、左クリック入力があった場合
            if (!_inventoryUseCase.IsOpen.CurrentValue && _isStoneEquipped && _inputService.AttackPressed)
            {
                // 石を投げる
                _view.ThrowRock();

                // 装備スロットから消費する
                _ = _inventoryUseCase.ConsumeEquippedItemAsync();
            }
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
