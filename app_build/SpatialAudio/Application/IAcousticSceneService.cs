using System;

namespace Shinzui.Application.SpatialAudio
{
    public enum AcousticSurfaceKind { Concrete, Metal, Wood }
    public sealed class AcousticMeshData
    {
        public AudioVector[] Vertices = Array.Empty<AudioVector>();
        public int[] Triangles = Array.Empty<int>();
        public AcousticSurfaceKind[] Surfaces = Array.Empty<AcousticSurfaceKind>();
    }
    public interface IAcousticSceneService : IDisposable
    {
        int Revision { get; }
        int TriangleCount { get; }
        void Replace(AcousticMeshData geometry);
        void Clear();
    }
}
