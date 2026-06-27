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

namespace Shinzui.Temp
{
    /// <summary>
    /// インベントリでの特定のアイテム消費を検知し、プレイヤーのスタミナを一時的に無限にするコンポーネント
    /// </summary>
    public class TempInfiniteStaminaState : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float duration = 15.0f; // 効果持続時間（秒）
        [SerializeField] private string targetItemId = "potion_stamina"; // 検知対象のアイテムID

        private PlayerEntity _playerEntity;
        private InventoryUseCase _inventoryUseCase;
        
        private float _remainingTime = 0.0f;
        private bool _isActive = false;
        private float _originalDecreaseRate = 15.0f;
        private int _prevItemCount = 0;
        private System.IDisposable _subscription;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            var playerScope = FindFirstObjectByType<PlayerLifetimeScope>();
            if (playerScope != null && playerScope.Container != null)
            {
                _playerEntity = playerScope.Container.Resolve<PlayerEntity>();
                _inventoryUseCase = playerScope.Container.Resolve<InventoryUseCase>();

                if (_playerEntity != null && _inventoryUseCase != null)
                {
                    _originalDecreaseRate = _playerEntity.PlayerStamina.StaminaDecreaseRate;
                    _prevItemCount = GetTotalItemCount();

                    // インベントリの全スロットの変化を購読
                    var builder = Disposable.CreateBuilder();
                    for (int i = 0; i < _inventoryUseCase.Capacity; i++)
                    {
                        _inventoryUseCase.GetSlotDto(i)
                            .Subscribe(_ => OnInventoryChanged())
                            .AddTo(ref builder);
                    }
                    _subscription = builder.Build();
                }
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
        /// インベントリ変更時に呼び出され、個数の減少（使用）を検知する
        /// </summary>
        private void OnInventoryChanged()
        {
            int currentCount = GetTotalItemCount();
            if (currentCount < _prevItemCount)
            {
                Activate();
            }
            _prevItemCount = currentCount;
        }

        /// <summary>
        /// スタミナ無限状態を有効化
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
            if (!_isActive || _playerEntity == null) return;

            // 毎フレームスタミナを最大値に保つ
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

        /// <summary>
        /// スタミナ無限状態を終了し、パラメータを元に戻す
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
        }
    }
}
