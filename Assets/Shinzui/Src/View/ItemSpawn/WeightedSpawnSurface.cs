using System;
using UnityEngine;

namespace Shinzui.View.ItemSpawn
{
    /// <summary>
    /// 明示的に有効化されたメッシュにおける、ランタイム読み取り可能な三角形ごとの生成重み
    /// このコンポーネントによって、ソースメッシュが変更されることはない
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class WeightedSpawnSurface : MonoBehaviour
    {
        [SerializeField] private Mesh sourceMesh;
        [SerializeField] private float[] triangleWeights = Array.Empty<float>();
        [SerializeField, Min(0f)] private float regionWeight = 1f;
        [SerializeField] private int sourceVertexCount;
        [SerializeField] private int sourceTriangleIndexCount;
        [SerializeField] private uint sourceTopologyHash;

        public Mesh SourceMesh => sourceMesh;
        public float RegionWeight => regionWeight;
        public int TriangleCount => triangleWeights?.Length ?? 0;
        public bool IsDataValid => IsDataValidFor(GetCurrentMesh());

        public float GetWeight(int triangleIndex)
        {
            return IsValidTriangleIndex(triangleIndex) ? triangleWeights[triangleIndex] : 0f;
        }

        public bool TryGetTriangleData(
            int triangleIndex,
            out Vector3 a,
            out Vector3 b,
            out Vector3 c,
            out float weight)
        {
            a = b = c = default;
            weight = 0f;
            if (!IsDataValid || !IsValidTriangleIndex(triangleIndex)) return false;

            int offset = triangleIndex * 3;
            int[] triangles = sourceMesh.triangles;
            a = transform.TransformPoint(sourceMesh.vertices[triangles[offset]]);
            b = transform.TransformPoint(sourceMesh.vertices[triangles[offset + 1]]);
            c = transform.TransformPoint(sourceMesh.vertices[triangles[offset + 2]]);
            weight = triangleWeights[triangleIndex];
            return true;
        }

        public float[] CopyWeights()
        {
            return triangleWeights == null ? Array.Empty<float>() : (float[])triangleWeights.Clone();
        }

        public void SetRegionWeight(float value) => regionWeight = Mathf.Max(0f, value);

        public void InitializeFromMesh(Mesh mesh)
        {
            sourceMesh = mesh;
            int triangleCount = mesh == null ? 0 : mesh.triangles.Length / 3;
            triangleWeights = new float[triangleCount];
            CaptureMeshSignature(mesh);
        }

        public void SetWeight(int triangleIndex, float value)
        {
            if (IsValidTriangleIndex(triangleIndex)) triangleWeights[triangleIndex] = Mathf.Max(0f, value);
        }

        public void SetAllWeights(float value)
        {
            if (triangleWeights == null) return;
            value = Mathf.Max(0f, value);
            for (int i = 0; i < triangleWeights.Length; i++) triangleWeights[i] = value;
        }

        public Mesh GetCurrentMesh()
        {
            var filter = GetComponent<MeshFilter>();
            return filter == null ? null : filter.sharedMesh;
        }

        public bool IsDataValidFor(Mesh mesh)
        {
            return mesh != null && sourceMesh == mesh &&
                   sourceVertexCount == mesh.vertexCount &&
                   sourceTriangleIndexCount == mesh.triangles.Length &&
                   sourceTopologyHash == CalculateTopologyHash(mesh) &&
                   triangleWeights != null && triangleWeights.Length == mesh.triangles.Length / 3;
        }

        public static uint CalculateTopologyHash(Mesh mesh)
        {
            if (mesh == null) return 0;
            unchecked
            {
                uint hash = 2166136261u;
                void Add(int value)
                {
                    hash ^= (uint)value;
                    hash *= 16777619u;
                }

                Add(mesh.vertexCount);
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Add(vertices[i].GetHashCode());
                }

                int[] triangles = mesh.triangles;
                Add(triangles.Length);
                for (int i = 0; i < triangles.Length; i++) Add(triangles[i]);
                return hash;
            }
        }

        private bool IsValidTriangleIndex(int index) => triangleWeights != null && index >= 0 && index < triangleWeights.Length;

        private void CaptureMeshSignature(Mesh mesh)
        {
            sourceVertexCount = mesh == null ? 0 : mesh.vertexCount;
            sourceTriangleIndexCount = mesh == null ? 0 : mesh.triangles.Length;
            sourceTopologyHash = CalculateTopologyHash(mesh);
        }

        private void OnValidate()
        {
            if (sourceMesh == null) sourceMesh = GetCurrentMesh();
            if (triangleWeights == null) triangleWeights = Array.Empty<float>();
        }
    }
}
