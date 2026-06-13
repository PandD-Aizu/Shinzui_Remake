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
        private readonly IFMODSEService _fmodSeService;

        private IDisposable _disposable;

        public FlashlightPresenter(
            FlashlightUseCase useCase,
            FlashlightView view,
            IInputService inputService,
            IFMODSEService fmodSeService)
        {
            _useCase = useCase;
            _view = view;
            _inputService = inputService;
            _fmodSeService = fmodSeService;
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

            // トグル時の効果音再生を同期（初期状態の発火はスキップ）
            _useCase.IsOn
                .Skip(1)
                .Subscribe(isOn =>
                {
                    if (!_view.ToggleEvent.IsNull)
                    {
                        _fmodSeService.PlayOneShot(_view.ToggleEvent);
                    }
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
