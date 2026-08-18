using System;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// ドメイン層におけるアイテム生成対象の定義（POCO）。
    /// コスト、基準重み、カテゴリ、生成数制約、配置ポリシーなどを保持する。
    /// </summary>
    public sealed class ItemSpawnDefinition
    {
        public string Id { get; }
        public float BaseWeight { get; }
        public ResourceCategory Category { get; }
        public int SpawnCost { get; }
        public int MinCount { get; }
        public int MaxCount { get; }
        public int CategoryMinCount { get; }
        public int CategoryMaxCount { get; }
        public float MinDistance { get; }
        public bool IsNormalRandomCandidate { get; }
        public ItemPlacementPolicy PlacementPolicy { get; }

        public ItemSpawnDefinition(
            string id,
            float baseWeight,
            ResourceCategory category,
            int spawnCost = 1,
            int minCount = 0,
            int maxCount = 99,
            int categoryMinCount = 0,
            int categoryMaxCount = 99,
            float minDistance = 1.0f,
            bool isNormalRandomCandidate = true,
            ItemPlacementPolicy placementPolicy = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Item definition ID cannot be null or empty.", nameof(id));
            }

            Id = id;
            BaseWeight = Math.Max(0f, baseWeight);
            Category = category;
            SpawnCost = Math.Max(0, spawnCost);
            MinCount = Math.Max(0, minCount);
            MaxCount = Math.Max(MinCount, maxCount);
            CategoryMinCount = Math.Max(0, categoryMinCount);
            CategoryMaxCount = Math.Max(CategoryMinCount, categoryMaxCount);
            MinDistance = Math.Max(0f, minDistance);
            IsNormalRandomCandidate = isNormalRandomCandidate;
            PlacementPolicy = placementPolicy ?? ItemPlacementPolicy.Default;
        }
    }
}
