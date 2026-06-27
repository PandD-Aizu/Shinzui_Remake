using System;
using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases.Flashlight;
using Shinzui.View.Flashlight;
using VContainer.Unity;

namespace Shinzui.Presentation.Flashlight
{
    public class FlashlightPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly FlashlightUseCase _useCase;
        private readonly FlashlightView _view;
        private readonly IInputService _inputService;

        private IDisposable _disposable;

        public FlashlightPresenter(
            FlashlightUseCase useCase,
            FlashlightView view,
            IInputService inputService)
        {
            _useCase = useCase;
            _view = view;
            _inputService = inputService;
        }

        public void Initialize()
        {
            var builder = Disposable.CreateBuilder();

            // UseCaseの状態変更を監視し、Viewのライト表示を同期
            _useCase.IsOn
                .Subscribe(isOn =>
                {
                    _view.SetLightActive(isOn);
                })
                .AddTo(ref builder);

            _disposable = builder.Build();
        }

        // 毎フレームのFキー入力を監視
        public void Tick()
        {
            if (_inputService.FlashlightTogglePressed)
            {
                _useCase.ToggleFlashlight();
            }
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
