using System;

namespace Shinzui.Application.SpatialAudio
{
    public readonly struct AudioVector
    {
        public readonly float X, Y, Z;
        public AudioVector(float x, float y, float z) { X = x; Y = y; Z = z; }
        public bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public float LengthSquared => X * X + Y * Y + Z * Z;
    }

    public readonly struct AudioPose
    {
        public readonly AudioVector Position, Forward, Up, Velocity;
        public AudioPose(AudioVector position, AudioVector forward, AudioVector up, AudioVector velocity = default)
        { Position = position; Forward = forward; Up = up; Velocity = velocity; }
        public static AudioPose At(float x, float y, float z) =>
            new AudioPose(new AudioVector(x, y, z), new AudioVector(0, 0, 1), new AudioVector(0, 1, 0));
        public bool IsValid
        {
            get
            {
                if (!Position.IsFinite || !Forward.IsFinite || !Up.IsFinite || !Velocity.IsFinite) return false;
                float crossX = Forward.Y * Up.Z - Forward.Z * Up.Y;
                float crossY = Forward.Z * Up.X - Forward.X * Up.Z;
                float crossZ = Forward.X * Up.Y - Forward.Y * Up.X;
                float crossLength = crossX * crossX + crossY * crossY + crossZ * crossZ;
                return Forward.LengthSquared > .0001f && Up.LengthSquared > .0001f &&
                    !float.IsInfinity(Forward.LengthSquared) && !float.IsInfinity(Up.LengthSquared) &&
                    crossLength > .0001f && !float.IsInfinity(crossLength);
            }
        }
    }

    public readonly struct SpatialVoiceHandle : IEquatable<SpatialVoiceHandle>
    {
        public readonly Guid Value;
        public SpatialVoiceHandle(Guid value) { Value = value; }
        public bool IsValid => Value != Guid.Empty;
        public bool Equals(SpatialVoiceHandle other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SpatialVoiceHandle other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
    }

    public enum SpatialAudioCategory { World, Footstep, Impact, Voice, Ambient }
    public enum SpatialVoiceState { Stopped, Preparing, Playing, Paused, Stopping, Ready }
    public enum SpatialAudioPlayStatus { Accepted, InvalidRequest, UnknownEvent, VoiceLimit, BackendUnavailable, Disposed }
    public enum SpatialAudioStopMode { AllowFadeOut, Immediate }

    public readonly struct SpatialAudioRequest
    {
        public readonly string EventId;
        public readonly AudioPose Pose;
        public readonly SpatialAudioCategory Category;
        /// <summary>0..255; higher values can replace lower-priority voices when the pool is full.</summary>
        public readonly int Priority;
        public readonly float Volume;
        public SpatialAudioRequest(string eventId, AudioPose pose, SpatialAudioCategory category = SpatialAudioCategory.World,
            int priority = 128, float volume = 1)
        { EventId = eventId; Pose = pose; Category = category; Priority = priority; Volume = volume; }
        public bool IsValid => !string.IsNullOrWhiteSpace(EventId) && Pose.IsValid && Priority >= 0 && Priority <= 255 &&
            Volume >= 0 && Volume <= 1 && Enum.IsDefined(typeof(SpatialAudioCategory), Category);
    }

    public readonly struct SpatialAudioPlayResult
    {
        public readonly SpatialAudioPlayStatus Status;
        public readonly SpatialVoiceHandle Handle;
        public bool Accepted => Status == SpatialAudioPlayStatus.Accepted;
        public SpatialAudioPlayResult(SpatialAudioPlayStatus status, SpatialVoiceHandle handle = default)
        { Status = status; Handle = handle; }
    }

    /// <summary>Return false when the tracked object no longer exists. Called on the main thread.</summary>
    public interface IAudioPoseSource { bool TryGetPose(out AudioPose pose); }
}
