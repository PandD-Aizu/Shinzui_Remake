using System;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// サーフェスメッシュのトポロジーに依存しない2D Weight Mapのドメイン値オブジェクト（POCO）。
    /// 解像度、重みテクセル配列、ローカル境界、投影平面、4x4逆変換行列によるWorld/Localサンプリング（バイリニア補間）を提供する。
    /// </summary>
    public sealed class SpawnSurfaceWeightMap
    {
        public int Resolution { get; }
        public float[] Weights { get; }
        public SpawnVector3 BoundsMin { get; }
        public SpawnVector3 BoundsSize { get; }
        public SpawnSurfaceProjectionPlane ProjectionPlane { get; }
        public SpawnMatrix4x4 WorldToLocalMatrix { get; }

        public bool IsValid => Weights != null && Weights.Length == Resolution * Resolution && Resolution > 0;

        public SpawnSurfaceWeightMap(
            int resolution,
            float[] weights,
            SpawnVector3 boundsMin,
            SpawnVector3 boundsSize,
            SpawnMatrix4x4 worldToLocalMatrix,
            SpawnSurfaceProjectionPlane projectionPlane = SpawnSurfaceProjectionPlane.XZ)
        {
            Resolution = Math.Max(1, resolution);
            Weights = weights ?? Array.Empty<float>();
            BoundsMin = boundsMin;
            BoundsSize = boundsSize;
            WorldToLocalMatrix = worldToLocalMatrix;
            ProjectionPlane = projectionPlane;
        }

        public SpawnSurfaceWeightMap(
            int resolution,
            float[] weights,
            SpawnVector3 boundsMin,
            SpawnVector3 boundsSize,
            SpawnVector3 transformPosition,
            SpawnVector3 transformScale,
            SpawnSurfaceProjectionPlane projectionPlane = SpawnSurfaceProjectionPlane.XZ)
        {
            Resolution = Math.Max(1, resolution);
            Weights = weights ?? Array.Empty<float>();
            BoundsMin = boundsMin;
            BoundsSize = boundsSize;
            ProjectionPlane = projectionPlane;

            float sx = Math.Abs(transformScale.X) > 1e-6f ? 1f / transformScale.X : 1f;
            float sy = Math.Abs(transformScale.Y) > 1e-6f ? 1f / transformScale.Y : 1f;
            float sz = Math.Abs(transformScale.Z) > 1e-6f ? 1f / transformScale.Z : 1f;

            WorldToLocalMatrix = new SpawnMatrix4x4(
                sx, 0f, 0f, -transformPosition.X * sx,
                0f, sy, 0f, -transformPosition.Y * sy,
                0f, 0f, sz, -transformPosition.Z * sz,
                0f, 0f, 0f, 1f);
        }

        public static SpawnSurfaceWeightMap CreateUniform(
            int resolution,
            float initialWeight,
            SpawnVector3 boundsMin,
            SpawnVector3 boundsSize,
            SpawnMatrix4x4 worldToLocalMatrix = default,
            SpawnSurfaceProjectionPlane projectionPlane = SpawnSurfaceProjectionPlane.XZ)
        {
            resolution = Math.Max(1, resolution);
            float[] weights = new float[resolution * resolution];
            float clampedWeight = Math.Clamp(initialWeight, 0f, 1f);
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = clampedWeight;
            }

            return new SpawnSurfaceWeightMap(
                resolution,
                weights,
                boundsMin,
                boundsSize,
                worldToLocalMatrix.Equals(default) ? SpawnMatrix4x4.Identity : worldToLocalMatrix,
                projectionPlane);
        }

        /// <summary>
        /// 正規化座標 (u, v) in [0, 1] における重みをバイリニア補間でサンプリングする。
        /// 領域外の場合は 0.0f を返す。
        /// </summary>
        public float SampleBilinear(float u, float v)
        {
            if (!IsValid || u < 0f || u > 1f || v < 0f || v > 1f)
            {
                return 0f;
            }

            int n = Resolution;
            if (n == 1)
            {
                return Weights.Length > 0 ? Math.Clamp(Weights[0], 0f, 1f) : 0f;
            }

            float px = u * (n - 1);
            float py = v * (n - 1);

            int x0 = (int)MathF.Floor(px);
            int y0 = (int)MathF.Floor(py);
            int x1 = Math.Min(x0 + 1, n - 1);
            int y1 = Math.Min(y0 + 1, n - 1);

            float fx = px - x0;
            float fy = py - y0;

            float w00 = Weights[y0 * n + x0];
            float w10 = Weights[y0 * n + x1];
            float w01 = Weights[y1 * n + x0];
            float w11 = Weights[y1 * n + x1];

            float top = (1f - fx) * w00 + fx * w10;
            float bottom = (1f - fx) * w01 + fx * w11;
            float result = (1f - fy) * top + fy * bottom;

            return Math.Clamp(result, 0f, 1f);
        }

        /// <summary>
        /// ローカル座標から (u, v) を計算してサンプリングする。
        /// </summary>
        public float SampleLocalPosition(SpawnVector3 localPos)
        {
            if (LocalToUV(localPos, out float u, out float v))
            {
                return SampleBilinear(u, v);
            }
            return 0f;
        }

        /// <summary>
        /// ワールド座標から4x4逆変換行列を用いてサンプリングする。
        /// </summary>
        public float SampleWorldPosition(SpawnVector3 worldPos)
        {
            if (WorldToUV(worldPos, out float u, out float v))
            {
                return SampleBilinear(u, v);
            }
            return 0f;
        }

        /// <summary>
        /// ローカル座標を正規化UV座標 [0, 1] に変換する。
        /// </summary>
        public bool LocalToUV(SpawnVector3 localPos, out float u, out float v)
        {
            float sizeX = Math.Max(BoundsSize.X, 1e-4f);
            float sizeY = Math.Max(BoundsSize.Y, 1e-4f);
            float sizeZ = Math.Max(BoundsSize.Z, 1e-4f);

            switch (ProjectionPlane)
            {
                case SpawnSurfaceProjectionPlane.XY:
                    u = (localPos.X - BoundsMin.X) / sizeX;
                    v = (localPos.Y - BoundsMin.Y) / sizeY;
                    break;

                case SpawnSurfaceProjectionPlane.YZ:
                    u = (localPos.Y - BoundsMin.Y) / sizeY;
                    v = (localPos.Z - BoundsMin.Z) / sizeZ;
                    break;

                case SpawnSurfaceProjectionPlane.XZ:
                default:
                    u = (localPos.X - BoundsMin.X) / sizeX;
                    v = (localPos.Z - BoundsMin.Z) / sizeZ;
                    break;
            }

            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        /// <summary>
        /// ワールド座標を正規化UV座標 [0, 1] に変換する。
        /// </summary>
        public bool WorldToUV(SpawnVector3 worldPos, out float u, out float v)
        {
            SpawnVector3 localPos = WorldToLocalMatrix.MultiplyPoint3x4(worldPos);
            return LocalToUV(localPos, out u, out v);
        }
    }
}
