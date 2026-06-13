using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using Shinzui.Domain.Entities;
using Shinzui.Domain.ValueObjects.Player;
using Shinzui.Infrastructure.Services;
using Shinzui.Presentation;
using Shinzui.View;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Shinzui.DI
{
    public class PlayerLifetimeScope : LifetimeScope
    {
        [SerializeField] private PlayerView playerView;

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