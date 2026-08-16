using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Shinzui.View.ItemSpawn
{
    /// <summary>
    /// サーフェスメッシュのバウンディングボックスに対する2D Weight Mapの投影平面（View層定義）。
    /// </summary>
    public enum SpawnSurfaceProjection
    {
        XZ = 0,
        XY = 1,
        YZ = 2
    }

    /// <summary>
    /// アイテム生成専用のサーフェスメッシュと、メッシュトポロジーに独立した2D Weight Mapを保持するViewコンポーネント。
    /// ワールド/ローカル座標からバイリニアサンプリングによる重み取得、解像度リサイズ、旧Triangle Weight移行に対応する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeightedSpawnSurface : MonoBehaviour
    {
        public const int DefaultResolution = 256;

        [Tooltip("アイテムスポーン専用のサーフェスメッシュ。描画用MeshFilter/MeshRendererとは独立して設定・管理されます。")]
        [FormerlySerializedAs("sourceMesh")]
        [SerializeField] private Mesh surfaceMesh;

        [Tooltip("2D Weight Mapの解像度（128/256/512等）。")]
        [SerializeField] private int mapResolution = DefaultResolution;

        [Tooltip("2D Weight Mapの投影平面（XZ: 水平/床, XY: 垂直/Z軸薄, YZ: 垂直/X軸薄）。")]
        [SerializeField] private SpawnSurfaceProjection projectionPlane = SpawnSurfaceProjection.XZ;

        [Tooltip("2D Weight Mapのテクセル配列（サイズ: mapResolution * mapResolution、値域: 0..1）。")]
        [SerializeField] private float[] weightMap = Array.Empty<float>();

        [SerializeField] private float[] triangleWeights = Array.Empty<float>();
        [SerializeField, Min(0f)] private float regionWeight = 1f;
        [SerializeField] private Bounds localBounds;
        [SerializeField] private int sourceVertexCount;
        [SerializeField] private int sourceTriangleIndexCount;
        [SerializeField] private uint sourceTopologyHash;

        /// <summary>アイテムスポーン専用サーフェスメッシュ</summary>
        public Mesh SurfaceMesh => surfaceMesh;

        /// <summary>後方互換用プロパティ（SurfaceMeshと同等）</summary>
        public Mesh SourceMesh => surfaceMesh;

        public float RegionWeight => regionWeight;
        public SpawnSurfaceProjection ProjectionPlane => projectionPlane;
        public int MapResolution => mapResolution > 0 ? mapResolution : DefaultResolution;
        public int TriangleCount => surfaceMesh != null ? surfaceMesh.triangles.Length / 3 : (triangleWeights?.Length ?? 0);
        public Bounds LocalBounds => surfaceMesh != null ? surfaceMesh.bounds : localBounds;
        public bool IsDataValid => IsDataValidFor(surfaceMesh);

        public void SetProjectionPlane(SpawnSurfaceProjection plane) => projectionPlane = plane;

        public static SpawnSurfaceProjection DetermineProjectionPlane(Bounds bounds)
        {
            Vector3 size = bounds.size;
            if (size.y <= size.x && size.y <= size.z) return SpawnSurfaceProjection.XZ;
            if (size.z < size.x && size.z < size.y) return SpawnSurfaceProjection.XY;
            if (size.x < size.y && size.x < size.z) return SpawnSurfaceProjection.YZ;
            return SpawnSurfaceProjection.XZ;
        }

        public float[] CopyWeightMap()
        {
            return weightMap == null ? Array.Empty<float>() : (float[])weightMap.Clone();
        }

        public void SetWeightMap(float[] map, int resolution)
        {
            mapResolution = Math.Max(1, resolution);
            if (map != null && map.Length == mapResolution * mapResolution)
            {
                weightMap = (float[])map.Clone();
            }
            else
            {
                InitializeWeightMap(mapResolution, 1f);
            }
        }

        public void InitializeWeightMap(int resolution = DefaultResolution, float initialWeight = 1f)
        {
            mapResolution = Math.Max(1, resolution);
            weightMap = new float[mapResolution * mapResolution];
            float clamped = Mathf.Clamp01(initialWeight);
            for (int i = 0; i < weightMap.Length; i++)
            {
                weightMap[i] = clamped;
            }
        }

        /// <summary>
        /// 解像度を変更する。既存のマップをバイリニア補間でリサンプリングしてデータを極力保持する。
        /// </summary>
        public void ResizeMap(int newResolution)
        {
            newResolution = Math.Max(1, newResolution);
            if (mapResolution == newResolution && weightMap != null && weightMap.Length == newResolution * newResolution)
            {
                return;
            }

            if (weightMap == null || weightMap.Length == 0)
            {
                InitializeWeightMap(newResolution, 1f);
                return;
            }

            float[] newMap = new float[newResolution * newResolution];
            if (newResolution == 1)
            {
                newMap[0] = GetWeight(0.5f, 0.5f);
            }
            else
            {
                for (int y = 0; y < newResolution; y++)
                {
                    float v = (float)y / (newResolution - 1);
                    for (int x = 0; x < newResolution; x++)
                    {
                        float u = (float)x / (newResolution - 1);
                        newMap[y * newResolution + x] = GetWeight(u, v);
                    }
                }
            }

            weightMap = newMap;
            mapResolution = newResolution;
        }

        /// <summary>
        /// 正規化座標 (u, v) in [0, 1] におけるWeightをバイリニア補間で取得する。領域外は 0 を返す。
        /// </summary>
        public float GetWeight(float u, float v)
        {
            if (u < 0f || u > 1f || v < 0f || v > 1f || weightMap == null || weightMap.Length == 0)
            {
                return 0f;
            }

            int n = MapResolution;
            if (weightMap.Length != n * n)
            {
                return 0f;
            }

            if (n == 1)
            {
                return Mathf.Clamp01(weightMap[0]);
            }

            float px = u * (n - 1);
            float py = v * (n - 1);

            int x0 = Mathf.FloorToInt(px);
            int y0 = Mathf.FloorToInt(py);
            int x1 = Mathf.Min(x0 + 1, n - 1);
            int y1 = Mathf.Min(y0 + 1, n - 1);

            float fx = px - x0;
            float fy = py - y0;

            float w00 = weightMap[y0 * n + x0];
            float w10 = weightMap[y0 * n + x1];
            float w01 = weightMap[y1 * n + x0];
            float w11 = weightMap[y1 * n + x1];

            float top = Mathf.Lerp(w00, w10, fx);
            float bottom = Mathf.Lerp(w01, w11, fx);
            float result = Mathf.Lerp(top, bottom, fy);

            return Mathf.Clamp01(result);
        }

        /// <summary>
        /// ワールド座標におけるWeightをバイリニア補間で取得する。
        /// </summary>
        public float GetWeightAtWorldPosition(Vector3 worldPos)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            return GetWeightAtLocalPosition(localPos);
        }

        /// <summary>
        /// サーフェスローカル座標におけるWeightをバイリニア補間で取得する。
        /// </summary>
        public float GetWeightAtLocalPosition(Vector3 localPos)
        {
            if (LocalToNormalizedMapCoords(localPos, out float u, out float v))
            {
                return GetWeight(u, v);
            }
            return 0f;
        }

        /// <summary>
        /// ワールド座標を正規化Map座標 (u, v) in [0, 1] に変換する。
        /// </summary>
        public bool WorldToNormalizedMapCoords(Vector3 worldPos, out float u, out float v)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            return LocalToNormalizedMapCoords(localPos, out u, out v);
        }

        /// <summary>
        /// サーフェスローカル座標を正規化Map座標 (u, v) in [0, 1] に変換する。
        /// </summary>
        public bool LocalToNormalizedMapCoords(Vector3 localPos, out float u, out float v)
        {
            Bounds b = LocalBounds;
            float sizeX = Mathf.Max(b.size.x, 1e-4f);
            float sizeY = Mathf.Max(b.size.y, 1e-4f);
            float sizeZ = Mathf.Max(b.size.z, 1e-4f);

            switch (projectionPlane)
            {
                case SpawnSurfaceProjection.XY:
                    u = (localPos.x - b.min.x) / sizeX;
                    v = (localPos.y - b.min.y) / sizeY;
                    break;

                case SpawnSurfaceProjection.YZ:
                    u = (localPos.y - b.min.y) / sizeY;
                    v = (localPos.z - b.min.z) / sizeZ;
                    break;

                case SpawnSurfaceProjection.XZ:
                default:
                    u = (localPos.x - b.min.x) / sizeX;
                    v = (localPos.z - b.min.z) / sizeZ;
                    break;
            }

            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        /// <summary>
        /// 正規化Map座標 (u, v) をサーフェスローカル座標に変換する。
        /// </summary>
        public Vector3 NormalizedMapCoordsToLocal(float u, float v)
        {
            Bounds b = LocalBounds;
            switch (projectionPlane)
            {
                case SpawnSurfaceProjection.XY:
                    return new Vector3(b.min.x + u * b.size.x, b.min.y + v * b.size.y, b.center.z);

                case SpawnSurfaceProjection.YZ:
                    return new Vector3(b.center.x, b.min.y + u * b.size.y, b.min.z + v * b.size.z);

                case SpawnSurfaceProjection.XZ:
                default:
                    return new Vector3(b.min.x + u * b.size.x, b.center.y, b.min.z + v * b.size.z);
            }
        }

        /// <summary>
        /// 正規化Map座標 (u, v) をワールド座標に変換する。
        /// </summary>
        public Vector3 NormalizedMapCoordsToWorld(float u, float v)
        {
            return transform.TransformPoint(NormalizedMapCoordsToLocal(u, v));
        }

        public void SetWeightAtNormalizedCoords(float u, float v, float value)
        {
            if (u < 0f || u > 1f || v < 0f || v > 1f || weightMap == null) return;
            int n = MapResolution;
            if (weightMap.Length != n * n) return;

            int x = Mathf.Clamp(Mathf.RoundToInt(u * (n - 1)), 0, n - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(v * (n - 1)), 0, n - 1);
            weightMap[y * n + x] = Mathf.Clamp01(value);
        }

        /// <summary>後方互換用: 三角形インデックス指定での重み取得（重心でのsampled weightまたは旧配列）</summary>
        public float GetWeight(int triangleIndex)
        {
            if (TryGetTriangleData(triangleIndex, out _, out _, out _, out float weight))
            {
                return weight;
            }
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
            if (!IsDataValid || surfaceMesh == null) return false;

            int[] triangles = surfaceMesh.triangles;
            int offset = triangleIndex * 3;
            if (offset < 0 || offset + 2 >= triangles.Length) return false;

            Vector3[] vertices = surfaceMesh.vertices;
            int idxA = triangles[offset];
            int idxB = triangles[offset + 1];
            int idxC = triangles[offset + 2];

            if (idxA >= vertices.Length || idxB >= vertices.Length || idxC >= vertices.Length) return false;

            a = transform.TransformPoint(vertices[idxA]);
            b = transform.TransformPoint(vertices[idxB]);
            c = transform.TransformPoint(vertices[idxC]);

            // Weight Mapサンプリング（重心位置）
            Vector3 centroid = (a + b + c) / 3f;
            weight = GetWeightAtWorldPosition(centroid);

            // もしWeightMapが未設定でtriangleWeightsがある場合はフォールバック
            if (weightMap == null || weightMap.Length == 0)
            {
                if (IsValidTriangleIndex(triangleIndex))
                {
                    weight = triangleWeights[triangleIndex];
                }
            }

            return true;
        }

        public float[] CopyWeights()
        {
            return triangleWeights == null ? Array.Empty<float>() : (float[])triangleWeights.Clone();
        }

        public void SetRegionWeight(float value) => regionWeight = Mathf.Max(0f, value);

        public void InitializeFromMesh(Mesh mesh)
        {
            surfaceMesh = mesh;
            int triangleCount = mesh == null ? 0 : mesh.triangles.Length / 3;
            triangleWeights = new float[triangleCount];
            if (mapResolution <= 0) mapResolution = DefaultResolution;
            if (mesh != null)
            {
                projectionPlane = DetermineProjectionPlane(mesh.bounds);
            }
            InitializeWeightMap(mapResolution, 1f);

            if (triangleWeights.Length > 0)
            {
                for (int i = 0; i < triangleWeights.Length; i++) triangleWeights[i] = 1f;
            }

            CaptureMeshSignature(mesh);
        }

        public void SetWeight(int triangleIndex, float value)
        {
            float clamped = Mathf.Max(0f, value);
            if (IsValidTriangleIndex(triangleIndex))
            {
                triangleWeights[triangleIndex] = clamped;
            }

            if (TryGetTriangleData(triangleIndex, out _, out _, out _, out _))
            {
                if (surfaceMesh != null)
                {
                    int[] triangles = surfaceMesh.triangles;
                    int offset = triangleIndex * 3;
                    if (offset + 2 < triangles.Length)
                    {
                        Vector3[] vertices = surfaceMesh.vertices;
                        Vector3 a = vertices[triangles[offset]];
                        Vector3 b = vertices[triangles[offset + 1]];
                        Vector3 c = vertices[triangles[offset + 2]];
                        Vector3 localCentroid = (a + b + c) / 3f;
                        if (LocalToNormalizedMapCoords(localCentroid, out float u, out float v))
                        {
                            SetWeightAtNormalizedCoords(u, v, clamped);
                        }
                    }
                }
            }
        }

        public void SetAllWeights(float value)
        {
            float clamped = Mathf.Clamp01(Mathf.Max(0f, value));
            if (weightMap != null)
            {
                for (int i = 0; i < weightMap.Length; i++) weightMap[i] = clamped;
            }

            if (triangleWeights != null)
            {
                for (int i = 0; i < triangleWeights.Length; i++) triangleWeights[i] = clamped;
            }
        }

        public void SetSurfaceMeshDirectly(Mesh mesh)
        {
            surfaceMesh = mesh;
        }

        /// <summary>
        /// 旧triangleWeightsのデータを2D Weight Mapへ面ラスタライズ焼き付けする。
        /// 複数三角形が重なるテクセルはMax値を採用する。
        /// </summary>
        public void MigrateFromLegacyTriangleWeights()
        {
            if (surfaceMesh == null || triangleWeights == null || triangleWeights.Length == 0) return;
            int n = MapResolution;
            if (weightMap == null || weightMap.Length != n * n)
            {
                InitializeWeightMap(n, 0f);
            }
            else
            {
                for (int i = 0; i < weightMap.Length; i++) weightMap[i] = 0f;
            }

            int[] triangles = surfaceMesh.triangles;
            Vector3[] vertices = surfaceMesh.vertices;
            int triCount = Math.Min(triangleWeights.Length, triangles.Length / 3);

            for (int t = 0; t < triCount; t++)
            {
                float w = Mathf.Clamp01(triangleWeights[t]);
                if (w <= 0f) continue;

                Vector3 a = vertices[triangles[t * 3]];
                Vector3 b = vertices[triangles[t * 3 + 1]];
                Vector3 c = vertices[triangles[t * 3 + 2]];

                if (!LocalToNormalizedMapCoords(a, out float ua, out float va) ||
                    !LocalToNormalizedMapCoords(b, out float ub, out float vb) ||
                    !LocalToNormalizedMapCoords(c, out float uc, out float vc))
                {
                    // 境界近傍フォールバック
                    LocalToNormalizedMapCoords(a, out ua, out va);
                    LocalToNormalizedMapCoords(b, out ub, out vb);
                    LocalToNormalizedMapCoords(c, out uc, out vc);
                }

                Vector2 p0 = new Vector2(ua, va);
                Vector2 p1 = new Vector2(ub, vb);
                Vector2 p2 = new Vector2(uc, vc);

                float minU = Mathf.Clamp01(Mathf.Min(p0.x, p1.x, p2.x));
                float maxU = Mathf.Clamp01(Mathf.Max(p0.x, p1.x, p2.x));
                float minV = Mathf.Clamp01(Mathf.Min(p0.y, p1.y, p2.y));
                float maxV = Mathf.Clamp01(Mathf.Max(p0.y, p1.y, p2.y));

                int x0 = Mathf.Clamp(Mathf.FloorToInt(minU * (n - 1)), 0, n - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt(maxU * (n - 1)), 0, n - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(minV * (n - 1)), 0, n - 1);
                int y1 = Mathf.Clamp(Mathf.CeilToInt(maxV * (n - 1)), 0, n - 1);

                for (int y = y0; y <= y1; y++)
                {
                    float tv = (float)y / (n - 1);
                    for (int x = x0; x <= x1; x++)
                    {
                        float tu = (float)x / (n - 1);
                        Vector2 pt = new Vector2(tu, tv);

                        if (IsPointInTriangle2D(pt, p0, p1, p2))
                        {
                            int idx = y * n + x;
                            weightMap[idx] = Mathf.Max(weightMap[idx], w);
                        }
                    }
                }
            }
        }

        private static bool IsPointInTriangle2D(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign2D(p, a, b);
            float d2 = Sign2D(p, b, c);
            float d3 = Sign2D(p, c, a);

            bool hasNeg = (d1 < -1e-5f) || (d2 < -1e-5f) || (d3 < -1e-5f);
            bool hasPos = (d1 > 1e-5f) || (d2 > 1e-5f) || (d3 > 1e-5f);

            return !(hasNeg && hasPos);
        }

        private static float Sign2D(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        public bool IsDataValidFor(Mesh mesh)
        {
            return mesh != null && surfaceMesh == mesh &&
                   sourceVertexCount == mesh.vertexCount &&
                   sourceTriangleIndexCount == mesh.triangles.Length &&
                   sourceTopologyHash == CalculateTopologyHash(mesh) &&
                   ((weightMap != null && weightMap.Length == MapResolution * MapResolution) ||
                    (triangleWeights != null && triangleWeights.Length == mesh.triangles.Length / 3));
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

        public void CaptureMeshSignature(Mesh mesh)
        {
            surfaceMesh = mesh;
            sourceVertexCount = mesh == null ? 0 : mesh.vertexCount;
            sourceTriangleIndexCount = mesh == null ? 0 : mesh.triangles.Length;
            sourceTopologyHash = CalculateTopologyHash(mesh);
            localBounds = mesh != null ? mesh.bounds : default;
        }

        private void OnValidate()
        {
            if (mapResolution <= 0) mapResolution = DefaultResolution;
            if (weightMap == null || (weightMap.Length == 0 && surfaceMesh != null))
            {
                InitializeWeightMap(mapResolution, 1f);
            }
            if (triangleWeights == null) triangleWeights = Array.Empty<float>();
            if (surfaceMesh != null && localBounds.size == Vector3.zero)
            {
                localBounds = surfaceMesh.bounds;
                projectionPlane = DetermineProjectionPlane(localBounds);
            }
        }
    }
}

