using System;
using System.Threading.Tasks;
using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases.Interaction;
using Shinzui.View.Interaction;
using Shinzui.View;
using VContainer.Unity;
using UnityEngine;

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
        private bool _isInteracting;
        private bool _disposed;
        private bool _requireRelease;
        private readonly ItemPickupUseCase _pickup = new();
        private InteractableComponent _pickupTarget;
        private IPickupInterruption _interruption;
        private bool _interruptionStarted;
        private PlayerBodyView _body;
        private Vector3 _pickupPosition;
        private Quaternion _pickupRotation;


        /// <summary>入力と取得結果を各Viewへ接続</summary>
        /// <param name="useCase">取得処理</param>
        /// <param name="inputService">入力状態</param>
        /// <param name="interactionView">対象の検出と表示</param>
        /// <param name="messageView">取得メッセージ</param>
        /// <param name="playerView">プレイヤーの参照</param>
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

        /// <summary>メッセージ通知を購読</summary>
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

        /// <summary>長押しと対象の有効性を監視して取得姿勢を更新</summary>
        public void Tick()
        {
            if (_disposed || _interactionView == null) return;
            bool held = _inputService.ItemUseHeld;
            if (!held) _requireRelease = false;
            if (_body == null && _playerView != null) _body = _playerView.GetComponentInChildren<PlayerBodyView>();

            if (_pickup.IsActive)
            {
                bool valid = _interactionView.CanContinuePickup(_pickupTarget);
                _pickup.Tick(Time.deltaTime, held, valid);

                // 指定進捗の演出は一操作につき一度だけ開始
                if (_pickup.State == ItemPickupState.Reaching && !_interruptionStarted && _interruption != null
                    && _pickup.Progress >= _interruption.TriggerProgress)
                {
                    _interruptionStarted = true;
                    _interruption.Begin();
                }
                if (_interruptionStarted && (_pickup.State == ItemPickupState.Reaching || _pickup.State == ItemPickupState.Waiting))
                {
                    switch (_interruption.Result)
                    {
                        case PickupInterruptionResult.Wait: _pickup.Pause(); break;
                        case PickupInterruptionResult.Cancel: _pickup.Cancel(); break;
                        default: _pickup.Resume(); break;
                    }
                }

                // 演出が対象を消す場合も確定前に再検証
                if (_pickup.State == ItemPickupState.Reaching && !_interactionView.CanContinuePickup(_pickupTarget)) _pickup.Cancel();
                if (_pickup.TryCommit()) _ = HandleInteractAsync(_pickupTarget, true);
                if (_pickup.State == ItemPickupState.Returning || !_pickup.IsActive) EndInterruption(_pickup.Succeeded);
                UpdatePickupPose();
                return;
            }

            // 長押しは対象を開始時に固定し、通常の調査操作は従来通り実行
            if (!_isInteracting && !_requireRelease && _inputService.ItemUsePressed)
            {
                var target = _interactionView.CurrentInteractable;
                if (target == null || !target.CanInteract) return;
                if (target.RequiresHold)
                {
                    if (!held || !_interactionView.CanContinuePickup(target)) return;
                    _pickupTarget = target;
                    _interruption = target.GetComponent<IPickupInterruption>();
                    _interruptionStarted = false;
                    _requireRelease = true;
                    _pickup.Begin(target.PickupDuration);
                    UpdatePickupPose();
                }
                else _ = HandleInteractAsync(target, false);
            }
        }

        /// <summary>進捗を右腕・指・案内へ反映</summary>
        private void UpdatePickupPose()
        {
            // 取得後やキャンセル後は最後の到達位置から手を戻す
            if (_pickupTarget != null && (_pickup.State == ItemPickupState.Reaching || _pickup.State == ItemPickupState.Waiting))
            {
                _pickupPosition = _pickupTarget.GetPickupContact(_playerView.CameraPosition);
                Vector3 direction = _pickupPosition - _playerView.CameraPosition;
                if (direction.sqrMagnitude < .0001f) direction = _playerView.CameraForward;
                _pickupRotation = _pickupTarget.PickupGrip != null ? _pickupTarget.PickupGrip.rotation
                    : Quaternion.LookRotation(direction.normalized, _playerView.MainCamera.transform.up);
            }
            float grip = _pickup.Succeeded ? .8f : Mathf.InverseLerp(.8f, 1f, _pickup.Progress) * .8f;
            if (_body != null) _body.SetPickupPose(_pickupPosition, _pickupRotation, _pickup.Progress, grip);
            _interactionView.SetPickupProgress(_pickup.IsActive, _pickup.Progress,
                _pickup.State == ItemPickupState.Reaching || _pickup.State == ItemPickupState.Waiting
                || _pickup.State == ItemPickupState.Committing, _pickup.Succeeded, _pickupTarget);
            if (!_pickup.IsActive)
            {
                _pickupTarget = null;
                _interruption = null;
            }
        }

        /// <summary>演出の所有する音やカメラ等を解放</summary>
        /// <param name="acquired">取得に成功したか</param>
        private void EndInterruption(bool acquired)
        {
            if (!_interruptionStarted) return;
            _interruptionStarted = false;
            _interruption.End(acquired);
        }

        /// <summary>取得を一度だけ確定し、成功時のみ対象へ反映</summary>
        /// <param name="target">開始時に確定した対象</param>
        /// <param name="pickup">長押しによる取得か</param>
        /// <returns>取得処理の完了</returns>
        private async Task HandleInteractAsync(InteractableComponent target, bool pickup)
        {
            _isInteracting = true;
            bool success = false;
            try
            {
                // この時点以降は取得確定として扱い、ボタン解放では巻き戻さない
                success = await _useCase.InteractAsync(target.InteractableId, target.InteractMessage);
                if (pickup) EndInterruption(success);
                if (success && target != null)
                {
                    if (pickup) target.CanInteract = false;
                    target.ExecuteInteractEffect();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (pickup) _pickup.Finish(success);
                _isInteracting = false;
            }
        }

        /// <summary>購読と取得演出を解放し右腕を通常姿勢へ戻す</summary>
        public void Dispose()
        {
            _disposed = true;
            _pickup.Cancel();
            EndInterruption(false);
            if (_body != null) _body.SetPickupPose(Vector3.zero, Quaternion.identity, 0f, 0f);
            if (_interactionView != null) _interactionView.SetPickupProgress(false, 0f);
            _disposable?.Dispose();
        }
    }
}
