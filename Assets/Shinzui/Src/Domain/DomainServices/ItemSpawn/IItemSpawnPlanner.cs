using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// 設定・サーフェス・アイテム定義・NeedWeightに基づき、
    /// 最低保証・通常抽選・幾何配置サンプリング・バリデーション・コスト管理を統括するドメインプランナー。
    /// </summary>
    public interface IItemSpawnPlanner
    {
        ItemSpawnRunResult PlanSpawns(
            ItemSpawnRunConfig config,
            IReadOnlyList<ItemSpawnTarget> targets,
            IReadOnlyList<SpawnSurfaceData> surfaces,
            Func<ResourceCategory, float> needWeightProvider = null,
            ISpawnPlacementValidator customValidator = null);
    }
}
