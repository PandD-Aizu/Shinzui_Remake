using System;
using System.Collections.Generic;
using System.IO;
using FMOD;
using Shinzui.Application.CustomSpatialAudio;
using Shinzui.Application.SpatialAudio;
using Shinzui.Infrastructure.CustomSpatialAudio.Binaural;
using Shinzui.Infrastructure.CustomSpatialAudio.Dsp;
using UnityEngine;
using static Shinzui.Infrastructure.CustomSpatialAudio.FmodRoomAcousticProcessor;

namespace Shinzui.Infrastructure.CustomSpatialAudio
{
    /// <summary>
    /// Bounded dry Core voices routed through the game's Studio bus. DSPs are preallocated,
    /// paused while idle/prepared, and retained through each sound's acoustic tail. Main thread only.
    /// World-space poses use the generated geometry's frame for the acoustic calculation.
    /// </summary>
    public sealed class CustomSpatialAudioService : ISpatialAudioService, IDisposable
    {
        sealed class Slot
        {
            public SpatialVoiceHandle Handle;
            public FmodRoomAcousticProcessor Processor;
            public Channel Channel;
            public Sound Sound;
            public AudioPose Pose;
            public IAudioPoseSource Follow;
            public int Priority;
            public bool Ready, Paused, Draining;
            public float TailSeconds, TailBudget, Volume;
        }
        readonly FMOD.System system;
        readonly Slot[] slots;
        readonly Dictionary<string, Sound> sounds = new Dictionary<string, Sound>();
        readonly CustomSpatialAudioConfiguration configuration;
        readonly HrirDataset hrir;
        readonly SpatialDspParameters silence;
        AudioPose listener = AudioPose.At(0, 1.6f, 0);
        GeneratedAcousticWorld world;
        Transform frame;
        bool suspended, disposed;
        float updateCountdown;
        public int ActiveVoiceCount { get; private set; }
        public int ProcessorCount => slots.Length;
        public int StartedCount { get; private set; }
        public int CallbackErrorCount { get { int n = 0; foreach (var s in slots) if (s?.Processor?.CallbackError != RESULT.OK) n++; return n; } }

        public CustomSpatialAudioService(FMOD.System system, ChannelGroup parent,
            CustomSpatialAudioConfiguration configuration, string streamingAssetsPath, HrirDataset hrir = null)
        {
            this.system = system;
            this.configuration = configuration ? configuration : throw new ArgumentNullException(nameof(configuration));
            this.hrir = hrir;
            if (configuration.MaxVoices < 4 || configuration.MaxVoices > 16 || configuration.UpdatesPerSecond < 1)
                throw new ArgumentException("Invalid custom spatial audio voice/update budget.");
            Check(system.getSoftwareFormat(out int rate, out _, out _));
            silence = SpatialDspParameters.Create(GeneratedAcousticWorld.Silence, rate);
            slots = new Slot[configuration.MaxVoices];
            try
            {
                foreach (var entry in configuration.Sounds)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || sounds.ContainsKey(entry.Id))
                        throw new ArgumentException("Dry sound IDs must be nonempty and unique.");
                    var path = Path.GetFullPath(Path.Combine(streamingAssetsPath, entry.Path ?? ""));
                    string root = Path.GetFullPath(streamingAssetsPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                        throw new ArgumentException("Dry sound must exist inside StreamingAssets: " + entry.Id);
                    Check(system.createSound(path, MODE._2D | MODE.CREATESAMPLE | (entry.Loop ? MODE.LOOP_NORMAL : MODE.LOOP_OFF), out var sound));
                    sounds.Add(entry.Id, sound);
                    Check(sound.getFormat(out _, out _, out int channels, out _));
                    if (channels != 1) throw new ArgumentException("Custom spatial audio requires dry mono assets: " + entry.Id);
                }
                for (int i = 0; i < slots.Length; i++)
                {
                    slots[i] = new Slot { Processor = new FmodRoomAcousticProcessor(system, parent, silence) };
                    slots[i].Processor.SetPaused(true);
                }
            }
            catch { Dispose(); throw; }
        }

        public void SetWorld(GeneratedAcousticWorld value, Transform coordinateFrame = null)
        { StopAll(SpatialAudioStopMode.Immediate); world = value; frame = coordinateFrame; updateCountdown = 0; }
        public void SetListener(AudioPose pose) { if (pose.IsValid) listener = pose; }
        public void SetSuspended(bool value)
        {
            if (suspended == value || disposed) return;
            suspended = value;
            foreach (var slot in slots) if (slot.Handle.IsValid) ApplyPause(slot);
        }

        public SpatialAudioPlayResult Play(SpatialAudioRequest request, IAudioPoseSource follow = null) => Allocate(request, follow, true);
        public SpatialAudioPlayResult Prepare(SpatialAudioRequest request, IAudioPoseSource follow = null) => Allocate(request, follow, false);
        SpatialAudioPlayResult Allocate(SpatialAudioRequest request, IAudioPoseSource follow, bool start)
        {
            if (disposed) return new SpatialAudioPlayResult(SpatialAudioPlayStatus.Disposed);
            if (!request.IsValid) return new SpatialAudioPlayResult(SpatialAudioPlayStatus.InvalidRequest);
            var pose = request.Pose;
            if (follow != null && (!follow.TryGetPose(out pose) || !pose.IsValid))
                return new SpatialAudioPlayResult(SpatialAudioPlayStatus.InvalidRequest);
            if (!sounds.TryGetValue(request.EventId, out var sound)) return new SpatialAudioPlayResult(SpatialAudioPlayStatus.UnknownEvent);
            if (world == null) return new SpatialAudioPlayResult(SpatialAudioPlayStatus.BackendUnavailable);
            Slot free = null, weakest = null;
            foreach (var s in slots)
            {
                if (!s.Handle.IsValid) { free = s; break; }
                if (weakest == null || s.Priority < weakest.Priority ||
                    (s.Priority == weakest.Priority && s.Draining && (!weakest.Draining || s.TailSeconds > weakest.TailSeconds))) weakest = s;
            }
            if (free == null)
            {
                // At equal priority, recycle the oldest completed dry sound's tail before
                // dropping the next foot contact. Never steal an equal-priority dry/ready voice.
                if (weakest == null || weakest.Priority > request.Priority ||
                    (weakest.Priority == request.Priority && !weakest.Draining))
                    return new SpatialAudioPlayResult(SpatialAudioPlayStatus.VoiceLimit);
                Release(weakest); free = weakest;
            }
            free.Handle = new SpatialVoiceHandle(Guid.NewGuid());
            free.Sound = sound; free.Pose = pose; free.Follow = follow; free.Priority = request.Priority;
            free.Ready = true; free.Paused = false; free.Draining = false;
            free.TailSeconds = 0; free.TailBudget = .1f; free.Volume = request.Volume;
            ActiveVoiceCount++;
            try
            {
                if (start) Start(free, false);
                return new SpatialAudioPlayResult(SpatialAudioPlayStatus.Accepted, free.Handle);
            }
            catch (Exception error)
            {
                Release(free); UnityEngine.Debug.LogException(error);
                return new SpatialAudioPlayResult(SpatialAudioPlayStatus.BackendUnavailable);
            }
        }

        public bool StartPrepared(SpatialVoiceHandle handle)
        {
            var slot = Find(handle);
            if (slot == null || !slot.Ready) return false;
            if (slot.Follow != null && (!slot.Follow.TryGetPose(out slot.Pose) || !slot.Pose.IsValid)) { Release(slot); return false; }
            Start(slot, true);
            return true;
        }
        void Start(Slot slot, bool detach)
        {
            slot.Processor.Reset(Parameters(slot));
            slot.Processor.SetVolume(slot.Volume);
            Check(system.playSound(slot.Sound, slot.Processor.Group, true, out slot.Channel));
            // Clear the channel pause while the group still holds it; publish the whole response together.
            Check(slot.Channel.setPaused(false));
            slot.Ready = false;
            if (detach) slot.Follow = null;
            ApplyPause(slot); StartedCount++;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (disposed) return;
            float dt = Mathf.Clamp(unscaledDeltaTime, 0, .25f);
            updateCountdown -= dt;
            bool update = updateCountdown <= 0;
            if (update) updateCountdown = 1f / configuration.UpdatesPerSecond;
            foreach (var slot in slots)
            {
                if (!slot.Handle.IsValid) continue;
                if (slot.Follow != null)
                {
                    if (!slot.Follow.TryGetPose(out slot.Pose) || !slot.Pose.IsValid) { Release(slot); continue; }
                }
                if (slot.Ready || slot.Paused || suspended) continue;
                if (update) slot.Processor.SetParameters(Parameters(slot));
                if (!slot.Draining)
                {
                    var result = slot.Channel.isPlaying(out bool playing);
                    if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE) Check(result);
                    if (result == RESULT.ERR_INVALID_HANDLE || !playing) { slot.Draining = true; slot.Channel.clearHandle(); }
                }
                if (slot.Draining)
                {
                    slot.TailSeconds += dt;
                    if (slot.TailSeconds >= slot.TailBudget) Release(slot);
                }
            }
        }

        SpatialDspParameters Parameters(Slot slot)
        {
            var source = Local(slot.Pose.Position, false);
            var head = Local(listener.Position, false);
            var forward = Local(listener.Forward, true); var up = Local(listener.Up, true);
            var right = new AcousticVector3(up.Y * forward.Z - up.Z * forward.Y,
                up.Z * forward.X - up.X * forward.Z, up.X * forward.Y - up.Y * forward.X);
            var response = world == null ? GeneratedAcousticWorld.Silence : world.Calculate(source, head, right);
            float rt60 = Math.Max(response.Rt60Seconds.Low, Math.Max(response.Rt60Seconds.Mid, response.Rt60Seconds.High));
            float delay = 0;
            for (int i = 0; i < RoomAcousticResponse.PathCount; i++) delay = Math.Max(delay, response.GetPath(i).DelaySeconds);
            slot.TailBudget = Math.Max(slot.TailBudget, rt60 + delay + .1f);
            return SpatialDspParameters.Create(response, slot.Processor.SampleRate, earlyGain: configuration.EarlyGain,
                lateGain: configuration.LateGain, hrirDataset: hrir, listenerForward: forward, listenerUp: up);
        }
        AcousticVector3 Local(AudioVector value, bool direction)
        {
            var v = new Vector3(value.X, value.Y, value.Z);
            if (frame) v = direction ? frame.InverseTransformDirection(v) : frame.InverseTransformPoint(v);
            return new AcousticVector3(v.x, v.y, v.z);
        }
        void ApplyPause(Slot slot) => slot.Processor.SetPaused(slot.Ready || slot.Paused || suspended);
        Slot Find(SpatialVoiceHandle handle)
        { if (!handle.IsValid || disposed) return null; foreach (var s in slots) if (s.Handle.Equals(handle)) return s; return null; }
        public SpatialVoiceState GetState(SpatialVoiceHandle handle)
        {
            var s = Find(handle);
            return s == null ? SpatialVoiceState.Stopped : s.Ready ? SpatialVoiceState.Ready :
                s.Paused || suspended ? SpatialVoiceState.Paused : s.Draining ? SpatialVoiceState.Stopping : SpatialVoiceState.Playing;
        }
        public bool UpdatePose(SpatialVoiceHandle handle, AudioPose pose)
        { var s = Find(handle); if (s == null || !pose.IsValid) return false; s.Pose = pose; s.Follow = null; updateCountdown = 0; return true; }
        public bool SetVolume(SpatialVoiceHandle handle, float volume)
        { var s = Find(handle); if (s == null || float.IsNaN(volume) || volume < 0 || volume > 1) return false; s.Volume = volume; s.Processor.SetVolume(volume); return true; }
        public bool SetPaused(SpatialVoiceHandle handle, bool paused)
        { var s = Find(handle); if (s == null) return false; s.Paused = paused; ApplyPause(s); return true; }
        public bool Stop(SpatialVoiceHandle handle, SpatialAudioStopMode mode = SpatialAudioStopMode.AllowFadeOut)
        {
            var s = Find(handle); if (s == null) return false;
            if (mode == SpatialAudioStopMode.Immediate || s.Ready) Release(s);
            else if (!s.Draining) { if (s.Channel.hasHandle()) s.Channel.stop(); s.Channel.clearHandle(); s.Draining = true; s.Follow = null; }
            return true;
        }
        public void StopAll(SpatialAudioStopMode mode = SpatialAudioStopMode.AllowFadeOut)
        { foreach (var s in slots) if (s != null && s.Handle.IsValid) Stop(s.Handle, mode); }
        void Release(Slot s)
        {
            if (!s.Handle.IsValid) return;
            s.Processor.SetPaused(true);
            if (s.Channel.hasHandle()) s.Channel.stop();
            s.Channel.clearHandle(); s.Handle = default; s.Follow = null; ActiveVoiceCount--;
        }
        public bool TryInspect(SpatialVoiceHandle handle, out ChannelGroup group, out AudioPose pose)
        { var s = Find(handle); group = s?.Processor.Group ?? default; pose = s?.Pose ?? default; return s != null; }
        public void Dispose()
        {
            if (disposed) return;
            StopAll(SpatialAudioStopMode.Immediate);
            foreach (var s in slots) s?.Processor?.Dispose();
            foreach (var sound in sounds.Values) Check(sound.release());
            sounds.Clear(); disposed = true;
        }
    }
}
