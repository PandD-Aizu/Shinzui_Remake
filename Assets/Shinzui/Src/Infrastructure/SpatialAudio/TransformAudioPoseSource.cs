using Shinzui.Application.SpatialAudio;
using UnityEngine;

namespace Shinzui.Infrastructure.SpatialAudio
{
    /// <summary>Unity object tracking stays outside Application. Large jumps reset velocity.</summary>
    public sealed class TransformAudioPoseSource : IAudioPoseSource
    {
        readonly Transform target;
        readonly bool calculateVelocity;
        readonly float teleportDistance;
        Vector3 previous;
        double previousTime;
        bool sampled;
        public TransformAudioPoseSource(Transform target, bool calculateVelocity = false, float teleportDistance = 10)
        { this.target = target; this.calculateVelocity = calculateVelocity; this.teleportDistance = Mathf.Max(.01f, teleportDistance); }
        public bool TryGetPose(out AudioPose pose)
        {
            pose = default;
            if (!target) return false;
            var position = target.position;
            double now = Time.realtimeSinceStartupAsDouble;
            var delta = position - previous;
            var velocity = Vector3.zero;
            if (calculateVelocity && sampled && now > previousTime && delta.sqrMagnitude < teleportDistance * teleportDistance)
                velocity = delta / (float)(now - previousTime);
            previous = position; previousTime = now; sampled = true;
            pose = new AudioPose(Convert(position), Convert(target.forward), Convert(target.up), Convert(velocity));
            return pose.IsValid;
        }
        internal static AudioVector Convert(Vector3 value) => new AudioVector(value.x, value.y, value.z);
    }
}
