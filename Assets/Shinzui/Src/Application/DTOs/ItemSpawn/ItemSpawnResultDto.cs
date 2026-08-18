using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Application.DTOs.ItemSpawn
{
    /// <summary>
    /// アイテム生成実行結果のサマリーDTO
    /// </summary>
    public sealed class ItemSpawnResultDto
    {
        public List<SpawnedItemDto> SpawnedItems { get; set; } = new();
        public int TotalSpawnedCount => SpawnedItems.Count;
        public int TotalCostUsed { get; set; }
        public int TotalCostSpent
        {
            get => TotalCostUsed;
            set => TotalCostUsed = value;
        }
        public int RemainingBudget { get; set; }
        public int FailedPlacementAttempts { get; set; }
        public List<string> SkipReasons { get; set; } = new();
        public Dictionary<string, float> FinalItemWeights { get; set; } = new();
        public Dictionary<string, float> CandidateFinalWeights
        {
            get => FinalItemWeights;
            set => FinalItemWeights = value;
        }
        public Dictionary<ResourceCategory, float> CategoryNeedWeights { get; set; } = new();
        public int Seed { get; set; }
        public int TotalTrianglesEvaluated { get; set; }
        public int ValidTrianglesCount { get; set; }
        public float TotalValidTriangleArea { get; set; }
        public bool HasReachedMaxSpawns { get; set; }
        public bool Success => SpawnedItems.Count > 0 || (TotalSpawnedCount == 0 && FailedPlacementAttempts == 0);

        public static ItemSpawnResultDto FromDomain(ItemSpawnRunResult result)
        {
            if (result == null) return new ItemSpawnResultDto();

            var dto = new ItemSpawnResultDto
            {
                TotalCostUsed = result.TotalCostSpent,
                RemainingBudget = result.RemainingBudget,
                FailedPlacementAttempts = result.FailedPlacementAttempts,
                SkipReasons = result.SkipReasons != null ? new List<string>(result.SkipReasons) : new List<string>(),
                FinalItemWeights = result.CandidateFinalWeights != null
                    ? new Dictionary<string, float>(result.CandidateFinalWeights, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, float>(),
                TotalTrianglesEvaluated = result.TotalTrianglesEvaluated,
                ValidTrianglesCount = result.ValidTrianglesCount,
                TotalValidTriangleArea = result.TotalValidTriangleArea,
                HasReachedMaxSpawns = result.HasReachedMaxSpawns
            };

            if (result.SpawnedItems != null)
            {
                for (int i = 0; i < result.SpawnedItems.Count; i++)
                {
                    dto.SpawnedItems.Add(SpawnedItemDto.FromDomain(result.SpawnedItems[i]));
                }
            }

            return dto;
        }
    }
}
