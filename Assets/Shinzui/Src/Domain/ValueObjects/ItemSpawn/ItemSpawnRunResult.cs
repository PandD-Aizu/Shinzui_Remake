using System;
using System.Collections.Generic;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// アイテム生成実行全体のドメイン結果。
    /// 生成されたアイテム一覧、総コスト、残予算、配置失敗回数、スキップ理由、ウェイトスナップショット等を網羅する。
    /// </summary>
    public record ItemSpawnRunResult
    {
        public IReadOnlyList<SpawnedItemRecord> SpawnedItems { get; init; }
        public int TotalSpawnedCount => SpawnedItems?.Count ?? 0;
        public int TotalCostSpent { get; init; }
        public int RemainingBudget { get; init; }
        public int FailedPlacementAttempts { get; init; }
        public IReadOnlyList<string> SkipReasons { get; init; }
        public IReadOnlyDictionary<string, float> CandidateFinalWeights { get; init; }
        public int TotalTrianglesEvaluated { get; init; }
        public int ValidTrianglesCount { get; init; }
        public float TotalValidTriangleArea { get; init; }
        public bool HasReachedMaxSpawns { get; init; }

        public ItemSpawnRunResult(
            IReadOnlyList<SpawnedItemRecord> spawnedItems,
            int totalCostSpent,
            int remainingBudget,
            int failedPlacementAttempts,
            IReadOnlyList<string> skipReasons,
            IReadOnlyDictionary<string, float> candidateFinalWeights,
            int totalTrianglesEvaluated,
            int validTrianglesCount,
            float totalValidTriangleArea,
            bool hasReachedMaxSpawns)
        {
            SpawnedItems = spawnedItems ?? Array.Empty<SpawnedItemRecord>();
            TotalCostSpent = Math.Max(0, totalCostSpent);
            RemainingBudget = Math.Max(0, remainingBudget);
            FailedPlacementAttempts = Math.Max(0, failedPlacementAttempts);
            SkipReasons = skipReasons ?? Array.Empty<string>();
            CandidateFinalWeights = candidateFinalWeights ?? new Dictionary<string, float>();
            TotalTrianglesEvaluated = totalTrianglesEvaluated;
            ValidTrianglesCount = validTrianglesCount;
            TotalValidTriangleArea = totalValidTriangleArea;
            HasReachedMaxSpawns = hasReachedMaxSpawns;
        }
    }
}
