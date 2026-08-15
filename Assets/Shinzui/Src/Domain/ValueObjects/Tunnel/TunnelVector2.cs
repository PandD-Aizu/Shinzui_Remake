using System;

namespace Shinzui.Domain.ValueObjects.Tunnel
{
    /// <summary>
    /// UnityEngine非依存の2Dベクトル値オブジェクト。
    /// ドメイン層でのトンネル配置・重複判定計算に使用する。
    /// </summary>
    public readonly struct TunnelVector2 : IEquatable<TunnelVector2>
    {
        public float X { get; }
        public float Y { get; }

        public TunnelVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static TunnelVector2 Zero => new(0.0f, 0.0f);
        public static TunnelVector2 One => new(1.0f, 1.0f);
        public static TunnelVector2 Left => new(-1.0f, 0.0f);
        public static TunnelVector2 Right => new(1.0f, 0.0f);

        public static TunnelVector2 operator +(TunnelVector2 a, TunnelVector2 b) => new(a.X + b.X, a.Y + b.Y);
        public static TunnelVector2 operator -(TunnelVector2 a, TunnelVector2 b) => new(a.X - b.X, a.Y - b.Y);
        public static TunnelVector2 operator *(TunnelVector2 a, float scalar) => new(a.X * scalar, a.Y * scalar);
        public static TunnelVector2 operator /(TunnelVector2 a, float scalar) => new(a.X / scalar, a.Y / scalar);

        public static float Dot(TunnelVector2 a, TunnelVector2 b) => a.X * b.X + a.Y * b.Y;

        public bool Equals(TunnelVector2 other) =>
            Math.Abs(X - other.X) < 1e-6f && Math.Abs(Y - other.Y) < 1e-6f;

        public override bool Equals(object obj) => obj is TunnelVector2 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"({X:F2}, {Y:F2})";
    }
}
