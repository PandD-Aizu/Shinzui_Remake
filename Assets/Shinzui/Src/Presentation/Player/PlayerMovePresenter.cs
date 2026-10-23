using System;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using Shinzui.Application.UseCases.Enemy;
using R3;
using Shinzui.View;
using UnityEngine;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    public class PlayerMovePresenter : IInitializable, ITickable, IDisposable, IPlayerTracker
    {
        private readonly PlayerMoveUseCase _useCase;
        private readonly PlayerView _view;
        private readonly EnemyDirectorUseCase _enemyDirector;

        private IDisposable _disposable;
        private bool _wasControllerGrounded;
        private float _peakFallVelocity;

        public Vector3 PlayerPosition => _view != null ? _view.transform.position : Vector3.zero;

        public (Vector3 start, Vector3 end)? CurrentTunnelBounds
        {
            get
            {
                if (_useCase != null && _useCase.currentTunnelStart != null && _useCase.currentTunnelEnd != null)
                {
                    return (_useCase.currentTunnelStart.transform.position, _useCase.currentTunnelEnd.transform.position);
                }
                return null;
            }
        }

        /// <summary>移動とカメラの揺れに必要なプレイヤー・敵の状態を受け取る</summary>
        /// <param name="useCase">移動とスタミナの状態</param>
        /// <param name="view">プレイヤーの表示とカメラ</param>
        /// <param name="enemyDirector">敵全体の追跡状態</param>
        public PlayerMovePresenter(
            PlayerMoveUseCase useCase,
            PlayerView view,
            EnemyDirectorUseCase enemyDirector)
        {
            _useCase = useCase;
            _view = view;
            _enemyDirector = enemyDirector;
        }

        public void Initialize()
        {
            if (_view != null)
            {
                _useCase.SetCrouchRatio(_view.CrouchRatio, _view.HeightChangeRate);
            }

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
            _wasControllerGrounded = _view.IsControllerGrounded;
            _peakFallVelocity = 0.0f;
        }

        public void Tick()
        {
            // 転倒中は通常移動とカメラ揺れによる姿勢の上書きを止める
            if (_view != null && _view.IsCinematicControlled)
            {
                _useCase.RestDuringCinematic(Time.deltaTime);
                return;
            }

            if (_view != null)
            {
                _useCase.SetCrouchRatio(_view.CrouchRatio, _view.HeightChangeRate);
            }

            // 強制しゃがみフラグがあれば適用
            bool forceCrouch = !_useCase.CrouchPressed && !_view.CanStand();

            // カメラ方向と設置状態をViewから取得
            Vector3 right = _view.CameraRight;
            Vector3 forward = _view.CameraForward;
            bool isGrounded = _view.IsGrounded;
            bool isControllerGrounded = _view.IsControllerGrounded;
            Vector3 groundNormal = _view.GroundNormal;
            float deltaTime = Time.deltaTime;
            float landingSpeed = isControllerGrounded && !_wasControllerGrounded
                ? Mathf.Max(0.0f, -_peakFallVelocity)
                : 0.0f;

            // ユースケースを通して状態を更新
            _useCase.Move(
                forceCrouch,
                isGrounded,
                right,
                forward,
                groundNormal,
                deltaTime
            );

            // 計算された速度をUseCaseから取得し、Viewに適用して物理移動を実行
            Vector3 currentVelocity = _useCase.Velocity.CurrentValue;
            _view.Move(currentVelocity);
            _view.UpdateCameraMotion(currentVelocity, isGrounded, _useCase.IsRunning, landingSpeed,
                _useCase.StaminaRatio.CurrentValue, _enemyDirector.IsPlayerFound.CurrentValue);

            // Y回転をカメラに合わせる
            _view.AlignYRotationWithCamera(currentVelocity);

            if (isControllerGrounded)
            {
                _peakFallVelocity = 0.0f;
            }
            else
            {
                _peakFallVelocity = Mathf.Min(_peakFallVelocity, currentVelocity.y);
            }
            _wasControllerGrounded = isControllerGrounded;
            
            // 現在のトンネルの判定
            _useCase.CheckCurrentTunnel(_view.transform.position);
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
