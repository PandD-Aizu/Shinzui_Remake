using System;
using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using Shinzui.View;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    public class PlayerDeathPresenter : IInitializable, IDisposable
    {
        private readonly PlayerDeathUseCase _useCase;
        private readonly PlayerDeathView _view;
        private readonly IInputService _inputService;
        private IDisposable _disposable;

        public PlayerDeathPresenter(
            PlayerDeathUseCase useCase,
            PlayerDeathView view,
            IInputService inputService)
        {
            _useCase = useCase;
            _view = view;
            _inputService = inputService;
        }

        public void Initialize()
        {
            _disposable = _useCase.IsDead
                .Subscribe(isDead =>
                {
                    if (!isDead)
                    {
                        return;
                    }

                    _inputService.SetBlocked(true);
                    _view.PlayDeathSequence();
                });
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
