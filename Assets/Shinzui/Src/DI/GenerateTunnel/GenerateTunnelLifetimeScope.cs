using System;
using Shinzui.Application.Interfaces.Tunnel;
using Shinzui.Application.UseCases.Tunnel;
using Shinzui.Domain.DomainServices.Tunnel;
using Shinzui.Infrastructure.Tunnel;
using Shinzui.Presentation.GenerateTunnel;
using Shinzui.Presentation.ItemSpawn;
using Shinzui.View.GenerateTunnel;
using Shinzui.View.ItemSpawn;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Shinzui.DI.GenerateTunnel
{
    /// <summary>Owns one stage's generation, navigation and map-ready notification.</summary>
    [DisallowMultipleComponent]
    public sealed class GenerateTunnelLifetimeScope : LifetimeScope
    {
        [SerializeField] private TunnelGenerator generator;
        [SerializeField] private TunnelMapView mapView;

        // Compatibility with the temporary floor progression, without state in the View layer.
        public static int? RuntimeSeedOverride { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeSeed() => RuntimeSeedOverride = null;

        public bool Regenerate(int seed)
        {
            if (Container == null) return false;
            var request = TunnelGenerationRequestFactory.Create(generator, seed);
            return Container.Resolve<TunnelGenerationEntryPoint>().Regenerate(request);
        }

        protected override void Configure(IContainerBuilder builder)
        {
            Scene scene = gameObject.scene;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var scope in root.GetComponentsInChildren<GenerateTunnelLifetimeScope>(true))
            {
                if (scope != this && scope.isActiveAndEnabled)
                    throw new InvalidOperationException($"Scene '{scene.name}' must contain only one active GenerateTunnelLifetimeScope.");
            }

            generator = ResolveInScene(generator, scene);
            if (generator == null)
                throw new InvalidOperationException($"Scene '{scene.name}' requires a TunnelGenerator settings component.");
            mapView = ResolveInScene(mapView != null ? mapView : generator != null ? generator.MapView : null, scene);
            if (mapView == null)
                mapView = gameObject.AddComponent<TunnelMapView>();

            mapView.Configure(generator);
            GameObject mapRoot = mapView.EnsureMapRoot();
            var navigation = mapRoot.GetComponent<TunnelNavigationBuilder>();
            if (navigation == null)
                navigation = mapRoot.AddComponent<TunnelNavigationBuilder>();

            builder.Register<TunnelLayoutGenerator>(Lifetime.Scoped).As<ITunnelLayoutGenerator>();
            builder.Register<GenerateTunnelUseCase>(Lifetime.Scoped).As<IGenerateTunnelUseCase>();
            builder.RegisterComponent(mapView);
            builder.RegisterComponent(navigation).As<ITunnelNavigationBuilder>();
            builder.RegisterComponent(generator);
            // World-audio callers depend on the Application API; the composition root owns the backend.
            builder.Register<Shinzui.Application.SpatialAudio.ISpatialAudioService>(
                _ => Shinzui.DI.CustomSpatialAudio.CustomTunnelAudioBinding.Attach(mapView).Runtime.Voices,
                Lifetime.Scoped);
            builder.Register<Shinzui.Application.SpatialAudio.SpatialAudioUseCase>(Lifetime.Scoped);
            builder.Register<GenerateTunnelPresenter>(Lifetime.Scoped);
            builder.RegisterInstance(new TunnelItemSpawnBinding(
                ResolveInScene<ItemSpawnContainerView>(null, scene),
                ResolveInScene<ItemSpawnManager>(null, scene), mapRoot));
            builder.RegisterEntryPoint<TunnelGenerationEntryPoint>(Lifetime.Scoped).AsSelf();
        }

        private static T ResolveInScene<T>(T reference, Scene scene) where T : Component
        {
            if (reference != null)
            {
                if (reference.gameObject.scene != scene)
                    throw new InvalidOperationException($"{typeof(T).Name} must belong to scene '{scene.name}'.");
                return reference;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>();
                if (component != null) return component;
            }
            return null;
        }
    }
}
