using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// アイテム抽選・位置サンプリング・最低保証・予算制約・バリデーション実行を統括するドメインサービスインターフェース
    /// </summary>
    public interface IItemSpawnLotteryCore
    {
        ItemSpawnExecutionReport Execute(
            ItemSpawnConfig config,
            IReadOnlyList<ItemSpawnDefinition> itemDefinitions,
            IReadOnlyList<SpawnSurfaceTriangle> triangles,
            IReadOnlyDictionary<ResourceCategory, float> categoryNeedWeights,
            IRandomNumberGenerator rng,
            ISpawnPositionValidator validator);
    }
}
