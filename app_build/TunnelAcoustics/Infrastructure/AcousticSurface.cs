using Shinzui.Application.SpatialAudio;
using UnityEngine;

namespace Shinzui.Infrastructure.TunnelAcoustics
{
    public sealed class AcousticSurface : MonoBehaviour
    {
        public AcousticSurfaceKind Kind = AcousticSurfaceKind.Concrete;
        public bool Ignore;
    }
}
