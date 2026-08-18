using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;

namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// サーフェスメッシュからの三角形重み付け選択および三角形内の一様サンプリングを行うドメインサービス。
    /// </summary>
    public interface ISurfaceSamplerService
    {
        IReadOnlyList<SpawnSurfaceTriangle> CollectValidTriangles(
            IReadOnlyList<SpawnSurfaceData> surfaces,
            out int totalTrianglesCount,
            out float totalArea);

        bool TrySelectTriangle(
            IReadOnlyList<SpawnSurfaceTriangle> validTriangles,
            ISpawnPrng prng,
            out SpawnSurfaceTriangle selectedTriangle);

        SpawnVector3 SamplePointInTriangle(
            SpawnSurfaceTriangle triangle,
            ISpawnPrng prng,
            out SpawnVector3 normal);

        bool TrySampleCandidatePoint(
            IReadOnlyList<SpawnSurfaceData> surfaces,
            IReadOnlyList<SpawnSurfaceTriangle> validTriangles,
            ISpawnPrng prng,
            out SpawnVector3 position,
            out SpawnVector3 normal,
            out SpawnSurfaceTriangle triangle,
            out float sampledWeight);
    }
}
