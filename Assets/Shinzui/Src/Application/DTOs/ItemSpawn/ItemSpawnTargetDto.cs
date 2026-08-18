using System;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Application.DTOs.ItemSpawn
{
    [Serializable]
    public class ItemSpawnTargetDto
    {
        public string Id;
        public ResourceCategory Category;
        public float BaseWeight = 1.0f;
        public int SpawnCost = 1;
        public int MinCount = 0;
        public int MaxCount = int.MaxValue;
        public int CategoryMaxCount = int.MaxValue;
        public float MinDistance = 1.0f;
        public bool IsNormalRandomCandidate = true;
        public float CollisionRadius = 0.3f;
        public float SurfaceOffset = 0.05f;
        public ItemSpawnRotationPolicy RotationPolicy = ItemSpawnRotationPolicy.AlignToSurfaceNormalWithRandomYaw;

        public ItemSpawnTarget ToDomain()
        {
            return new ItemSpawnTarget(
                Id,
                Category,
                BaseWeight,
                SpawnCost,
                MinCount,
                MaxCount,
                CategoryMaxCount,
                MinDistance,
                IsNormalRandomCandidate,
                CollisionRadius,
                SurfaceOffset,
                RotationPolicy);
        }
    }
}
