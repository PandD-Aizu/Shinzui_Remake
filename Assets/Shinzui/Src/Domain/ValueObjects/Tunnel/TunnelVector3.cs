using System;

namespace Shinzui.Domain.ValueObjects.Tunnel
{
    /// <summary>
    /// UnityEngine非依存の3Dベクトル値オブジェクト。
    /// ドメイン層でのトンネル座標・寸法・配置計算に使用する。
    /// </summary>
    public readonly struct TunnelVector3 : IEquatable<TunnelVector3>
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public TunnelVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static TunnelVector3 Zero => new(0.0f, 0.0f, 0.0f);
        public static TunnelVector3 One => new(1.0f, 1.0f, 1.0f);
        public static TunnelVector3 Up => new(0.0f, 1.0f, 0.0f);
        public static TunnelVector3 Forward => new(0.0f, 0.0f, 1.0f);

        public static TunnelVector3 operator +(TunnelVector3 a, TunnelVector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static TunnelVector3 operator -(TunnelVector3 a, TunnelVector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static TunnelVector3 operator *(TunnelVector3 a, float scalar) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);
        public static TunnelVector3 operator /(TunnelVector3 a, float scalar) => new(a.X / scalar, a.Y / scalar, a.Z / scalar);

        public float Magnitude => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        public TunnelVector3 Normalized
        {
            get
            {
                float mag = Magnitude;
                return mag > 1e-6f ? this / mag : Zero;
            }
        }

        public bool Equals(TunnelVector3 other) =>
            Math.Abs(X - other.X) < 1e-6f &&
            Math.Abs(Y - other.Y) < 1e-6f &&
            Math.Abs(Z - other.Z) < 1e-6f;

        public override bool Equals(object obj) => obj is TunnelVector3 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }
}
