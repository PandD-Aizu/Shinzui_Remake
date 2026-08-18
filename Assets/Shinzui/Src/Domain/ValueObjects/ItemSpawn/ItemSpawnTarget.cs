using System;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// アイテムスポーン対象のドメイン定義モデル。
    /// POCOであり、Unity固有のGameObject参照などは持たない。
    /// </summary>
    public record ItemSpawnTarget
    {
        public string Id { get; init; }
        public ResourceCategory Category { get; init; }
        public float BaseWeight { get; init; }
        public int SpawnCost { get; init; }
        public int MinCount { get; init; }
        public int MaxCount { get; init; }
        public int CategoryMaxCount { get; init; }
        public float MinDistance { get; init; }
        public bool IsNormalRandomCandidate { get; init; }
        public float CollisionRadius { get; init; }
        public float SurfaceOffset { get; init; }
        public ItemSpawnRotationPolicy RotationPolicy { get; init; }

        public ItemSpawnTarget(
            string id,
            ResourceCategory category,
            float baseWeight = 1.0f,
            int spawnCost = 1,
            int minCount = 0,
            int maxCount = int.MaxValue,
            int categoryMaxCount = int.MaxValue,
            float minDistance = 1.0f,
            bool isNormalRandomCandidate = true,
            float collisionRadius = 0.3f,
            float surfaceOffset = 0.05f,
            ItemSpawnRotationPolicy rotationPolicy = ItemSpawnRotationPolicy.AlignToSurfaceNormalWithRandomYaw)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("ItemSpawnTarget Id cannot be null or empty.", nameof(id));
            }

            Id = id;
            Category = category;
            BaseWeight = Math.Max(0f, baseWeight);
            SpawnCost = Math.Max(1, spawnCost);
            MinCount = Math.Max(0, minCount);
            MaxCount = maxCount <= 0 ? int.MaxValue : maxCount;
            CategoryMaxCount = categoryMaxCount <= 0 ? int.MaxValue : categoryMaxCount;
            MinDistance = Math.Max(0f, minDistance);
            IsNormalRandomCandidate = isNormalRandomCandidate;
            CollisionRadius = Math.Max(0.01f, collisionRadius);
            SurfaceOffset = surfaceOffset;
            RotationPolicy = rotationPolicy;
        }
    }
}
