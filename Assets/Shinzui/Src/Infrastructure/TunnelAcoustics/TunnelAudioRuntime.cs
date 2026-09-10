using System;
using FMODUnity;
using Shinzui.Application.SpatialAudio;
using Shinzui.Infrastructure.SpatialAudio;
using SteamAudio;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Shinzui.Infrastructure.TunnelAcoustics
{
    public sealed class TunnelAudioRuntime : IDisposable
    {
        public FmodSpatialAudioService Voices { get; }
        public SteamAudioAcousticSceneService Geometry { get; }
        public PreparedFootstepUseCase Footsteps { get; private set; }
        readonly TunnelAudioConfiguration configuration;
        readonly Transform owner;
        Transform listener, feet;
        StudioListener fmodListener;
        SteamAudioListener steamListener;
        bool ownsFmodListener, ownsSteamListener, disposed;
        Vector3 previousListener;
        public TunnelAudioRuntime(Transform owner, TunnelAudioConfiguration configuration)
        {
            this.owner=owner; this.configuration=configuration;
            Geometry = new SteamAudioAcousticSceneService();
            Voices = new FmodSpatialAudioService(new SpatialAudioRuntimeOptions { Owner=owner,Catalog=configuration.Events,MaxVoices=configuration.MaxVoices,Propagation=configuration.Propagation });
        }
        public void BindPlayer(Transform listenerTransform, Transform footTransform)
        {
            if (listener == listenerTransform && feet == footTransform) return;
            UnbindPlayer(); listener=listenerTransform; feet=footTransform;
            if (!listener || !feet) return;
            fmodListener=listener.GetComponent<StudioListener>(); ownsFmodListener=!fmodListener;
            if (!fmodListener) fmodListener=listener.gameObject.AddComponent<StudioListener>();
            steamListener=listener.GetComponent<SteamAudioListener>(); ownsSteamListener=!steamListener;
            if (!steamListener) steamListener=listener.gameObject.AddComponent<SteamAudioListener>();
            SteamAudioManager.NotifyAudioListenerChangedTo(listener);
            previousListener=listener.position;
            Footsteps = new PreparedFootstepUseCase(Voices, new TransformAudioPoseSource(feet), configuration.FootstepEventId);
        }
        public void Rebuild(Transform root)
        { Clear(); Geometry.Replace(AcousticGeometryCollector.Collect(root,configuration.MeshLibrary)); }
        public void Clear() { Footsteps?.Reset(); Voices.StopAll(SpatialAudioStopMode.Immediate); Geometry.Clear(); }
        public void NotifyWarp() { Footsteps?.Reset(); if (listener) previousListener=listener.position; }
        public void Tick(float deltaTime, bool grounded, bool paused = false)
        {
            if (disposed) return;
            Voices.SetSuspended(paused);
            if (listener)
            {
                if ((listener.position-previousListener).sqrMagnitude>16) NotifyWarp();
                previousListener=listener.position;
            }
            Voices.Tick();
            if (!paused && Geometry.TriangleCount>0) Footsteps?.Tick(deltaTime,grounded);
        }
        public void LateTick()
        {
            // Use zero listener velocity at the authoritative camera pose; teleports cannot create Doppler spikes.
            if (listener && fmodListener && fmodListener.ListenerNumber>=0)
                RuntimeManager.SetListenerLocation(fmodListener.ListenerNumber, listener.gameObject, fmodListener.AttenuationObject, Vector3.zero);
        }
        void UnbindPlayer()
        {
            Footsteps?.Dispose(); Footsteps=null;
            if (ownsFmodListener && fmodListener) UnityEngine.Object.Destroy(fmodListener);
            if (ownsSteamListener && steamListener) UnityEngine.Object.Destroy(steamListener);
            listener=null; feet=null; fmodListener=null; steamListener=null;
        }
        public void Dispose()
        { if (disposed) return; disposed=true; UnbindPlayer(); Voices.Dispose(); Geometry.Dispose(); }
    }
}
