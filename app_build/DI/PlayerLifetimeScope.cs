using Shinzui.Application.Interfaces;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Application.UseCases;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.Domain.Entities;
using Shinzui.Domain.Entities.Inventory;
using Shinzui.Domain.ValueObjects.Player;
using Shinzui.Infrastructure.Repositories;
using Shinzui.Infrastructure.Services;
using Shinzui.Presentation;
using Shinzui.Presentation.Inventory;
using Shinzui.View;
using Shinzui.View.Inventory;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Shinzui.DI
{
    public class PlayerLifetimeScope : LifetimeScope
    {
        [SerializeField] private PlayerView playerView;
        [SerializeField] private InventoryView inventoryView; // 【追加】インベントリView参照

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
            // 【追加】インベントリシステムのDI登録
            // ==========================================
            
            // Domain & UseCase
            builder.Register<InventoryEntity>(Lifetime.Singleton).WithParameter(10); // 初期スロット数: 10
            builder.Register<InventoryUseCase>(Lifetime.Singleton);

            // Infrastructure (Catalog & Repository)
            builder.Register<IItemCatalog, AddressableItemCatalog>(Lifetime.Singleton);
            builder.Register<IInventoryRepository, PlayerPrefsInventoryRepository>(Lifetime.Singleton);

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
