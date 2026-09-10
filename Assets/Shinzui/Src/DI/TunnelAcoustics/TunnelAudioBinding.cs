using System.Collections;
using Shinzui.Infrastructure.TunnelAcoustics;
using Shinzui.View;
using Shinzui.View.GenerateTunnel;
using UnityEngine;

namespace Shinzui.DI.TunnelAcoustics
{
    /// <summary>Composition root binds basic View lifecycle events to the engine audio implementation.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class TunnelAudioBinding : MonoBehaviour
    {
        TunnelMapView view;
        PlayerView player;
        Transform footAnchor;
        Coroutine pending;
        public TunnelAudioRuntime Runtime { get; private set; }
        public int BuildCount { get; private set; }
        public static TunnelAudioBinding Attach(TunnelMapView view, TunnelAudioConfiguration configuration = null)
        {
            var existing=view.GetComponent<TunnelAudioBinding>();
            if (existing) return existing;
            configuration=configuration ? configuration : Resources.Load<TunnelAudioConfiguration>("SpatialAudio/TunnelAudioConfiguration");
            if (!configuration) throw new System.InvalidOperationException("Tunnel audio configuration is missing. Run the acoustic asset export.");
            var binding=view.gameObject.AddComponent<TunnelAudioBinding>();
            binding.view=view;
            binding.Runtime=new TunnelAudioRuntime(view.transform,configuration);
            view.GeometryClearing+=binding.OnClearing;
            view.GeometryReady+=binding.OnReady;
            if (view.GeometryRoot) binding.OnReady(view.GeometryRoot);
            return binding;
        }
        void OnClearing()
        {
            if (pending!=null) { StopCoroutine(pending); pending=null; }
            Runtime?.Clear();
        }
        void OnReady(Transform geometry)
        {
            if (pending!=null) StopCoroutine(pending);
            pending=StartCoroutine(BuildAfterDeferredDestruction(geometry));
        }
        IEnumerator BuildAfterDeferredDestruction(Transform geometry)
        {
            // The generator removes marker colliders with Destroy, which completes at the end of this frame.
            yield return null;
            if (geometry) { Runtime.Rebuild(geometry); BuildCount++; }
            pending=null;
        }
        void Update()
        {
            if (Runtime==null) return;
            if (!player)
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    player=root.GetComponentInChildren<PlayerView>();
                    if (player) break;
                }
                if (player)
                {
                    footAnchor=new GameObject("Spatial Audio Foot Position").transform;
                    footAnchor.SetParent(player.transform,false);
                    footAnchor.localPosition=new Vector3(0,.15f,0);
                    player.Warped+=OnWarp;
                    Runtime.BindPlayer(player.MainCamera ? player.MainCamera.transform : player.transform,footAnchor);
                }
            }
            Runtime.Tick(Time.deltaTime,player && player.IsControllerGrounded,Time.timeScale<=0);
        }
        void OnWarp(Vector3 offset) { Runtime?.NotifyWarp(); }
        void LateUpdate() { Runtime?.LateTick(); }
        void OnDestroy()
        {
            if (view) { view.GeometryClearing-=OnClearing; view.GeometryReady-=OnReady; }
            if (player) player.Warped-=OnWarp;
            if (footAnchor) Destroy(footAnchor.gameObject);
            Runtime?.Dispose();
        }
    }
}
