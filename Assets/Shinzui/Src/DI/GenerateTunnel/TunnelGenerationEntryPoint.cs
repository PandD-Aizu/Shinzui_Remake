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
        private readonly TunnelGenerationRequestDto _request;
        private readonly TunnelItemSpawnBinding _items;
        private readonly TunnelMapView _mapView;
        private bool _started;

        public TunnelGenerationEntryPoint(GenerateTunnelPresenter presenter,
            TunnelGenerationRequestDto request, TunnelItemSpawnBinding items, TunnelMapView mapView)
        {
            _presenter = presenter;
            _request = request;
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
            if (_presenter.ExecuteGeneration(_request))
                _items.OnMapReady();
        }
    }
}
