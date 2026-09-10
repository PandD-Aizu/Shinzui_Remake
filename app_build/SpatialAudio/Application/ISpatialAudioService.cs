namespace Shinzui.Application.SpatialAudio
{
    /// <summary>Main-thread API. A handle identifies one playback, including preparation and fade-out.</summary>
    public interface ISpatialAudioService
    {
        int ActiveVoiceCount { get; }
        SpatialAudioPlayResult Play(SpatialAudioRequest request, IAudioPoseSource follow = null);
        SpatialAudioPlayResult Prepare(SpatialAudioRequest request, IAudioPoseSource follow = null);
        /// <summary>Starts an already prepared voice without adding the preparation delay again.</summary>
        bool StartPrepared(SpatialVoiceHandle handle);
        SpatialVoiceState GetState(SpatialVoiceHandle handle);
        bool UpdatePose(SpatialVoiceHandle handle, AudioPose pose);
        bool SetVolume(SpatialVoiceHandle handle, float volume);
        bool SetPaused(SpatialVoiceHandle handle, bool paused);
        bool Stop(SpatialVoiceHandle handle, SpatialAudioStopMode mode = SpatialAudioStopMode.AllowFadeOut);
        void StopAll(SpatialAudioStopMode mode = SpatialAudioStopMode.AllowFadeOut);
    }
}
