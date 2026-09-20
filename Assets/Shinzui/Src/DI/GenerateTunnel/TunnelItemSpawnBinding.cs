using System.Collections.Generic;
using Shinzui.Presentation.ItemSpawn;
using Shinzui.View.ItemSpawn;
using UnityEngine;

namespace Shinzui.DI.GenerateTunnel
{
    /// <summary>Connects the generated stage to the same scene's optional item system.</summary>
    public sealed class TunnelItemSpawnBinding
    {
        private readonly ItemSpawnContainerView _container;
        private readonly ItemSpawnManager _manager;
        private readonly GameObject _mapRoot;

        public TunnelItemSpawnBinding(ItemSpawnContainerView container, ItemSpawnManager manager, GameObject mapRoot)
        {
            _container = container;
            _manager = manager;
            _mapRoot = mapRoot;
        }

        public void OnMapReady()
        {
            if (_manager == null) return;
            if (_container == null)
            {
                Debug.LogError("Tunnel item spawning requires an ItemSpawnContainerView in the stage scene.", _manager);
                return;
            }

            var surfaces = new List<WeightedSpawnSurface>();
            foreach (var surface in _mapRoot.GetComponentsInChildren<WeightedSpawnSurface>())
            {
                if (surface.isActiveAndEnabled && surface.IsDataValid) surfaces.Add(surface);
            }
            _container.SetSurfaces(surfaces);
            if (surfaces.Count > 0) _manager.RequestGenerateAfterMapReady();
        }
    }
}
