using Shinzui.Application.Interfaces;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Application.UseCases;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.Application.UseCases.Flashlight; // 【追加】
using Shinzui.Domain.Entities;
using Shinzui.Domain.Entities.Inventory;
using Shinzui.Domain.Entities.Flashlight; // 【追加】
using Shinzui.Domain.ValueObjects.Player;
using Shinzui.Infrastructure.Repositories;
using Shinzui.Infrastructure.Services;
using Shinzui.Presentation;
using Shinzui.Presentation.Inventory;
using Shinzui.Presentation.Flashlight; // 【追加】
using Shinzui.View;
using Shinzui.View.Inventory;
using Shinzui.View.Flashlight; // 【追加】
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Shinzui.DI
{
    public class PlayerLifetimeScope : LifetimeScope
    {
        [Header("Views")]
        [SerializeField] private PlayerView playerView;
        [SerializeField] private InventoryView inventoryView;
        [SerializeField] private FlashlightView flashlightView; // 【追加】懐中電灯View参照

        protected override void Configure(IContainerBuilder builder)
        {
            var player = CreatePlayer();

            // シングルトンインスタンスとして登録
            builder.RegisterInstance(player);

            // InputServiceの登録
            builder.Register<UnityInputService>(Lifetime.Singleton).As<IInputService>();

            // UseCaseの登録
            builder.Register<PlayerMoveUseCase>(Lifetime.Scoped);

            // Viewの登録
            var viewInstance = playerView;
            if (viewInstance == null)
            {
                viewInstance = FindFirstObjectByType<PlayerView>();
            }

            if (viewInstance != null)
            {
                builder.RegisterComponent(viewInstance);
            }
            else
            {
                Debug.LogWarning("PlayerView instance was not assigned and not found in the scene hierarchy.");
            }

            // PresenterをVContainerのEntryPointとして登録
            builder.RegisterEntryPoint<PlayerMovePresenter>();

            // ==========================================
            // インベントリシステムのDI登録
            // ==========================================
            
            // Domain & UseCase
            builder.Register<InventoryEntity>(Lifetime.Singleton).WithParameter(10); // 初期スロット数: 10
            builder.Register<InventoryUseCase>(Lifetime.Singleton);

            // Infrastructure (Catalog & Repository & Sound)
            builder.Register<IItemCatalog, AddressableItemCatalog>(Lifetime.Singleton);
            builder.Register<IInventoryRepository, PlayerPrefsInventoryRepository>(Lifetime.Singleton);
            builder.Register<IFMODSEService, FMODSEService>(Lifetime.Singleton);

            // View (シーン上のUIコンポーネント)
            var invViewInstance = inventoryView;
            if (invViewInstance == null)
            {
                invViewInstance = FindFirstObjectByType<InventoryView>();
            }

            if (invViewInstance != null)
            {
                builder.RegisterComponent(invViewInstance);
            }
            else
            {
                Debug.LogWarning("InventoryView instance was not assigned and not found in the scene hierarchy.");
            }

            // Presentation (EntryPointとして自動でInitialize/Tick/Disposeを実行)
            builder.RegisterEntryPoint<InventoryPresenter>();

            // ==========================================
            // 【追加】懐中電灯システムのDI登録
            // ==========================================
            
            // Domain & UseCase
            builder.Register<FlashlightEntity>(Lifetime.Singleton).WithParameter(false); // 初期状態: OFF
            builder.Register<FlashlightUseCase>(Lifetime.Singleton);

            // View (シーン上のLightオブジェクト)
            var lightViewInstance = flashlightView;
            if (lightViewInstance == null)
            {
                lightViewInstance = FindFirstObjectByType<FlashlightView>();
            }

            if (lightViewInstance != null)
            {
                builder.RegisterComponent(lightViewInstance);
            }
            else
            {
                Debug.LogWarning("FlashlightView instance was not assigned and not found in the scene hierarchy.");
            }

            // Presentation (EntryPoint)
            builder.RegisterEntryPoint<FlashlightPresenter>();
        }

        private static PlayerEntity CreatePlayer()
        {
            return new PlayerEntity(
                new PlayerSpeedStatus(),
                new PlayerCrouchStatus(),
                new PlayerStamina());
        }
    }
}