using UnityEngine;

namespace Shinzui.Infrastructure.SpatialAudio
{
    public sealed class SpatialAudioRuntimeOptions
    {
        public SpatialAudioCatalog Catalog;
        public Transform Owner;
        public int MaxVoices = 8;
        public SpatialPropagationSettings Propagation;
        // Wait for source registration and a reflection simulation before starting the timeline.
        public float PreparationSeconds = .25f;
        public float PreparationTimeoutSeconds = 5;
        public float StopTimeoutSeconds = 15;
    }
}
