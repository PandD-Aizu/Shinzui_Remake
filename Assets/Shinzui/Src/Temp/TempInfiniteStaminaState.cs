using UnityEngine;
using System.Collections.Generic;
using Shinzui.Domain.Entities;
using Shinzui.Domain.ValueObjects.Inventory;
using Shinzui.DI;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Infrastructure.Repositories;
using VContainer;
using R3;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Shinzui.View;

namespace Shinzui.Temp
{
    /// <summary>
    /// インベントリでの特定アイテム消費を検知し、プレイヤーのスタミナを一時的に無限にし、画面・UI演出を行うコンポーネント
    /// </summary>
    public class TempInfiniteStaminaState : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float duration = 15.0f; // 効果持続時間（秒）
        [SerializeField] private string targetItemId = "potion_stamina"; // 検知対象のアイテムID

        [Header("Fade Duration Settings")]
        [SerializeField] private float fadeDuration = 2.0f; // 画面効果が最大になる／元に戻るまでにかける時間（秒）

        [Header("Required References")]
        [SerializeField] private Volume staminaBoostVolume; // 効果中にWeightを0 -> 1にするVolume
        [SerializeField] private Camera targetCamera; // 視野角を変更するカメラ
        [SerializeField] private Image staminaFillImage; // 色を変更するUIスタミナスライダーのFill画像

        [Header("Camera FOV Settings")]
        [SerializeField] private float fovIncreaseAmount = 8.0f; // どれだけ視野角を広げるか

        [Header("UI Slider Settings")]
        [SerializeField] private Color boostColor = new Color(0.8f, 0.1f, 0.1f, 1f); // ブースト中の色

        private PlayerEntity _playerEntity;
        private InventoryUseCase _inventoryUseCase;
        
        private float _remainingTime = 0.0f;
        private bool _isActive = false;
        private float _originalDecreaseRate = 15.0f;
        private int _prevItemCount = 0;
        private System.IDisposable _subscription;

        // 演出用の内部状態
        private float _currentVolumeWeight = 0.0f;
        private float _initialFov = 60.0f;
        private float _currentFovOffset = 0.0f;
        private Color _originalFillColor = Color.white;
        private bool _hasSavedInitialFov = false;
        private bool _hasSavedOriginalColor = false;
        private bool _isInitialized = false;

        private void Start()
        {
            // VContainerビルド完了を待つため、Start時に初期化を試みる
            TryInitialize();
        }

        private void TryInitialize()
        {
            if (_isInitialized) return;

            // プレイヤー自身（親子含む）から PlayerLifetimeScope を取得する
            var playerScope = GetComponent<PlayerLifetimeScope>();
            if (playerScope == null) playerScope = GetComponentInParent<PlayerLifetimeScope>();
            if (playerScope == null) playerScope = GetComponentInChildren<PlayerLifetimeScope>();

            // フォールバック：親子関係にない場合はシーン全体から検索
            if (playerScope == null)
            {
                playerScope = FindFirstObjectByType<PlayerLifetimeScope>();
            }

            if (playerScope == null) return;
            if (playerScope.Container == null) return;

            try
            {
                _playerEntity = playerScope.Container.Resolve<PlayerEntity>();
                _inventoryUseCase = playerScope.Container.Resolve<InventoryUseCase>();
            }
            catch (System.Exception)
            {
                return;
            }

            if (_playerEntity != null && _inventoryUseCase != null)
            {
                _originalDecreaseRate = _playerEntity.PlayerStamina.StaminaDecreaseRate;
                _prevItemCount = GetTotalItemCount();

                // インベントリの全スロットの変化を監視
                var builder = Disposable.CreateBuilder();
                for (int i = 0; i < _inventoryUseCase.Capacity; i++)
                {
                    _inventoryUseCase.GetSlotDto(i)
                        .Subscribe(_ => OnInventoryChanged())
                        .AddTo(ref builder);
                }
                _subscription = builder.Build();

                // アサインが空の場合はPlayerViewから自動取得を試みる
                if (targetCamera == null)
                {
                    var playerView = GetComponent<PlayerView>();
                    if (playerView == null) playerView = GetComponentInParent<PlayerView>();
                    if (playerView == null) playerView = GetComponentInChildren<PlayerView>();
                    if (playerView != null)
                    {
                        targetCamera = playerView.MainCamera;
                    }
                }

                if (targetCamera != null)
                {
                    _initialFov = targetCamera.fieldOfView;
                    _hasSavedInitialFov = true;
                }

                if (staminaFillImage == null)
                {
                    var playerView = GetComponent<PlayerView>();
                    if (playerView == null) playerView = GetComponentInParent<PlayerView>();
                    if (playerView == null) playerView = GetComponentInChildren<PlayerView>();
                    if (playerView != null)
                    {
                        staminaFillImage = playerView.StaminaFillImage;
                    }
                }

                if (staminaFillImage != null)
                {
                    _originalFillColor = staminaFillImage.color;
                    _hasSavedOriginalColor = true;
                }

                if (staminaBoostVolume == null)
                {
                    var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
                    foreach (var vol in volumes)
                    {
                        if (vol.gameObject.name.Contains("Stamina", System.StringComparison.OrdinalIgnoreCase) ||
                            vol.gameObject.name.Contains("Boost", System.StringComparison.OrdinalIgnoreCase) ||
                            vol.gameObject.name.Contains("Effect", System.StringComparison.OrdinalIgnoreCase))
                        {
                            staminaBoostVolume = vol;
                            break;
                        }
                    }
                    if (staminaBoostVolume == null && volumes.Length > 0)
                    {
                        staminaBoostVolume = volumes[0];
                    }
                }

                if (staminaBoostVolume != null)
                {
                    staminaBoostVolume.weight = 0.0f;
                }

                _isInitialized = true;
            }
        }

        /// <summary>
        /// インベントリ内の対象アイテムの合計所持数を取得
        /// </summary>
        private int GetTotalItemCount()
        {
            if (_inventoryUseCase == null) return 0;
            int total = 0;
            for (int i = 0; i < _inventoryUseCase.Capacity; i++)
            {
                var dto = _inventoryUseCase.GetSlotDto(i).CurrentValue;
                if (dto != null && dto.HasItem && dto.ItemId == targetItemId)
                {
                    total += dto.Quantity;
                }
            }
            return total;
        }

        /// <summary>
        /// インベントリ変化を監視し、個数減少時に効果を発動する
        /// </summary>
        private void OnInventoryChanged()
        {
            int currentCount = GetTotalItemCount();
            
            // 所持数が減ったタイミングを使用とみなして発動
            if (currentCount < _prevItemCount)
            {
                Activate();
            }
            _prevItemCount = currentCount;
        }

        /// <summary>
        /// スタミナ無限状態を活性化する
        /// </summary>
        private void Activate()
        {
            _remainingTime = duration;
            _isActive = true;

            var stamina = _playerEntity.PlayerStamina;
            _playerEntity.PlayerStamina = stamina with 
            { 
                StaminaDecreaseRate = 0.0f,
                CurrentStamina = stamina.MaxStamina
            };
            _playerEntity.CurrentStamina.Value = stamina.MaxStamina;
            _playerEntity.IsExhausted.Value = false;
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                // VContainerの登録完了まで毎フレーム初期化を試みる
                TryInitialize();
                return;
            }

            if (_playerEntity != null && _isActive)
            {
                // 効果中はスタミナ低下処理と競合しないよう、毎フレーム最大値を維持する
                var stamina = _playerEntity.PlayerStamina;
                _playerEntity.PlayerStamina = stamina with { CurrentStamina = stamina.MaxStamina };
                _playerEntity.CurrentStamina.Value = stamina.MaxStamina;
                _playerEntity.IsExhausted.Value = false;

                _remainingTime -= Time.deltaTime;
                if (_remainingTime <= 0.0f)
                {
                    Deactivate();
                }
            }

            UpdateEffects();
        }

        /// <summary>
        /// 各種演出パラメータを時間に基づいて滑らかに補間する
        /// </summary>
        private void UpdateEffects()
        {
            float dt = Time.deltaTime;
            
            // fadeDurationに基づき、指定秒数で遷移するように速度を計算する
            float fadeSpeed = fadeDuration > 0.0f ? (1.0f / fadeDuration) : 999.0f;

            // Volume Weight のフェード
            if (staminaBoostVolume != null)
            {
                float targetWeight = _isActive ? 1.0f : 0.0f;
                _currentVolumeWeight = Mathf.MoveTowards(_currentVolumeWeight, targetWeight, fadeSpeed * dt);
                staminaBoostVolume.weight = _currentVolumeWeight;
            }

            // カメラ FOV のフェード
            if (targetCamera != null && _hasSavedInitialFov)
            {
                float targetOffset = _isActive ? fovIncreaseAmount : 0.0f;
                _currentFovOffset = Mathf.MoveTowards(_currentFovOffset, targetOffset, (fovIncreaseAmount * fadeSpeed) * dt);
                targetCamera.fieldOfView = _initialFov + _currentFovOffset;
            }

            // UI スタミナゲージの色のフェード
            if (staminaFillImage != null && _hasSavedOriginalColor)
            {
                Color targetColor = _isActive ? boostColor : _originalFillColor;
                staminaFillImage.color = Color.Lerp(staminaFillImage.color, targetColor, 3.0f * fadeSpeed * dt);
            }
        }

        /// <summary>
        /// スタミナ無限化効果を終了し、プレイヤーパラメータを復元する
        /// </summary>
        private void Deactivate()
        {
            _isActive = false;
            RestoreStamina();
        }

        private void RestoreStamina()
        {
            if (_playerEntity != null)
            {
                _playerEntity.PlayerStamina = _playerEntity.PlayerStamina with 
                { 
                    StaminaDecreaseRate = _originalDecreaseRate 
                };
            }
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            if (_isActive)
            {
                RestoreStamina();
            }

            // 演出パラメータの強制リセット
            if (targetCamera != null && _hasSavedInitialFov)
            {
                targetCamera.fieldOfView = _initialFov;
            }
            if (staminaFillImage != null && _hasSavedOriginalColor)
            {
                staminaFillImage.color = _originalFillColor;
            }
        }
    }
}
