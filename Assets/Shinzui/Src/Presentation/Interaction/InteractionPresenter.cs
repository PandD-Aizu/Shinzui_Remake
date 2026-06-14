using System;
using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases.Interaction;
using Shinzui.View.Interaction;
using Shinzui.View;
using VContainer.Unity;

namespace Shinzui.Presentation.Interaction
{
    /// <summary>
    /// インタラクト用入力の監視と、UseCaseの結果をViewに流し込むPresenter
    /// </summary>
    public class InteractionPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly InteractionUseCase _useCase;
        private readonly IInputService _inputService;
        private readonly PlayerInteractionView _interactionView;
        private readonly InteractionMessageView _messageView;
        private readonly PlayerView _playerView;

        private IDisposable _disposable;

        public InteractionPresenter(
            InteractionUseCase useCase,
            IInputService inputService,
            PlayerInteractionView interactionView,
            InteractionMessageView messageView,
            PlayerView playerView)
        {
            _useCase = useCase;
            _inputService = inputService;
            _interactionView = interactionView;
            _messageView = messageView;
            _playerView = playerView;
        }

        public void Initialize()
        {
            var builder = Disposable.CreateBuilder();

            // UseCaseからのテロップ表示要求をViewへ反映する
            _useCase.OnShowMessage
                .Subscribe(message =>
                {
                    if (_messageView != null)
                    {
                        _messageView.ShowMessage(message);
                    }
                })
                .AddTo(ref builder);

            _disposable = builder.Build();
        }

        public void Tick()
        {
            // Eキー（またはコントローラーボタン）が押されたか判定
            if (_inputService.ItemUsePressed)
            {
                var target = _interactionView.CurrentInteractable;
                if (target != null && target.CanInteract)
                {
                    // UseCaseへインタラクトIDと表示メッセージを流す
                    _ = _useCase.InteractAsync(target.InteractableId, target.InteractMessage);
                    
                    // View側の物理的変化（一回限り化、オブジェクト破壊等）を実行
                    target.ExecuteInteractEffect();
                }
            }
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
