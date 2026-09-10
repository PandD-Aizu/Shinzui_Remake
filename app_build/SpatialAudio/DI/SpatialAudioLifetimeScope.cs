using Shinzui.Application.SpatialAudio;
using Shinzui.Infrastructure.SpatialAudio;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Shinzui.DI.SpatialAudio
{
    public sealed class SpatialAudioLifetimeScope : LifetimeScope
    {
        public SpatialAudioCatalog Catalog;
        [Range(1, 128)] public int MaxVoices = 8;
        [Min(0)] public float PreparationSeconds = .25f;
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(new SpatialAudioRuntimeOptions { Catalog = Catalog, Owner = transform,
                MaxVoices = MaxVoices, PreparationSeconds = PreparationSeconds });
            builder.Register<FmodSpatialAudioService>(Lifetime.Scoped).AsSelf().As<ISpatialAudioService>();
            builder.Register<SpatialAudioUseCase>(Lifetime.Scoped);
            builder.RegisterEntryPoint<SpatialAudioTick>();
        }
    }

    sealed class SpatialAudioTick : ITickable
    {
        readonly FmodSpatialAudioService service;
        public SpatialAudioTick(FmodSpatialAudioService service) { this.service = service; }
        public void Tick() { service.Tick(); }
    }
}
