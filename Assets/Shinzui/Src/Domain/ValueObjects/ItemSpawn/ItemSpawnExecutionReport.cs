using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// スポーン実行全体のサマリー・デバッグ結果レポート（POCO）
    /// </summary>
    public sealed class ItemSpawnExecutionReport
    {
        public IReadOnlyList<SpawnedItemData> SpawnedItems { get; }
        public int TotalSpawnedCount => SpawnedItems.Count;
        public int TotalCostUsed { get; }
        public int RemainingBudget { get; }
        public int FailedPlacementAttempts { get; }
        public IReadOnlyList<string> SkipReasons { get; }
        public IReadOnlyDictionary<string, float> FinalItemWeights { get; }
        public IReadOnlyDictionary<ResourceCategory, float> CategoryNeedWeights { get; }
        public int Seed { get; }

        public ItemSpawnExecutionReport(
            IReadOnlyList<SpawnedItemData> spawnedItems,
            int totalCostUsed,
            int remainingBudget,
            int failedPlacementAttempts,
            IReadOnlyList<string> skipReasons,
            IDictionary<string, float> finalItemWeights,
            IDictionary<ResourceCategory, float> categoryNeedWeights,
            int seed)
        {
            SpawnedItems = spawnedItems ?? Array.Empty<SpawnedItemData>();
            TotalCostUsed = totalCostUsed;
            RemainingBudget = remainingBudget;
            FailedPlacementAttempts = failedPlacementAttempts;
            SkipReasons = skipReasons ?? Array.Empty<string>();

            FinalItemWeights = finalItemWeights != null
                ? new ReadOnlyDictionary<string, float>(new Dictionary<string, float>(finalItemWeights))
                : new ReadOnlyDictionary<string, float>(new Dictionary<string, float>());

            CategoryNeedWeights = categoryNeedWeights != null
                ? new ReadOnlyDictionary<ResourceCategory, float>(new Dictionary<ResourceCategory, float>(categoryNeedWeights))
                : new ReadOnlyDictionary<ResourceCategory, float>(new Dictionary<ResourceCategory, float>());

            Seed = seed;
        }
    }
}
