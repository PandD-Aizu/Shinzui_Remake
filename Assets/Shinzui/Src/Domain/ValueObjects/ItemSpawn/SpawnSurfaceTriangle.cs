using System;
using UnityEngine;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// サーフェスメッシュの1つの三角形データ。
    /// ワールド座標系の頂点A, B, C、法線、面積、ペイントウェイト、リージョンウェイトを保持する。
    /// </summary>
    public readonly struct SpawnSurfaceTriangle : IEquatable<SpawnSurfaceTriangle>
    {
        public SpawnVector3 A { get; }
        public SpawnVector3 B { get; }
        public SpawnVector3 C { get; }

        public Vector3 VertexA => new(A.X, A.Y, A.Z);
        public Vector3 VertexB => new(B.X, B.Y, B.Z);
        public Vector3 VertexC => new(C.X, C.Y, C.Z);

        public SpawnVector3 Normal { get; }
        public float Area { get; }
        public float PaintWeight { get; }
        public float RegionWeight { get; }
        public string SurfaceId { get; }
        public string SurfaceName { get; }
        public int TriangleIndex { get; }

        public float TotalWeight
        {
            get
            {
                if (Area <= 1e-6f || PaintWeight <= 0f || RegionWeight <= 0f)
                {
                    return 0f;
                }
                return Area * PaintWeight * RegionWeight;
            }
        }

        public float EffectiveWeight => TotalWeight;

        public bool IsValid => TotalWeight > 0f;

        public SpawnSurfaceTriangle(
            SpawnVector3 a,
            SpawnVector3 b,
            SpawnVector3 c,
            float paintWeight,
            float regionWeight,
            string surfaceId,
            int triangleIndex)
        {
            A = a;
            B = b;
            C = c;
            Area = SpawnVector3.CalculateTriangleArea(a, b, c);
            Normal = SpawnVector3.CalculateTriangleNormal(a, b, c);
            PaintWeight = Math.Max(0f, paintWeight);
            RegionWeight = Math.Max(0f, regionWeight);
            SurfaceId = surfaceId ?? string.Empty;
            SurfaceName = surfaceId ?? string.Empty;
            TriangleIndex = triangleIndex;
        }

        public SpawnSurfaceTriangle(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            float paintWeight,
            float regionWeight,
            int surfaceId,
            string surfaceName,
            int triangleIndex)
        {
            A = a;
            B = b;
            C = c;
            Area = SpawnVector3.CalculateTriangleArea(A, B, C);
            Normal = SpawnVector3.CalculateTriangleNormal(A, B, C);
            PaintWeight = Math.Max(0f, paintWeight);
            RegionWeight = Math.Max(0f, regionWeight);
            SurfaceId = surfaceId.ToString();
            SurfaceName = surfaceName ?? surfaceId.ToString();
            TriangleIndex = triangleIndex;
        }

        public SpawnSurfaceTriangle(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            float paintWeight,
            float regionWeight,
            string surfaceId,
            string surfaceName,
            int triangleIndex)
        {
            A = a;
            B = b;
            C = c;
            Area = SpawnVector3.CalculateTriangleArea(A, B, C);
            Normal = SpawnVector3.CalculateTriangleNormal(A, B, C);
            PaintWeight = Math.Max(0f, paintWeight);
            RegionWeight = Math.Max(0f, regionWeight);
            SurfaceId = surfaceId ?? string.Empty;
            SurfaceName = surfaceName ?? surfaceId ?? string.Empty;
            TriangleIndex = triangleIndex;
        }

        /// <summary>
        /// 平方根サンプリング（Square Root Barycentric Sampling）による三角形内部の一様ランダムサンプリング
        /// </summary>
        public Vector3 SampleUniformPoint(float u1, float u2)
        {
            if (u1 <= 1e-6f) u1 = 1e-6f;
            if (u1 >= 1f) u1 = 0.999999f;

            float sqrtU1 = Mathf.Sqrt(u1);
            float u = 1f - sqrtU1;
            float v = u2 * sqrtU1;
            float w = 1f - u - v;

            return (u * (Vector3)A) + (v * (Vector3)B) + (w * (Vector3)C);
        }

        public bool Equals(SpawnSurfaceTriangle other) =>
            TriangleIndex == other.TriangleIndex &&
            SurfaceId == other.SurfaceId &&
            A.Equals(other.A) &&
            B.Equals(other.B) &&
            C.Equals(other.C);

        public override bool Equals(object obj) => obj is SpawnSurfaceTriangle other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SurfaceId, TriangleIndex, A, B, C);
    }
}
