using UnityEngine;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// スポーン成功した個々のアイテムの生成結果データ（POCO）
    /// </summary>
    public sealed class SpawnedItemData
    {
        public string ItemId { get; }
        public ItemSpawnDefinition Definition { get; }
        public Vector3 WorldPosition { get; }
        public Quaternion WorldRotation { get; }
        public int SurfaceId { get; }
        public string SurfaceName { get; }
        public int TriangleIndex { get; }
        public int Seed { get; }
        public int Cost { get; }
        public float EffectiveWeight { get; }
        public bool IsGuaranteed { get; }

        public SpawnedItemData(
            string itemId,
            ItemSpawnDefinition definition,
            Vector3 worldPosition,
            Quaternion worldRotation,
            int surfaceId,
            string surfaceName,
            int triangleIndex,
            int seed,
            int cost,
            float effectiveWeight,
            bool isGuaranteed)
        {
            ItemId = itemId;
            Definition = definition;
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
            SurfaceId = surfaceId;
            SurfaceName = surfaceName ?? string.Empty;
            TriangleIndex = triangleIndex;
            Seed = seed;
            Cost = cost;
            EffectiveWeight = effectiveWeight;
            IsGuaranteed = isGuaranteed;
        }
    }
}
