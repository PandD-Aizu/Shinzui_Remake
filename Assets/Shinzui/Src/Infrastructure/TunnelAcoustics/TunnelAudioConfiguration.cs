using Shinzui.Infrastructure.SpatialAudio;
using UnityEngine;

namespace Shinzui.Infrastructure.TunnelAcoustics
{
    [CreateAssetMenu(menuName = "Shinzui/Audio/Tunnel Audio Configuration")]
    public sealed class TunnelAudioConfiguration : ScriptableObject
    {
        public AcousticMeshLibrary MeshLibrary;
        public SpatialAudioCatalog Events;
        public string FootstepEventId = "footstep.prototype";
        [Range(4,128)] public int MaxVoices = 12;
        public SpatialPropagationSettings Propagation = new SpatialPropagationSettings();
    }
}
