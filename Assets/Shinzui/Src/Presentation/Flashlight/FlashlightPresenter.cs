using System;
using System.Collections.Generic;
using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases.Flashlight;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.Presentation.Inventory;
using Shinzui.View.Flashlight;
using Shinzui.View.Interaction;
using UnityEngine;
using VContainer.Unity;

namespace Shinzui.Presentation.Flashlight
{
    public class FlashlightPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly FlashlightUseCase _useCase;
        private readonly FlashlightView _view;
        private readonly IInputService _inputService;
        private readonly StrobeGaugeView _strobeGaugeView;
        private readonly EnemyPresenter _enemyPresenter;
        private readonly InventoryUseCase _inventoryUseCase;
        private readonly InteractionMessageView _messageView;

        private IDisposable _disposable;

        public FlashlightPresenter(
            FlashlightUseCase useCase,
            FlashlightView view,
            IInputService inputService,
            StrobeGaugeView strobeGaugeView,
            EnemyPresenter enemyPresenter,
            InventoryUseCase inventoryUseCase,
            InteractionMessageView messageView)
        {
            _useCase = useCase;
            _view = view;
            _inputService = inputService;
            _strobeGaugeView = strobeGaugeView;
            _enemyPresenter = enemyPresenter;
            _inventoryUseCase =  inventoryUseCase;
            _messageView = messageView;
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
            
            if (_inventoryUseCase == null)
            {
                Debug.LogError("_inventoryUseCase is NULL");
                return;
            }

            if (_inputService.FlashlightTogglePressed)
            {
                _useCase.ToggleFlashlight();
            }

            // ストロボ入力処理
            var idx = _inventoryUseCase.CheckItem("FlashlightBattery");
            bool isAttackHeld = _inputService.AttackHeld;
            if (isAttackHeld)
            {
                if (idx >= 0)
                {
                    _useCase.Charge(deltaTime);
                }
                else
                {
                    _messageView.ShowMessage("乾電池切れのようだ");
                }
            }
            else if (_wasAttackHeld)
            {
                StrobeReleaseResult result = _useCase.Release();
                if (result.Fired)
                {
                    ApplyStrobeToEnemies(result.Charge);
                    _inventoryUseCase.UseItemAsync(idx);
                }
            }
            _wasAttackHeld = isAttackHeld;

            // 毎フレームの更新処理（減衰など）
            _useCase.Update(deltaTime);
        }

        /// <summary>
        /// ストロボのチャージ量に応じて、範囲内の敵に効果を適用する
        /// </summary>
        /// <param name="charge">ストロボのチャージ量</param>
        private void ApplyStrobeToEnemies(float charge)
        {
            if (_view == null) return;

            Vector3 origin = _view.StrobeOrigin;
            Vector3 direction = _view.StrobeDirection;
            if (direction.sqrMagnitude <= 0f) return;

            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                _view.StrobeRadius,
                direction.normalized,
                _view.StrobeRange,
                _view.StrobeHitMask,
                QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0) return;

            var affectedEnemies = new HashSet<int>();
            foreach (RaycastHit hit in hits)
            {
                StrobeTarget enemy = _enemyPresenter.FindStrobeTarget(hit, 6.0f);
                if (!enemy.IsValid || !affectedEnemies.Add(enemy.Id))
                {
                    continue;
                }

                ApplyStrobeToEnemy(enemy, origin, charge);
            }
        }

        /// <summary>
        /// 指定された敵に対して、ストロボの効果を適用する
        /// </summary>
        /// <param name="enemy">対象の敵</param>
        /// <param name="origin">ストロボの発射元</param>
        /// <param name="charge">ストロボのチャージ量</param>
        private void ApplyStrobeToEnemy(StrobeTarget enemy, Vector3 origin, float charge)
        {
            Vector3 targetPosition = enemy.StrobeTargetPosition;
            float distance = Vector3.Distance(origin, targetPosition);
            float centerFactor = CalculateCenterFactor(targetPosition);
            if (centerFactor <= 0f)
            {
                return;
            }

            bool shouldStop =
                charge >= _view.FullChargeStopThreshold &&
                distance <= _view.StopDistance &&
                centerFactor >= 0.85f;

            if (shouldStop)
            {
                enemy.ApplyStrobeEffect(true, 0f, _view.StopDuration);
                return;
            }

            float chargeFactor = Mathf.Clamp01(charge);
            float distanceFactor = Mathf.Clamp01(1.0f - (distance / _view.StrobeRange));
            float duration = _view.MaxSlowDuration * chargeFactor * distanceFactor * centerFactor;
            if (duration <= 0.05f)
            {
                return;
            }

            enemy.ApplyStrobeEffect(false, _view.SlowSpeedMultiplier, duration);
        }

        /// <summary>
        /// 指定されたワールド座標が画面中央にどれだけ近いかを計算する
        /// </summary>
        /// <param name="worldPosition">ワールド座標</param>
        /// <returns>中央からの距離の因子</returns>
        private float CalculateCenterFactor(Vector3 worldPosition)
        {
            Camera targetCamera = _view.TargetCamera;
            if (targetCamera == null)
            {
                return 1.0f;
            }

            Vector3 viewportPosition = targetCamera.WorldToViewportPoint(worldPosition);
            if (viewportPosition.z <= 0f)
            {
                return 0f;
            }

            float centerDistance = Vector2.Distance(
                new Vector2(viewportPosition.x, viewportPosition.y),
                new Vector2(0.5f, 0.5f));
            float centerRadius = Mathf.Max(0.001f, _view.CenterViewportRadius);

            if (centerDistance > centerRadius)
            {
                return 0.35f;
            }

            return Mathf.Lerp(0.35f, 1.0f, 1.0f - Mathf.Clamp01(centerDistance / centerRadius));
        }

        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
}
