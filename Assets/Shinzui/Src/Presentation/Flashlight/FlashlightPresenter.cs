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
        private readonly StrobeGaugeView _strobeGaugeView;

        private IDisposable _disposable;

        public FlashlightPresenter(
            FlashlightUseCase useCase,
            FlashlightView view,
            IInputService inputService,
            StrobeGaugeView strobeGaugeView)
        {
            _useCase = useCase;
            _view = view;
            _inputService = inputService;
            _strobeGaugeView = strobeGaugeView;
        }

        private bool _wasAttackHeld;

        public void Initialize()
        {
            var builder = Disposable.CreateBuilder();

            // 懐中電灯のオンオフ状態を監視し、ライトのアクティブ状態を更新
            _useCase.IsOn
                .Subscribe(isOn =>
                {
                    _view.SetLightActive(isOn || _useCase.StrobeIntensity.CurrentValue > 0f);
                })
                .AddTo(ref builder);

            // ストロボ強度を監視し、ライトのアクティブ状態と明るさを更新
            _useCase.StrobeIntensity
                .Subscribe(strobeVal =>
                {
                    _view.SetLightActive(_useCase.IsOn.CurrentValue || strobeVal > 0f);
                    _view.SetStrobeIntensity(strobeVal);
                })
                .AddTo(ref builder);

            // ストロボのチャージ進捗を監視し、UIゲージを更新
            if (_strobeGaugeView != null)
            {
                _useCase.StrobeCharge
                    .Subscribe(charge =>
                    {
                        _strobeGaugeView.SetGaugeActive(charge > 0f);
                        _strobeGaugeView.SetProgress(charge);
                    })
                    .AddTo(ref builder);
            }

            _disposable = builder.Build();
        }

        // 毎フレームの入力を監視
        public void Tick()
        {
            float deltaTime = UnityEngine.Time.deltaTime;

            if (_inputService.FlashlightTogglePressed)
            {
                _useCase.ToggleFlashlight();
            }

            // ストロボ入力処理
            bool isAttackHeld = _inputService.AttackHeld;
            if (isAttackHeld)
            {
                _useCase.Charge(deltaTime);
            }
            else if (_wasAttackHeld)
            {
                _useCase.Release();
            }
            _wasAttackHeld = isAttackHeld;

            // 毎フレームの更新処理（減衰など）
            _useCase.Update(deltaTime);
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
