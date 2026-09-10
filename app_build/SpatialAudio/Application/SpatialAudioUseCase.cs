using System;
using System.Collections.Generic;

namespace Shinzui.Application.SpatialAudio
{
    /// <summary>Owns only this caller's voices; disposing it cannot stop another caller's playback.</summary>
    public sealed class SpatialAudioUseCase : IDisposable
    {
        readonly ISpatialAudioService service;
        readonly HashSet<SpatialVoiceHandle> owned = new HashSet<SpatialVoiceHandle>();
        bool disposed;
        public SpatialAudioUseCase(ISpatialAudioService service) { this.service = service ?? throw new ArgumentNullException(nameof(service)); }
        public SpatialAudioPlayResult Play(SpatialAudioRequest request, IAudioPoseSource follow = null)
        {
            if (disposed) return new SpatialAudioPlayResult(SpatialAudioPlayStatus.Disposed);
            owned.RemoveWhere(handle => service.GetState(handle) == SpatialVoiceState.Stopped);
            var result = service.Play(request, follow);
            if (result.Accepted) owned.Add(result.Handle);
            return result;
        }
        public bool Move(SpatialVoiceHandle handle, AudioPose pose) => !disposed && owned.Contains(handle) && service.UpdatePose(handle, pose);
        public bool Stop(SpatialVoiceHandle handle, SpatialAudioStopMode mode = SpatialAudioStopMode.AllowFadeOut) =>
            !disposed && owned.Contains(handle) && service.Stop(handle, mode);
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var handle in owned) service.Stop(handle, SpatialAudioStopMode.Immediate);
            owned.Clear();
        }
    }
}
