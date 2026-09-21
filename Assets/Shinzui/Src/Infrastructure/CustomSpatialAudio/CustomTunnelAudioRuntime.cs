using System;
using System.Collections.Generic;
using System.IO;
using FMODUnity;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.SpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio.Binaural;
using UnityEngine;
using static Shinzui.Infrastructure.CustomSpatialAudio.FmodRoomAcousticProcessor;

namespace Shinzui.Infrastructure.CustomSpatialAudio
{
    /// <summary>Owns the Studio bus lock, native voice pool and generated acoustic world for one stage.</summary>
    public sealed class CustomTunnelAudioRuntime : IDisposable
    {
        static readonly Dictionary<int, HrirDataset> HrirByRate = new Dictionary<int, HrirDataset>();
        readonly CustomSpatialAudioConfiguration configuration;
        FMOD.Studio.Bus bus;
        StudioBusLease busLease;
        bool disposed, ownsListener;
        StudioListener studioListener;
        Transform listener, feet;
        Vector3 previousPosition;
        Transform geometryFrame;
        readonly List<IAcousticDoorSource> doorSources = new List<IAcousticDoorSource>();
        readonly List<AcousticDoorState> doorStates = new List<AcousticDoorState>();
        public CustomSpatialAudioService Voices { get; private set; }
        public PreparedFootstepUseCase Footsteps { get; private set; }
        public GeneratedAcousticWorld World { get; private set; }

        public CustomTunnelAudioRuntime(CustomSpatialAudioConfiguration configuration)
        {
            this.configuration = configuration;
            try
            {
                bus = RuntimeManager.GetBus(configuration.BusPath);
                busLease = StudioBusLease.Acquire(bus);
                Check(RuntimeManager.StudioSystem.flushCommands());
                Check(bus.getChannelGroup(out var group));
                var core = RuntimeManager.CoreSystem;
                Check(core.getSoftwareFormat(out int rate, out _, out _));
                HrirDataset hrir = null;
                if (configuration.Binaural && !HrirByRate.TryGetValue(rate, out hrir))
                {
                    hrir = HrirDataset.LoadMitKemarCompact(Path.Combine(UnityEngine.Application.streamingAssetsPath,
                        "CustomSpatialAudio/Kemar/mit-kemar-compact.zip"), rate);
                    HrirByRate.Add(rate, hrir);
                }
                Voices = new CustomSpatialAudioService(core, group, configuration, UnityEngine.Application.streamingAssetsPath, hrir);
            }
            catch { Dispose(); throw; }
        }

        public void Rebuild(TunnelMapDto map, Transform geometryRoot)
        {
            Clear();
            if (!geometryRoot) throw new ArgumentNullException(nameof(geometryRoot));
            Vector3 scale = geometryRoot.lossyScale;
            if ((scale - Vector3.one).sqrMagnitude > .0001f)
                throw new InvalidOperationException("Generated acoustics requires a unit-scale geometry root (metres).");
            World = new GeneratedAcousticWorld(map);
            geometryFrame = geometryRoot;
            doorSources.Clear();
            foreach (var root in geometryRoot.gameObject.scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is IAcousticDoorSource source) doorSources.Add(source);
            Voices.SetWorld(World, geometryRoot);
        }
        public void Clear() { Footsteps?.Reset(); Voices?.SetWorld(null); World = null; }
        public void BindPlayer(Transform head, Transform foot)
        {
            if (listener == head && feet == foot) return;
            Unbind(); listener = head; feet = foot;
            if (!listener || !feet) return;
            studioListener = head.GetComponent<StudioListener>(); ownsListener = !studioListener;
            if (!studioListener) studioListener = head.gameObject.AddComponent<StudioListener>();
            previousPosition = head.position;
            Voices.SetListener(Pose(head));
            Footsteps = new PreparedFootstepUseCase(Voices, new TransformPose(feet), configuration.FootstepEventId);
        }
        public void NotifyWarp()
        {
            Footsteps?.Reset(); Voices.StopAll(SpatialAudioStopMode.Immediate);
            if (listener) { previousPosition = listener.position; Voices.SetListener(Pose(listener)); }
        }
        public void Tick(float deltaTime, bool grounded, bool paused)
        {
            if (disposed) return;
            // Studio bus pause does not advance the custom tail expiry clock.
            Check(bus.getPaused(out bool busPaused));
            paused |= busPaused || !listener;
            Voices.SetSuspended(paused);
            if (World != null && geometryFrame)
            {
                doorStates.Clear();
                foreach (var source in doorSources)
                {
                    if (source is UnityEngine.Object obj && !obj) continue;
                    if (!source.TryGetAcousticDoor(out var state)) continue;
                    var c = geometryFrame.InverseTransformPoint(new Vector3(state.Centre.X, state.Centre.Y, state.Centre.Z));
                    var n = geometryFrame.InverseTransformDirection(new Vector3(state.Normal.X, state.Normal.Y, state.Normal.Z));
                    doorStates.Add(new AcousticDoorState(new AcousticVector3(c.x, c.y, c.z), new AcousticVector3(n.x, n.y, n.z),
                        state.Width, state.Height, state.OpeningFraction));
                }
                World.SetDoors(doorStates);
            }
            if (listener)
            {
                if ((listener.position - previousPosition).sqrMagnitude > 16) NotifyWarp();
                previousPosition = listener.position;
                Voices.SetListener(Pose(listener));
            }
            Voices.Tick(deltaTime);
            if (!paused && World != null) Footsteps?.Tick(deltaTime, grounded);
        }
        public void LateTick()
        {
            if (listener && studioListener && studioListener.ListenerNumber >= 0)
                RuntimeManager.SetListenerLocation(studioListener.ListenerNumber, listener.gameObject, studioListener.AttenuationObject, Vector3.zero);
        }
        void Unbind()
        {
            Footsteps?.Dispose(); Footsteps = null;
            Voices?.StopAll(SpatialAudioStopMode.Immediate);
            if (ownsListener && studioListener) UnityEngine.Object.Destroy(studioListener);
            studioListener = null; listener = feet = null; ownsListener = false;
        }
        public void Dispose()
        {
            if (disposed) return;
            try { Unbind(); Voices?.Dispose(); }
            finally { busLease?.Dispose(); busLease = null; disposed = true; }
        }

        // Additive stages share one Studio bus. Its lock is not recursive: keep it alive
        // until the final stage releases its processors, without unlocking an external owner.
        sealed class StudioBusLease : IDisposable
        {
            sealed class State
            {
                public FMOD.Studio.Bus Bus;
                public int References;
                public bool Owned;
            }
            static readonly Dictionary<IntPtr, State> States = new Dictionary<IntPtr, State>();
            State state;
            StudioBusLease(State state) { this.state = state; state.References++; }
            public static StudioBusLease Acquire(FMOD.Studio.Bus bus)
            {
                if (!States.TryGetValue(bus.handle, out var state))
                {
                    var result = bus.lockChannelGroup();
                    if (result != FMOD.RESULT.ERR_ALREADY_LOCKED) Check(result);
                    state = new State { Bus = bus, Owned = result == FMOD.RESULT.OK };
                    States.Add(bus.handle, state);
                }
                return new StudioBusLease(state);
            }
            public void Dispose()
            {
                if (state == null) return;
                var released = state; state = null;
                if (--released.References != 0) return;
                States.Remove(released.Bus.handle);
                if (released.Owned && released.Bus.isValid()) Check(released.Bus.unlockChannelGroup());
            }
        }
        static AudioVector Vector(Vector3 v) => new AudioVector(v.x, v.y, v.z);
        static AudioPose Pose(Transform t) => new AudioPose(Vector(t.position), Vector(t.forward), Vector(t.up));
        sealed class TransformPose : IAudioPoseSource
        {
            readonly Transform target;
            public TransformPose(Transform target) { this.target = target; }
            public bool TryGetPose(out AudioPose pose) { pose = target ? Pose(target) : default; return target; }
        }
    }
}
