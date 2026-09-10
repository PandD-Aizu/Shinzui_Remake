using System;
using System.Collections.Generic;

namespace Shinzui.Application.SpatialAudio
{
    /// <summary>Two reserved sources follow the feet before contact; emitted tails remain in world space.</summary>
    public sealed class PreparedFootstepUseCase : IDisposable
    {
        readonly ISpatialAudioService service;
        readonly IAudioPoseSource follow;
        readonly string eventId;
        readonly List<SpatialVoiceHandle> prepared = new List<SpatialVoiceHandle>(2);
        readonly List<SpatialVoiceHandle> emitted = new List<SpatialVoiceHandle>();
        AudioPose lastPose;
        bool hasPose, disposed;
        float distance, cooldown;
        public int EmittedCount { get; private set; }
        public int MissedCount { get; private set; }
        public int ReadyCount
        {
            get { int count = 0; foreach (var h in prepared) if (service.GetState(h) == SpatialVoiceState.Ready) count++; return count; }
        }
        public PreparedFootstepUseCase(ISpatialAudioService service, IAudioPoseSource follow, string eventId)
        { this.service = service; this.follow = follow; this.eventId = eventId; }
        public void Tick(float deltaTime, bool grounded)
        {
            if (disposed || !follow.TryGetPose(out var pose) || !pose.IsValid) return;
            for (int i = emitted.Count-1; i >= 0; i--) if (service.GetState(emitted[i]) == SpatialVoiceState.Stopped) emitted.RemoveAt(i);
            for (int i = prepared.Count-1; i >= 0; i--) if (service.GetState(prepared[i]) == SpatialVoiceState.Stopped) prepared.RemoveAt(i);
            float dx = hasPose ? pose.Position.X-lastPose.Position.X : 0;
            float dz = hasPose ? pose.Position.Z-lastPose.Position.Z : 0;
            float travel = (float)Math.Sqrt(dx*dx+dz*dz);
            if (travel > 4) Reset();
            else if (grounded && deltaTime > 0)
            {
                distance += travel;
                cooldown = Math.Max(0, cooldown-deltaTime);
                if (distance >= 1.6f && cooldown <= 0)
                { Trigger(); distance = 0; cooldown = .3f; }
            }
            else distance = 0;
            lastPose = pose; hasPose = true;
            while (prepared.Count < 2)
            {
                var result = service.Prepare(new SpatialAudioRequest(eventId, pose, SpatialAudioCategory.Footstep, 96, .5f), follow);
                if (!result.Accepted) break;
                prepared.Add(result.Handle);
            }
        }
        public SpatialVoiceHandle Trigger()
        {
            if (disposed) return default;
            for (int i = 0; i < prepared.Count; i++)
            {
                var handle = prepared[i];
                if (service.GetState(handle) != SpatialVoiceState.Ready) continue;
                if (!service.StartPrepared(handle)) continue;
                prepared.RemoveAt(i); emitted.Add(handle); EmittedCount++;
                return handle;
            }
            MissedCount++; // Do not queue a delayed footstep after the contact has passed.
            return default;
        }
        public void Reset()
        {
            foreach (var h in prepared) service.Stop(h, SpatialAudioStopMode.Immediate);
            foreach (var h in emitted) service.Stop(h, SpatialAudioStopMode.Immediate);
            prepared.Clear(); emitted.Clear(); distance = cooldown = 0; hasPose = false;
        }
        public void Dispose() { if (!disposed) { Reset(); disposed = true; } }
    }
}
