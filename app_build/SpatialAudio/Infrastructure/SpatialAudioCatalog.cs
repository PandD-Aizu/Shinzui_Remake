using System;
using FMODUnity;
using UnityEngine;

namespace Shinzui.Infrastructure.SpatialAudio
{
    [CreateAssetMenu(menuName = "Shinzui/Audio/Spatial Audio Catalog")]
    public sealed class SpatialAudioCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Id;
            public EventReference Event;
            [Tooltip("The event must contain Steam Audio Spatializer and sufficient authored silence/release for its reverb tail.")]
            public string AuthoringNotes;
        }
        public Entry[] Entries = Array.Empty<Entry>();
    }
}
