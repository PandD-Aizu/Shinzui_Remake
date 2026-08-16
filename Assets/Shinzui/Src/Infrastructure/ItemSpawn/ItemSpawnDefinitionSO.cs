using System;
using Shinzui.Application.DTOs.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using UnityEngine;

namespace Shinzui.Infrastructure.ItemSpawn
{
    /// <summary>
    /// スポーン対象アイテムのUnity Inspector設定ScriptableObject。
    /// 生成プレハブ、識別ID、基本ウェイト、コスト、カテゴリ、配置制約等を保持する。
    /// </summary>
    [CreateAssetMenu(fileName = "ItemSpawnDefinition", menuName = "Shinzui/ItemSpawn/ItemSpawnDefinition")]
    public class ItemSpawnDefinitionSO : ScriptableObject
    {
        [Header("Identification")]
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private ResourceCategory category = ResourceCategory.Utility;
        [SerializeField] private GameObject prefab;

        [Header("Lottery Weights & Costs")]
        [SerializeField, Min(0f)] private float baseWeight = 1.0f;
        [SerializeField, Min(1)] private int spawnCost = 1;
        [SerializeField] private bool isNormalRandomCandidate = true;

        [Header("Spawn Limits & Constraints")]
        [SerializeField, Min(0)] private int minCount = 0;
        [Tooltip("0以下の場合は無制限")]
        [SerializeField] private int maxCount = 0;
        [Tooltip("0以下の場合は無制限")]
        [SerializeField] private int categoryMaxCount = 0;
        [SerializeField, Min(0f)] private float minDistance = 1.0f;

        [Header("Placement Geometry")]
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.3f;
        [SerializeField] private float surfaceOffset = 0.05f;
        [SerializeField] private ItemSpawnRotationPolicy rotationPolicy = ItemSpawnRotationPolicy.AlignToSurfaceNormalWithRandomYaw;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public ResourceCategory Category => category;
        public GameObject Prefab => prefab;
        public float BaseWeight => baseWeight;
        public int SpawnCost => spawnCost;
        public bool IsNormalRandomCandidate => isNormalRandomCandidate;
        public int MinCount => minCount;
        public int MaxCount => maxCount <= 0 ? int.MaxValue : maxCount;
        public int CategoryMaxCount => categoryMaxCount <= 0 ? int.MaxValue : categoryMaxCount;
        public float MinDistance => minDistance;
        public float CollisionRadius => collisionRadius;
        public float SurfaceOffset => surfaceOffset;
        public ItemSpawnRotationPolicy RotationPolicy => rotationPolicy;

        public ItemSpawnTargetDto ToDto()
        {
            return new ItemSpawnTargetDto
            {
                Id = string.IsNullOrEmpty(itemId) ? name : itemId,
                Category = category,
                BaseWeight = baseWeight,
                SpawnCost = spawnCost,
                MinCount = minCount,
                MaxCount = maxCount <= 0 ? int.MaxValue : maxCount,
                CategoryMaxCount = categoryMaxCount <= 0 ? int.MaxValue : categoryMaxCount,
                MinDistance = minDistance,
                IsNormalRandomCandidate = isNormalRandomCandidate,
                CollisionRadius = collisionRadius,
                SurfaceOffset = surfaceOffset,
                RotationPolicy = rotationPolicy
            };
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(itemId))
            {
                itemId = name;
            }
        }
    }
}
