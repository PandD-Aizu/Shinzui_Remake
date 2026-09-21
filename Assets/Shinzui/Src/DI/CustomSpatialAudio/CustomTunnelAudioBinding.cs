using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Infrastructure.CustomSpatialAudio;
using Shinzui.View;
using Shinzui.View.GenerateTunnel;
using UnityEngine;

namespace Shinzui.DI.CustomSpatialAudio
{
    /// <summary>Composition root; Views expose lifecycle/input only and never reference this binding.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class CustomTunnelAudioBinding : MonoBehaviour
    {
        TunnelMapView view;
        PlayerView player;
        Transform feet;
        public CustomTunnelAudioRuntime Runtime { get; private set; }
        public int BuildCount { get; private set; }

        public static CustomTunnelAudioBinding Attach(TunnelMapView view)
        {
            var existing = view.GetComponent<CustomTunnelAudioBinding>();
            if (existing) return existing;
            var config = Resources.Load<CustomSpatialAudioConfiguration>("SpatialAudio/CustomSpatialAudioConfiguration");
            if (!config) throw new System.InvalidOperationException("CustomSpatialAudioConfiguration resource is missing.");
            // Construct first so a failed initialization cannot leave a half-attached component.
            var runtime = new CustomTunnelAudioRuntime(config);
            var binding = view.gameObject.AddComponent<CustomTunnelAudioBinding>();
            binding.view = view; binding.Runtime = runtime;
            view.GeometryClearing += binding.OnClearing;
            return binding;
        }
        public void Rebuild(TunnelMapDto map) { Runtime.Rebuild(map, view.GeometryRoot); BuildCount++; }
        void OnClearing() => Runtime?.Clear();
        void Update()
        {
            if (Runtime == null) return;
            if (!player)
            {
                if (feet) Destroy(feet.gameObject);
                foreach (var root in gameObject.scene.GetRootGameObjects())
                { player = root.GetComponentInChildren<PlayerView>(); if (player) break; }
                if (player)
                {
                    feet = new GameObject("Custom Audio Foot Position").transform;
                    feet.SetParent(player.transform, false); feet.localPosition = new Vector3(0, .15f, 0);
                    player.Warped += OnWarp;
                    Runtime.BindPlayer(player.MainCamera ? player.MainCamera.transform : player.transform, feet);
                }
                else Runtime.BindPlayer(null, null);
            }
            Runtime.Tick(Time.unscaledDeltaTime, player && player.IsControllerGrounded, Time.timeScale <= 0 || !isActiveAndEnabled);
        }
        void OnDisable() { Runtime?.Voices.SetSuspended(true); }
        void OnWarp(Vector3 offset) => Runtime?.NotifyWarp();
        void LateUpdate() => Runtime?.LateTick();
        void OnDestroy()
        {
            if (view) view.GeometryClearing -= OnClearing;
            if (player) player.Warped -= OnWarp;
            if (feet) Destroy(feet.gameObject);
            Runtime?.Dispose();
        }
    }
}
