using System;
using Shinzui.Application.UseCases;
using R3;
using Shinzui.View;
using UnityEngine;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    public class PlayerMovePresenter : IInitializable, ITickable, IDisposable
    {
        private readonly PlayerMoveUseCase _useCase;
        private readonly PlayerView _view;

        private IDisposable _disposable;

        public PlayerMovePresenter(
            PlayerMoveUseCase useCase,
            PlayerView view)
        {
            _useCase = useCase;
            _view = view;
        }

        public void Initialize()
        {
            var disposableBuilder = Disposable.CreateBuilder();

            // UseCaseを介してコライダーの高さ更新を監視し、Viewに流す
            _useCase.CurrentHeight
                .Subscribe(height => _view.SetHeight(height))
                .AddTo(ref disposableBuilder);

            // UseCaseを介してスタミナの割合更新を監視し、Viewに流す
            _useCase.StaminaRatio
                .Subscribe(ratio => _view.ChangeStaminaSlider(ratio))
                .AddTo(ref disposableBuilder);

            _disposable = disposableBuilder.Build();
        }

        public void Tick()
        {
            // 強制しゃがみフラグがあれば適用
            bool forceCrouch = false; 

            // カメラ方向と設置状態をViewから取得
            Vector3 right = _view.CameraRight;
            Vector3 forward = _view.CameraForward;
            bool isGrounded = _view.IsGrounded;
            float deltaTime = Time.deltaTime;

            // ユースケースを通して状態を更新
            _useCase.Move(
                forceCrouch,
                isGrounded,
                right,
                forward,
                deltaTime
            );

            // 計算された速度をUseCaseから取得し、Viewに適用して物理移動を実行
            Vector3 currentVelocity = _useCase.Velocity.CurrentValue;
            _view.Move(currentVelocity);

            // Y回転をカメラに合わせる
            _view.AlignYRotationWithCamera();
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
