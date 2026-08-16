using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;

namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// サーフェスメッシュの三角形選択および位置サンプリングを行うドメインサービス実装。
    /// weight = World Triangle Area * PaintWeight * RegionWeight に基づき選択し、
    /// 三角形内では平方根バリセントリック座標変換により面積一様サンプリングを行う。
    /// </summary>
    public sealed class SurfaceSamplerService : ISurfaceSamplerService
    {
        public IReadOnlyList<SpawnSurfaceTriangle> CollectValidTriangles(
            IReadOnlyList<SpawnSurfaceData> surfaces,
            out int totalTrianglesCount,
            out float totalArea)
        {
            totalTrianglesCount = 0;
            totalArea = 0f;

            if (surfaces == null || surfaces.Count == 0)
            {
                return Array.Empty<SpawnSurfaceTriangle>();
            }

            var validList = new List<SpawnSurfaceTriangle>();
            for (int s = 0; s < surfaces.Count; s++)
            {
                SpawnSurfaceData surface = surfaces[s];
                if (surface == null || !surface.IsValid || surface.Triangles == null)
                {
                    continue;
                }

                int triCount = surface.Triangles.Count;
                totalTrianglesCount += triCount;

                for (int t = 0; t < triCount; t++)
                {
                    SpawnSurfaceTriangle triangle = surface.Triangles[t];
                    if (triangle.IsValid)
                    {
                        validList.Add(triangle);
                        totalArea += triangle.Area;
                    }
                }
            }

            return validList;
        }

        public bool TrySelectTriangle(
            IReadOnlyList<SpawnSurfaceTriangle> validTriangles,
            ISpawnPrng prng,
            out SpawnSurfaceTriangle selectedTriangle)
        {
            selectedTriangle = default;
            if (validTriangles == null || validTriangles.Count == 0)
            {
                return false;
            }

            // 総ウェイトの計算（Area * RegionWeight を基本とし、旧PaintWeight==0の縮退のみ除外）
            double totalWeight = 0.0;
            for (int i = 0; i < validTriangles.Count; i++)
            {
                var tri = validTriangles[i];
                float w = tri.Area * tri.RegionWeight;
                if (tri.PaintWeight > 0f && w > 0f)
                {
                    totalWeight += w;
                }
            }

            if (totalWeight <= 1e-7)
            {
                // フォールバック: TotalWeight
                for (int i = 0; i < validTriangles.Count; i++)
                {
                    totalWeight += validTriangles[i].TotalWeight;
                }
            }

            if (totalWeight <= 1e-7)
            {
                return false;
            }

            double randomValue = prng.NextDouble() * totalWeight;
            double cumulative = 0.0;

            for (int i = 0; i < validTriangles.Count; i++)
            {
                var tri = validTriangles[i];
                float w = tri.Area * tri.RegionWeight;
                if (tri.PaintWeight > 0f && w > 0f)
                {
                    cumulative += w;
                    if (randomValue <= cumulative || i == validTriangles.Count - 1)
                    {
                        selectedTriangle = tri;
                        return true;
                    }
                }
            }

            selectedTriangle = validTriangles[validTriangles.Count - 1];
            return true;
        }

        public SpawnVector3 SamplePointInTriangle(
            SpawnSurfaceTriangle triangle,
            ISpawnPrng prng,
            out SpawnVector3 normal)
        {
            normal = triangle.Normal;

            // 平方根サンプリングによる面積一様サンプリング (Square Root Barycentric Sampling)
            // r1 in (0, 1), r2 in [0, 1)
            float r1 = prng.NextFloat();
            float r2 = prng.NextFloat();

            // 境界値安全処理
            if (r1 <= 1e-6f) r1 = 1e-6f;
            if (r1 >= 1f) r1 = 0.999999f;

            float sqrtR1 = (float)Math.Sqrt(r1);
            float u = 1f - sqrtR1;
            float v = r2 * sqrtR1;
            float w = 1f - u - v;

            return (u * triangle.A) + (v * triangle.B) + (w * triangle.C);
        }

        public bool TrySampleCandidatePoint(
            IReadOnlyList<SpawnSurfaceData> surfaces,
            IReadOnlyList<SpawnSurfaceTriangle> validTriangles,
            ISpawnPrng prng,
            out SpawnVector3 position,
            out SpawnVector3 normal,
            out SpawnSurfaceTriangle triangle,
            out float sampledWeight)
        {
            position = SpawnVector3.Zero;
            normal = SpawnVector3.Up;
            triangle = default;
            sampledWeight = 0f;

            if (validTriangles == null || validTriangles.Count == 0 || prng == null)
            {
                return false;
            }

            // 1. 幾何面積・RegionWeight比例ルーレットで三角形を選択（旧PaintWeightに依存しない）
            if (!TrySelectTriangle(validTriangles, prng, out triangle))
            {
                return false;
            }

            // 2. 三角形内部を一様サンプリング
            position = SamplePointInTriangle(triangle, prng, out normal);

            // 3. Weight Map サンプリング（該当サーフェスの2D Weight Mapが存在する場合はバイリニア取得）
            SpawnSurfaceData matchedSurface = null;
            if (surfaces != null)
            {
                for (int i = 0; i < surfaces.Count; i++)
                {
                    if (surfaces[i] != null && string.Equals(surfaces[i].SurfaceId, triangle.SurfaceId, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedSurface = surfaces[i];
                        break;
                    }
                }
            }

            if (matchedSurface != null && matchedSurface.WeightMap != null && matchedSurface.WeightMap.IsValid)
            {
                sampledWeight = matchedSurface.WeightMap.SampleWorldPosition(position);
            }
            else
            {
                sampledWeight = triangle.PaintWeight;
            }

            // 4. Weight 0 は完全除外
            if (sampledWeight <= 0f)
            {
                return false;
            }

            // 5. Rejection Sampling (棄却サンプリング): 乱数 R in [0, 1] <= sampledWeight なら採用
            float roll = prng.NextFloat(0f, 1f);
            if (roll > sampledWeight)
            {
                return false;
            }

            return true;
        }
    }
}
