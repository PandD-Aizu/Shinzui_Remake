using UnityEngine;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// アイテムごとの配置サイズ、法線オフセット、回転方針、物理衝突判定ポリシー
    /// </summary>
    public sealed class ItemPlacementPolicy
    {
        public Vector3 BoundsSize { get; }
        public float HeightOffset { get; }
        public PlacementRotationMode RotationMode { get; }
        public PlacementCollisionShape CollisionShape { get; }

        public ItemPlacementPolicy(
            Vector3? boundsSize = null,
            float heightOffset = 0f,
            PlacementRotationMode rotationMode = PlacementRotationMode.AlignWithNormalAndRandomYaw,
            PlacementCollisionShape collisionShape = PlacementCollisionShape.Box)
        {
            BoundsSize = boundsSize ?? new Vector3(0.5f, 0.5f, 0.5f);
            HeightOffset = heightOffset;
            RotationMode = rotationMode;
            CollisionShape = collisionShape;
        }

        public static ItemPlacementPolicy Default => new();
    }
}
