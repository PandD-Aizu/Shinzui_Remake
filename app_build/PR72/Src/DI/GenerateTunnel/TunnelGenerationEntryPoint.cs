using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Presentation.GenerateTunnel;
using Shinzui.View.GenerateTunnel;
using VContainer.Unity;

namespace Shinzui.DI.GenerateTunnel
{
    /// <summary>Starts after scene Awake/injection, before gameplay Update.</summary>
    public sealed class TunnelGenerationEntryPoint : IStartable
    {
        private readonly GenerateTunnelPresenter _presenter;
        private readonly TunnelGenerator _generator;
        private readonly TunnelItemSpawnBinding _items;
        private readonly TunnelMapView _mapView;
        private bool _started;

        public TunnelGenerationEntryPoint(GenerateTunnelPresenter presenter,
            TunnelGenerator generator, TunnelItemSpawnBinding items, TunnelMapView mapView)
        {
            _presenter = presenter;
            _generator = generator;
            _items = items;
            _mapView = mapView;
        }

        public void Start()
        {
            if (_started) return;
            _started = true;
#if STEAMAUDIO_ENABLED
            // Steam Audio creates its native scene in sceneLoaded, after LifetimeScope.Awake.
            Shinzui.DI.TunnelAcoustics.TunnelAudioBinding.Attach(_mapView);
#endif
            var request = TunnelGenerationRequestFactory.Create(_generator, GenerateTunnelLifetimeScope.RuntimeSeedOverride);
            if (_presenter.ExecuteGeneration(request))
                _items.OnMapReady();
        }

        public bool Regenerate(TunnelGenerationRequestDto request)
        {
            if (!_started || !_presenter.ExecuteGeneration(request, regenerate: true)) return false;
            _items.OnMapReady();
            return true;
        }
    }
}
