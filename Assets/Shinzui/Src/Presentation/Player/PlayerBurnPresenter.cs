using System;
using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.View;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    public class PlayerBurnPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly InventoryUseCase _inventoryUseCase;
        private readonly PlayerBurnUseCase _burnUseCase;
        private readonly PlayerBurnView _view;
        private readonly IInputService _inputService;

        private IDisposable _disposable;
        private bool _isStoneEquipped;

        public PlayerBurnPresenter(
            InventoryUseCase inventoryUseCase,
            PlayerBurnUseCase burnUseCase,
            PlayerBurnView view,
            IInputService inputService)
        {
            _inventoryUseCase = inventoryUseCase;
            _burnUseCase = burnUseCase;
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

            // 蜘蛛の巣との衝突を検知して効果音を再生する
            _view.OnMatchStickCollision
                .Subscribe(_ =>
                {
                    _burnUseCase.PlayBurningSpiderwebSound();
                })
                .AddTo(ref builder);

            _disposable = builder.Build();
        }

        public void Tick()
        {
            // インベントリが開いておらず、マッチ棒が装備されていて、左クリック入力があった場合
            if (!_inventoryUseCase.IsOpen.CurrentValue && _isStoneEquipped && _inputService.AttackPressed)
            {
                // 蜘蛛の巣を燃やす
                _view.BurnSpiderweb();

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
