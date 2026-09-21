using System;
using UnityEngine;

namespace Shinzui.Infrastructure.CustomSpatialAudio
{
    [CreateAssetMenu(menuName = "Shinzui/Audio/Custom Spatial Audio Configuration")]
    public sealed class CustomSpatialAudioConfiguration : ScriptableObject
    {
        [Serializable]
        public sealed class DrySound
        {
            public string Id;
            [Tooltip("Dry mono file, relative to StreamingAssets. No authored spatializer or reverb.")]
            public string Path;
            public bool Loop;
        }
        public string BusPath = "bus:/WorldSE";
        public string FootstepEventId = "footstep.prototype";
        [Range(4, 16)] public int MaxVoices = 8;
        public bool Binaural = true;
        [Range(5, 30)] public int UpdatesPerSecond = 20;
        [Range(0, 1)] public float EarlyGain = .7f;
        [Range(0, 1)] public float LateGain = .2f;
        public DrySound[] Sounds = Array.Empty<DrySound>();
    }
}
