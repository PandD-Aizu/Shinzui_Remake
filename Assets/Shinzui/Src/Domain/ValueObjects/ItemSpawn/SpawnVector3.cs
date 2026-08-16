using System;
using UnityEngine;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// UnityEngine非依存・相互変換可能な3次元ベクトル値オブジェクト。
    /// ドメイン層でのアイテム配置座標・法線・距離・三角形面積計算に使用する。
    /// </summary>
    public readonly struct SpawnVector3 : IEquatable<SpawnVector3>
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public SpawnVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static implicit operator Vector3(SpawnVector3 v) => new(v.X, v.Y, v.Z);
        public static implicit operator SpawnVector3(Vector3 v) => new(v.x, v.y, v.z);

        public static SpawnVector3 Zero => new(0f, 0f, 0f);
        public static SpawnVector3 One => new(1f, 1f, 1f);
        public static SpawnVector3 Up => new(0f, 1f, 0f);
        public static SpawnVector3 Forward => new(0f, 0f, 1f);
        public static SpawnVector3 Right => new(1f, 0f, 0f);

        public static SpawnVector3 operator +(SpawnVector3 a, SpawnVector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static SpawnVector3 operator -(SpawnVector3 a, SpawnVector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static SpawnVector3 operator *(SpawnVector3 a, float scalar) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);
        public static SpawnVector3 operator *(float scalar, SpawnVector3 a) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);
        public static SpawnVector3 operator /(SpawnVector3 a, float scalar) => new(a.X / scalar, a.Y / scalar, a.Z / scalar);
        public static SpawnVector3 operator -(SpawnVector3 a) => new(-a.X, -a.Y, -a.Z);

        public float SqrMagnitude => X * X + Y * Y + Z * Z;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        public SpawnVector3 Normalized
        {
            get
            {
                float mag = Magnitude;
                return mag > 1e-6f ? this / mag : Zero;
            }
        }

        public static float Dot(SpawnVector3 a, SpawnVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static SpawnVector3 Cross(SpawnVector3 a, SpawnVector3 b) =>
            new(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );

        public static float Distance(SpawnVector3 a, SpawnVector3 b) => (a - b).Magnitude;
        public static float SqrDistance(SpawnVector3 a, SpawnVector3 b) => (a - b).SqrMagnitude;

        public static SpawnVector3 Lerp(SpawnVector3 a, SpawnVector3 b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new SpawnVector3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t
            );
        }

        /// <summary>
        /// 頂点 A, B, C で構成される三角形のワールド面積を計算する
        /// </summary>
        public static float CalculateTriangleArea(SpawnVector3 a, SpawnVector3 b, SpawnVector3 c)
        {
            SpawnVector3 ab = b - a;
            SpawnVector3 ac = c - a;
            return 0.5f * Cross(ab, ac).Magnitude;
        }

        /// <summary>
        /// 頂点 A, B, C で構成される三角形の法線ベクトル（正規化済み）を計算する
        /// </summary>
        public static SpawnVector3 CalculateTriangleNormal(SpawnVector3 a, SpawnVector3 b, SpawnVector3 c)
        {
            SpawnVector3 ab = b - a;
            SpawnVector3 ac = c - a;
            return Cross(ab, ac).Normalized;
        }

        public bool Equals(SpawnVector3 other) =>
            Math.Abs(X - other.X) < 1e-6f &&
            Math.Abs(Y - other.Y) < 1e-6f &&
            Math.Abs(Z - other.Z) < 1e-6f;

        public override bool Equals(object obj) => obj is SpawnVector3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
    }
}
