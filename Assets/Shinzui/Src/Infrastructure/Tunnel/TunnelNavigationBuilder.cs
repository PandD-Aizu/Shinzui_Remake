using System;
using Shinzui.Application.Interfaces.Tunnel;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Shinzui.Infrastructure.Tunnel
{
    /// <summary>Owns runtime NavMesh data on MapRoot; never destroys authored assets.</summary>
    [DisallowMultipleComponent]
    public sealed class TunnelNavigationBuilder : MonoBehaviour, ITunnelNavigationBuilder
    {
        private NavMeshData _ownedData;
        private NavMeshSurface _surface;

        public bool BuildNavMesh()
        {
            if (_surface == null && !TryGetComponent(out _surface))
                _surface = gameObject.AddComponent<NavMeshSurface>();

            _surface.collectObjects = CollectObjects.Children;
            _surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            try
            {
                Physics.SyncTransforms();
                var previousData = _surface.navMeshData;
                _surface.BuildNavMesh();
                if (_surface.navMeshData != previousData)
                {
                    if (_ownedData != null) Destroy(_ownedData);
                    _ownedData = _surface.navMeshData;
                }
                if (_surface.navMeshData == null)
                    throw new InvalidOperationException("The generated stage has no navigation data.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        private void OnDestroy()
        {
            if (_ownedData == null) return;
            if (_surface != null && _surface.navMeshData == _ownedData) _surface.RemoveData();
            Destroy(_ownedData);
        }
    }
}
