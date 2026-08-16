using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;

namespace Shinzui.Application.Interfaces.ItemSpawn
{
    /// <summary>
    /// スポーンサーフェスからドメイン層のSpawnSurfaceTriangle群を抽出・提供するインターフェース
    /// </summary>
    public interface ISpawnSurfaceDataProvider
    {
        IReadOnlyList<SpawnSurfaceTriangle> ExtractTriangles();
    }
}
