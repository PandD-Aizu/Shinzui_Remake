using System;
using System.Collections.Generic;
using Shinzui.Application.SpatialAudio;
using UnityEngine;

namespace Shinzui.Infrastructure.TunnelAcoustics
{
    [CreateAssetMenu(menuName = "Shinzui/Audio/Acoustic Mesh Library")]
    public sealed class AcousticMeshLibrary : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public Mesh Mesh;
            public Vector3[] Vertices;
            public int[] Triangles;
            public int OriginalVertexCount;
        }
        public Entry[] Entries = Array.Empty<Entry>();
        Dictionary<Mesh, Entry> lookup;
        public Entry Resolve(Mesh mesh)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<Mesh, Entry>();
                foreach (var entry in Entries) if (entry.Mesh) lookup.Add(entry.Mesh, entry);
            }
            if (lookup.TryGetValue(mesh, out var found)) return found;
            if (mesh && mesh.isReadable) return new Entry { Mesh = mesh, Vertices = mesh.vertices, Triangles = mesh.triangles };
            throw new InvalidOperationException("Missing acoustic mesh export: " + (mesh ? mesh.name : "null"));
        }
    }

    public static class AcousticGeometryCollector
    {
        static readonly Vector3[] UnitBox = { new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),
            new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1) };
        static readonly int[] BoxIndices = {0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,0,1,5,0,5,4,3,7,6,3,6,2};
        public static AcousticMeshData Collect(Transform root, AcousticMeshLibrary library, bool includeMoving = false, bool localSpace = false)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            var vertices = new List<AudioVector>(); var indices = new List<int>(); var surfaces = new List<AcousticSurfaceKind>();
            foreach (var collider in root.GetComponentsInChildren<Collider>(false))
            {
                if (!collider.enabled || collider.isTrigger) continue;
                var renderer = collider.GetComponent<Renderer>();
                if (!renderer || !renderer.enabled) continue; // Navigation floors and warp backstops are not acoustic walls.
                var profile = collider.GetComponentInParent<AcousticSurface>();
                if (profile && profile.Ignore) continue;
                if (!includeMoving && collider.GetComponentInParent<MovingAcousticGeometry>()) continue;
                var kind = profile ? profile.Kind : AcousticSurfaceKind.Concrete;
                var matrix = localSpace ? root.worldToLocalMatrix * collider.transform.localToWorldMatrix : collider.transform.localToWorldMatrix;
                if (collider is BoxCollider box)
                {
                    var points = new Vector3[8];
                    for (int i = 0; i < points.Length; i++) points[i] = box.center + Vector3.Scale(UnitBox[i], box.size * .5f);
                    Append(points, BoxIndices, matrix, kind, vertices, indices, surfaces);
                }
                else if (collider is MeshCollider mesh && mesh.sharedMesh)
                {
                    var entry = library.Resolve(mesh.sharedMesh);
                    Append(entry.Vertices, entry.Triangles, matrix, kind, vertices, indices, surfaces);
                }
                else throw new InvalidOperationException("Unsupported acoustic collider: " + collider.name + " / " + collider.GetType().Name);
            }
            return new AcousticMeshData { Vertices = vertices.ToArray(), Triangles = indices.ToArray(), Surfaces = surfaces.ToArray() };
        }
        static void Append(Vector3[] points, int[] triangles, Matrix4x4 matrix, AcousticSurfaceKind kind,
            List<AudioVector> vertices, List<int> indices, List<AcousticSurfaceKind> surfaces)
        {
            int offset = vertices.Count;
            foreach (var point in points) { var p = matrix.MultiplyPoint3x4(point); vertices.Add(new AudioVector(p.x,p.y,p.z)); }
            foreach (int index in triangles) indices.Add(offset+index);
            for (int i = 0; i < triangles.Length/3; i++) surfaces.Add(kind);
        }
    }
}
