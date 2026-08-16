using System;
using System.Collections.Generic;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// 1つのスポーンサーフェス（メッシュ）に含まれる三角形群および2D Weight Mapのドメインデータ。
    /// </summary>
    public record SpawnSurfaceData
    {
        public string SurfaceId { get; init; }
        public float RegionWeight { get; init; }
        public IReadOnlyList<SpawnSurfaceTriangle> Triangles { get; init; }
        public SpawnSurfaceWeightMap WeightMap { get; init; }
        public bool IsValid { get; init; }

        public SpawnSurfaceData(
            string surfaceId,
            float regionWeight,
            IReadOnlyList<SpawnSurfaceTriangle> triangles,
            bool isValid = true)
            : this(surfaceId, regionWeight, triangles, null, isValid)
        {
        }

        public SpawnSurfaceData(
            string surfaceId,
            float regionWeight,
            IReadOnlyList<SpawnSurfaceTriangle> triangles,
            SpawnSurfaceWeightMap weightMap,
            bool isValid = true)
        {
            SurfaceId = surfaceId ?? string.Empty;
            RegionWeight = Math.Max(0f, regionWeight);
            Triangles = triangles ?? Array.Empty<SpawnSurfaceTriangle>();
            WeightMap = weightMap;
            IsValid = isValid && RegionWeight > 0f && Triangles.Count > 0;
        }

        public float SampleWeightAtWorldPosition(SpawnVector3 worldPos)
        {
            if (WeightMap != null && WeightMap.IsValid)
            {
                return WeightMap.SampleWorldPosition(worldPos);
            }
            return 1.0f;
        }
    }
}
