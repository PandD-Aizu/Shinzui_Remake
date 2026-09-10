using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using Shinzui.Application.SpatialAudio;
using SteamAudio;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Shinzui.Infrastructure.SpatialAudio
{
    public sealed class FmodSpatialAudioService : ISpatialAudioService, IDisposable
    {
        sealed class Slot
        {
            public GameObject Object;
            public ManagedSpatialEmitter Emitter;
            public SteamAudioSource Acoustics;
            public EventInstance Instance;
            public SpatialVoiceHandle Handle;
            public SpatialVoiceState State;
            public SpatialAudioCategory Category;
            public IAudioPoseSource Follow;
            public AudioPose Pose;
            public int Priority, StartFrame;
            public long Sequence;
            public double StartedAt, StopStartedAt;
            public bool Paused, AutoStart;
        }

        public readonly struct VoiceDiagnostics
        {
            public readonly IntPtr NativeInstance;
            public readonly Vector3 Position, Velocity;
            public readonly int TimelineMilliseconds;
            public readonly GameObject PoolObject;
            public readonly bool SpatializerReady;
            public readonly SpatialAudioCategory Category;
            public VoiceDiagnostics(IntPtr nativeInstance, Vector3 position, Vector3 velocity, int timeline, GameObject poolObject,
                bool ready, SpatialAudioCategory category)
            { NativeInstance = nativeInstance; Position = position; Velocity = velocity; TimelineMilliseconds = timeline;
                PoolObject = poolObject; SpatializerReady = ready; Category = category; }
        }

        readonly Dictionary<string, EventReference> events = new Dictionary<string, EventReference>(StringComparer.Ordinal);
        readonly Dictionary<string, EventDescription> loaded = new Dictionary<string, EventDescription>(StringComparer.Ordinal);
        readonly List<Slot> slots;
        readonly Transform owner;
        readonly int capacity;
        readonly float preparationSeconds, preparationTimeout, stopTimeout;
        readonly SpatialPropagationSettings propagation;
        bool disposed, suspended;
        double suspendedAt;
        long sequence;
        public int ActiveVoiceCount { get; private set; }
        public int PoolSize => slots.Count;
        public int CreatedInstanceCount { get; private set; }
        public int ReleasedInstanceCount { get; private set; }
        public int FailureCount { get; private set; }
        public string LastFailure { get; private set; }

        public FmodSpatialAudioService(SpatialAudioRuntimeOptions options)
        {
            if (options == null || !options.Catalog || !options.Owner) throw new ArgumentException("Spatial audio requires a catalog and a scene owner.");
            if (options.MaxVoices < 1 || options.MaxVoices > SteamAudioSettings.Singleton.realTimeMaxSources)
                throw new ArgumentOutOfRangeException(nameof(options.MaxVoices), "Voice capacity must fit the configured Steam Audio source budget.");
            if (SteamAudioSettings.Singleton.audioEngine != AudioEngineType.FMODStudio)
                throw new InvalidOperationException("Steam Audio must use the FMOD Studio audio engine.");
            if (!Finite(options.PreparationSeconds) || options.PreparationSeconds < 0 ||
                !Finite(options.PreparationTimeoutSeconds) || options.PreparationTimeoutSeconds <= options.PreparationSeconds ||
                !Finite(options.StopTimeoutSeconds) || options.StopTimeoutSeconds <= 0)
                throw new ArgumentException("Invalid spatial audio timeouts.");
            owner = options.Owner; capacity = options.MaxVoices;
            propagation = options.Propagation?.Snapshot();
            if (propagation != null && propagation.VolumetricOcclusion && propagation.OcclusionSamples > SteamAudioSettings.Singleton.maxOcclusionSamples)
                throw new ArgumentException("Occlusion samples exceed the Steam Audio scene budget. Apply the propagation configuration first.");
            preparationSeconds = Math.Max(options.PreparationSeconds, SteamAudioSettings.Singleton.simulationUpdateInterval * 2);
            preparationTimeout = options.PreparationTimeoutSeconds; stopTimeout = options.StopTimeoutSeconds;
            if (preparationTimeout <= preparationSeconds) throw new ArgumentException("Preparation timeout is shorter than the simulation warmup.");
            slots = new List<Slot>(capacity);
            foreach (var entry in options.Catalog.Entries ?? Array.Empty<SpatialAudioCatalog.Entry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || entry.Event.IsNull || events.ContainsKey(entry.Id))
                    throw new ArgumentException("Spatial audio catalog contains an invalid or duplicate ID.");
                events.Add(entry.Id, entry.Event);
            }
        }

        public SpatialAudioPlayResult Play(SpatialAudioRequest request, IAudioPoseSource follow = null)
            => Allocate(request, follow, true);

        public SpatialAudioPlayResult Prepare(SpatialAudioRequest request, IAudioPoseSource follow = null)
            => Allocate(request, follow, false);

        public bool StartPrepared(SpatialVoiceHandle handle)
        {
            var slot = Find(handle);
            if (slot == null || slot.State != SpatialVoiceState.Ready) return false;
            return Run(slot, () => {
                if (slot.Follow != null)
                {
                    if (!slot.Follow.TryGetPose(out var pose) || !pose.IsValid) throw new InvalidOperationException("Prepared source lost its target.");
                    ApplyPose(slot, pose);
                }
                slot.Follow = null; // The emitted sound stays at the foot-contact position.
                slot.Paused = false;
                Check(slot.Instance.setPaused(suspended));
                slot.State = suspended ? SpatialVoiceState.Paused : SpatialVoiceState.Playing;
            });
        }

        SpatialAudioPlayResult Allocate(SpatialAudioRequest request, IAudioPoseSource follow, bool autoStart)
        {
            if (disposed || !owner) return new SpatialAudioPlayResult(SpatialAudioPlayStatus.Disposed);
            if (!request.IsValid) return new SpatialAudioPlayResult(SpatialAudioPlayStatus.InvalidRequest);
            var pose = request.Pose;
            if (follow != null && (!follow.TryGetPose(out pose) || !pose.IsValid))
                return new SpatialAudioPlayResult(SpatialAudioPlayStatus.InvalidRequest);
            if (!events.TryGetValue(request.EventId, out var reference)) return new SpatialAudioPlayResult(SpatialAudioPlayStatus.UnknownEvent);

            Slot slot = null, weakest = null;
            foreach (var candidate in slots)
            {
                if (!candidate.Handle.IsValid) { slot = candidate; break; }
                if (weakest == null || candidate.Priority < weakest.Priority ||
                    (candidate.Priority == weakest.Priority && candidate.Sequence < weakest.Sequence)) weakest = candidate;
            }
            if (slot == null && slots.Count == capacity && request.Priority <= weakest.Priority)
                return new SpatialAudioPlayResult(SpatialAudioPlayStatus.VoiceLimit);

            EventInstance instance = default;
            try
            {
                if (!loaded.TryGetValue(request.EventId, out var description))
                {
                    description = RuntimeManager.GetEventDescription(reference);
                    Check(description.is3D(out bool is3D));
                    if (!is3D) throw new InvalidOperationException("Spatial catalog event must be 3D: " + request.EventId);
                    Check(description.loadSampleData());
                    loaded.Add(request.EventId, description);
                }
                Check(description.createInstance(out instance)); CreatedInstanceCount++;
                Check(instance.set3DAttributes(Attributes(pose)));
                Check(instance.setVolume(request.Volume));
                // FMOD priority is inverse to the public API: 0 is most important.
                Check(instance.setProperty(EVENT_PROPERTY.CHANNELPRIORITY, 255 - request.Priority));
                Check(instance.setPaused(true));
                if (slot == null)
                {
                    if (slots.Count < capacity) { slot = CreateSlot(); slots.Add(slot); }
                    else { slot = weakest; Release(slot, true); }
                }
                slot.Instance = instance; instance.clearHandle(); // Slot owns release from here.
                slot.Handle = new SpatialVoiceHandle(Guid.NewGuid());
                slot.Priority = request.Priority; slot.Category = request.Category; slot.Sequence = ++sequence;
                slot.Follow = follow; slot.Pose = pose; slot.Paused = false;
                slot.AutoStart = autoStart;
                slot.State = SpatialVoiceState.Preparing; slot.StartedAt = Now; slot.StartFrame = Time.frameCount;
                ActiveVoiceCount++;
                SetTransform(slot, pose);
                slot.Emitter.Bind(slot.Instance);
                slot.Object.SetActive(true);
                Check(slot.Instance.start());
                return new SpatialAudioPlayResult(SpatialAudioPlayStatus.Accepted, slot.Handle);
            }
            catch (Exception exception)
            {
                if (instance.isValid()) { instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); instance.release(); ReleasedInstanceCount++; }
                if (slot != null && slot.Handle.IsValid) Release(slot, true);
                Fail(exception.Message);
                return new SpatialAudioPlayResult(SpatialAudioPlayStatus.BackendUnavailable);
            }
        }

        Slot CreateSlot()
        {
            var obj = new GameObject("Spatial Voice " + slots.Count);
            obj.SetActive(false);
            obj.transform.SetParent(owner, false);
            try
            {
                var emitter = obj.AddComponent<ManagedSpatialEmitter>();
                var acoustics = obj.AddComponent<SteamAudioSource>();
                acoustics.reflections = true; acoustics.reflectionsType = ReflectionsType.Realtime;
                acoustics.occlusion = true; acoustics.occlusionInput = OcclusionInput.SimulationDefined;
                propagation?.Configure(acoustics);
                return new Slot { Object = obj, Emitter = emitter, Acoustics = acoustics };
            }
            catch { UnityEngine.Object.Destroy(obj); throw; }
        }

        public void Tick()
        {
            if (disposed) return;
            if (!owner) { Dispose(); return; }
            foreach (var slot in slots)
            {
                if (!slot.Handle.IsValid) continue;
                try
                {
                    if (!slot.Object || !slot.Emitter || !slot.Acoustics) { Release(slot, true); continue; }
                    if (slot.Follow != null)
                    {
                        if (!slot.Follow.TryGetPose(out var pose) || !pose.IsValid) { Release(slot, true); continue; }
                        ApplyPose(slot, pose);
                    }
                    Check(slot.Instance.getPlaybackState(out var state));
                    if (slot.State == SpatialVoiceState.Preparing)
                    {
                        if (Now - slot.StartedAt > preparationTimeout) throw new TimeoutException("Spatializer/sample preparation timed out.");
                        if (Time.frameCount <= slot.StartFrame + 2 || Now - slot.StartedAt < preparationSeconds) continue;
                        Check(slot.Instance.getDescription(out var description));
                        Check(description.getSampleLoadingState(out var sampleState));
                        if (sampleState == LOADING_STATE.ERROR) throw new InvalidOperationException("FMOD sample loading failed.");
                        if (sampleState != LOADING_STATE.LOADED || !FindSpatializer(slot.Instance, out var spatializer)) continue;
                        propagation?.Configure(spatializer);
                        if (slot.AutoStart)
                        {
                            Check(slot.Instance.setPaused(slot.Paused || suspended));
                            slot.State = slot.Paused || suspended ? SpatialVoiceState.Paused : SpatialVoiceState.Playing;
                        }
                        else slot.State = SpatialVoiceState.Ready;
                    }
                    else if (state == PLAYBACK_STATE.STOPPED) Release(slot, false);
                    else if (!suspended && slot.State == SpatialVoiceState.Stopping && Now - slot.StopStartedAt > stopTimeout)
                    { Fail("Fade-out exceeded its deadline; voice stopped immediately."); Release(slot, true); }
                }
                catch (Exception exception) { Fail(exception.Message); Release(slot, true); }
            }
        }

        public SpatialVoiceState GetState(SpatialVoiceHandle handle) => Find(handle)?.State ?? SpatialVoiceState.Stopped;
        public bool UpdatePose(SpatialVoiceHandle handle, AudioPose pose)
        {
            var slot = Find(handle);
            if (slot == null || !pose.IsValid) return false;
            slot.Follow = null; // Explicit positioning takes over from a tracking provider.
            return Run(slot, () => ApplyPose(slot, pose));
        }
        public bool SetVolume(SpatialVoiceHandle handle, float volume)
        {
            var slot = Find(handle);
            return slot != null && Finite(volume) && volume >= 0 && volume <= 1 && Run(slot, () => Check(slot.Instance.setVolume(volume)));
        }
        public bool SetPaused(SpatialVoiceHandle handle, bool paused)
        {
            var slot = Find(handle);
            if (slot == null || slot.State == SpatialVoiceState.Stopping || slot.State == SpatialVoiceState.Ready) return false;
            return Run(slot, () => {
                slot.Paused = paused;
                if (slot.State != SpatialVoiceState.Preparing)
                { Check(slot.Instance.setPaused(paused || suspended)); slot.State = paused || suspended ? SpatialVoiceState.Paused : SpatialVoiceState.Playing; }
            });
        }
        /// <summary>Scene pause is independent of each voice's own pause flag. Prepared voices stay inaudible.</summary>
        public void SetSuspended(bool value)
        {
            if (disposed || suspended == value) return;
            double now = Now;
            suspended = value;
            if (value) suspendedAt = now;
            foreach (var slot in slots)
            {
                if (!slot.Handle.IsValid || slot.State == SpatialVoiceState.Preparing || slot.State == SpatialVoiceState.Ready) continue;
                try
                {
                    Check(slot.Instance.setPaused(value || slot.Paused));
                    if (slot.State == SpatialVoiceState.Stopping)
                    {
                        if (!value) slot.StopStartedAt += now - Math.Max(suspendedAt, slot.StopStartedAt);
                    }
                    else slot.State = value || slot.Paused ? SpatialVoiceState.Paused : SpatialVoiceState.Playing;
                }
                catch (Exception exception) { Fail(exception.Message); Release(slot, true); }
            }
        }
        public bool Stop(SpatialVoiceHandle handle, SpatialAudioStopMode mode = SpatialAudioStopMode.AllowFadeOut)
        {
            var slot = Find(handle);
            if (slot == null || !Enum.IsDefined(typeof(SpatialAudioStopMode), mode)) return false;
            if (mode == SpatialAudioStopMode.Immediate || slot.State == SpatialVoiceState.Preparing || slot.State == SpatialVoiceState.Ready) { Release(slot, true); return true; }
            if (slot.State == SpatialVoiceState.Stopping) return true;
            return Run(slot, () => {
                Check(slot.Instance.setPaused(false));
                Check(slot.Instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT));
                if (suspended) Check(slot.Instance.setPaused(true));
                slot.State = SpatialVoiceState.Stopping; slot.StopStartedAt = Now;
            });
        }
        public void StopAll(SpatialAudioStopMode mode = SpatialAudioStopMode.AllowFadeOut)
        {
            foreach (var slot in slots) if (slot.Handle.IsValid) Stop(slot.Handle, mode);
        }

        public bool TryInspect(SpatialVoiceHandle handle, out VoiceDiagnostics diagnostics)
        {
            diagnostics = default;
            var slot = Find(handle);
            if (slot == null || !slot.Object || slot.Instance.get3DAttributes(out var attributes) != FMOD.RESULT.OK ||
                slot.Instance.getTimelinePosition(out int timeline) != FMOD.RESULT.OK) return false;
            diagnostics = new VoiceDiagnostics(slot.Instance.handle, Unity(attributes.position), Unity(attributes.velocity),
                timeline, slot.Object, FindSpatializer(slot.Instance, out _), slot.Category);
            return true;
        }

        void Release(Slot slot, bool stop)
        {
            if (!slot.Handle.IsValid) return;
            if (slot.Instance.isValid())
            {
                if (stop) slot.Instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                slot.Instance.release();
            }
            ReleasedInstanceCount++;
            slot.Instance.clearHandle();
            if (slot.Emitter) slot.Emitter.Unbind();
            if (slot.Object) slot.Object.SetActive(false);
            slot.Follow = null; slot.Handle = default; slot.State = SpatialVoiceState.Stopped;
            ActiveVoiceCount--;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var slot in slots) { Release(slot, true); if (slot.Object) UnityEngine.Object.Destroy(slot.Object); }
            slots.Clear();
            foreach (var description in loaded.Values) if (description.isValid()) description.unloadSampleData();
            loaded.Clear();
        }
        Slot Find(SpatialVoiceHandle handle)
        {
            if (disposed || !handle.IsValid) return null;
            foreach (var slot in slots) if (slot.Handle.Equals(handle)) return slot;
            return null;
        }
        bool Run(Slot slot, Action operation)
        {
            try { operation(); return true; }
            catch (Exception exception) { Fail(exception.Message); Release(slot, true); return false; }
        }
        void Fail(string message) { FailureCount++; LastFailure = message; Debug.LogError("[SpatialAudio] " + message); }
        void ApplyPose(Slot slot, AudioPose pose) { Check(slot.Instance.set3DAttributes(Attributes(pose))); slot.Pose = pose; SetTransform(slot, pose); }
        static void SetTransform(Slot slot, AudioPose pose)
        { slot.Object.transform.SetPositionAndRotation(Unity(pose.Position), Quaternion.LookRotation(Unity(pose.Forward), Unity(pose.Up))); }
        static FMOD.ATTRIBUTES_3D Attributes(AudioPose pose)
        {
            var rotation = Quaternion.LookRotation(Unity(pose.Forward), Unity(pose.Up));
            return new FMOD.ATTRIBUTES_3D { position = Fmod(Unity(pose.Position)), velocity = Fmod(Unity(pose.Velocity)),
                forward = Fmod(rotation * Vector3.forward), up = Fmod(rotation * Vector3.up) };
        }
        static Vector3 Unity(AudioVector vector) => new Vector3(vector.X, vector.Y, vector.Z);
        static Vector3 Unity(FMOD.VECTOR vector) => new Vector3(vector.x, vector.y, vector.z);
        static FMOD.VECTOR Fmod(Vector3 vector) => new FMOD.VECTOR { x = vector.x, y = vector.y, z = vector.z };
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static double Now => Time.realtimeSinceStartupAsDouble;
        static void Check(FMOD.RESULT result) { if (result != FMOD.RESULT.OK) throw new InvalidOperationException("FMOD: " + result); }
        static bool FindSpatializer(EventInstance instance, out FMOD.DSP result)
        {
            result = default;
            if (!instance.isValid() || instance.getChannelGroup(out var group) != FMOD.RESULT.OK || group.getNumDSPs(out int count) != FMOD.RESULT.OK) return false;
            for (int i = 0; i < count; i++)
            {
                if (group.getDSP(i, out var dsp) == FMOD.RESULT.OK && dsp.getInfo(out string name, out _, out _, out _, out _) == FMOD.RESULT.OK && name == "Steam Audio Spatializer")
                { result = dsp; return true; }
            }
            return false;
        }
    }
}
