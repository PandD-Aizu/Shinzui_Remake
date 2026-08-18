using System;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// UnityEngine非依存・純粋なSystemのみで構成される4x4アフィン変換行列値オブジェクト（POCO）。
    /// ワールド座標からサーフェスローカル座標への正確な逆変換（位置・回転・非一様スケール・親階層対応）に使用する。
    /// </summary>
    public readonly struct SpawnMatrix4x4 : IEquatable<SpawnMatrix4x4>
    {
        public float M00 { get; }
        public float M01 { get; }
        public float M02 { get; }
        public float M03 { get; }

        public float M10 { get; }
        public float M11 { get; }
        public float M12 { get; }
        public float M13 { get; }

        public float M20 { get; }
        public float M21 { get; }
        public float M22 { get; }
        public float M23 { get; }

        public float M30 { get; }
        public float M31 { get; }
        public float M32 { get; }
        public float M33 { get; }

        public static SpawnMatrix4x4 Identity => new(
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f);

        public SpawnMatrix4x4(
            float m00, float m01, float m02, float m03,
            float m10, float m11, float m12, float m13,
            float m20, float m21, float m22, float m23,
            float m30, float m31, float m32, float m33)
        {
            M00 = m00; M01 = m01; M02 = m02; M03 = m03;
            M10 = m10; M11 = m11; M12 = m12; M13 = m13;
            M20 = m20; M21 = m21; M22 = m22; M23 = m23;
            M30 = m30; M31 = m31; M32 = m32; M33 = m33;
        }

        public static SpawnMatrix4x4 FromFloatArray(float[] values)
        {
            if (values == null || values.Length < 16) return Identity;
            return new SpawnMatrix4x4(
                values[0], values[1], values[2], values[3],
                values[4], values[5], values[6], values[7],
                values[8], values[9], values[10], values[11],
                values[12], values[13], values[14], values[15]);
        }

        public static SpawnMatrix4x4 CreateInverseTranslationScale(SpawnVector3 position, SpawnVector3 scale)
        {
            float sx = Math.Abs(scale.X) > 1e-6f ? 1f / scale.X : 1f;
            float sy = Math.Abs(scale.Y) > 1e-6f ? 1f / scale.Y : 1f;
            float sz = Math.Abs(scale.Z) > 1e-6f ? 1f / scale.Z : 1f;

            return new SpawnMatrix4x4(
                sx, 0f, 0f, -position.X * sx,
                0f, sy, 0f, -position.Y * sy,
                0f, 0f, sz, -position.Z * sz,
                0f, 0f, 0f, 1f);
        }

        public float[] ToFloatArray()
        {
            return new float[]
            {
                M00, M01, M02, M03,
                M10, M11, M12, M13,
                M20, M21, M22, M23,
                M30, M31, M32, M33
            };
        }

        /// <summary>
        /// ワールド位置ベクトルを行列変換してローカル位置ベクトルを算出する（同次座標系 w=1）。
        /// </summary>
        public SpawnVector3 MultiplyPoint3x4(SpawnVector3 point)
        {
            float x = M00 * point.X + M01 * point.Y + M02 * point.Z + M03;
            float y = M10 * point.X + M11 * point.Y + M12 * point.Z + M13;
            float z = M20 * point.X + M21 * point.Y + M22 * point.Z + M23;
            return new SpawnVector3(x, y, z);
        }

        public bool Equals(SpawnMatrix4x4 other) =>
            Math.Abs(M00 - other.M00) < 1e-6f && Math.Abs(M01 - other.M01) < 1e-6f && Math.Abs(M02 - other.M02) < 1e-6f && Math.Abs(M03 - other.M03) < 1e-6f &&
            Math.Abs(M10 - other.M10) < 1e-6f && Math.Abs(M11 - other.M11) < 1e-6f && Math.Abs(M12 - other.M12) < 1e-6f && Math.Abs(M13 - other.M13) < 1e-6f &&
            Math.Abs(M20 - other.M20) < 1e-6f && Math.Abs(M21 - other.M21) < 1e-6f && Math.Abs(M22 - other.M22) < 1e-6f && Math.Abs(M23 - other.M23) < 1e-6f &&
            Math.Abs(M30 - other.M30) < 1e-6f && Math.Abs(M31 - other.M31) < 1e-6f && Math.Abs(M32 - other.M32) < 1e-6f && Math.Abs(M33 - other.M33) < 1e-6f;

        public override bool Equals(object obj) => obj is SpawnMatrix4x4 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(M00, M11, M22, M33);
    }
}
