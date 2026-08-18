using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using UnityEngine;

namespace Shinzui.Infrastructure.ItemSpawn
{
    [Serializable]
    public sealed class ItemSpawnGuaranteeConfig
    {
        [SerializeField] private string itemId;
        [SerializeField] private bool useCategory;
        [SerializeField] private ResourceCategory category;
        [SerializeField, Min(1)] private int guaranteedCount = 1;

        public string ItemId => itemId;
        public bool UseCategory => useCategory;
        public ResourceCategory Category => category;
        public int GuaranteedCount => guaranteedCount;

        public ItemSpawnGuaranteeConfig() { }

        public ItemSpawnGuaranteeConfig(string itemId, int count)
        {
            this.itemId = itemId;
            this.useCategory = false;
            this.guaranteedCount = count;
        }

        public ItemSpawnGuaranteeConfig(ResourceCategory category, int count)
        {
            this.itemId = null;
            this.useCategory = true;
            this.category = category;
            this.guaranteedCount = count;
        }
    }

    /// <summary>
    /// アイテム生成システム全体の予算、制限、物理設定、対象アイテムリストを保持するScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "ItemSpawnSettings", menuName = "Shinzui/ItemSpawn/Item Spawn Settings", order = 20)]
    public sealed class ItemSpawnSettingsSO : ScriptableObject
    {
        [Header("Budget & Overall Limits")]
        [SerializeField, Min(0)] private int budget = 50;
        [SerializeField, Min(0)] private int minTotalCount = 0;
        [SerializeField, Min(1)] private int maxTotalCount = 50;
        [SerializeField, Min(1)] private int maxPlacementAttempts = 50;

        [Header("Player Start Distance")]
        [SerializeField, Min(0f)] private float minPlayerStartDistance = 3.0f;

        [Header("Physics Collision Checks")]
        [SerializeField] private bool checkPhysicsCollision = true;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;
        [SerializeField] private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Item Definitions")]
        [SerializeField] private List<ItemSpawnDefinitionSO> itemDefinitions = new();

        [Header("Explicit Minimum Guarantees")]
        [SerializeField] private List<ItemSpawnGuaranteeConfig> explicitGuarantees = new();

        public int Budget => budget;
        public int MinTotalCount => minTotalCount;
        public int MaxTotalCount => maxTotalCount;
        public int MaxPlacementAttempts => maxPlacementAttempts;
        public float MinPlayerStartDistance => minPlayerStartDistance;
        public bool CheckPhysicsCollision => checkPhysicsCollision;
        public LayerMask ObstacleLayerMask => obstacleLayerMask;
        public QueryTriggerInteraction QueryTriggerInteraction => queryTriggerInteraction;
        public IReadOnlyList<ItemSpawnDefinitionSO> ItemDefinitions => itemDefinitions;
        public IReadOnlyList<ItemSpawnGuaranteeConfig> ExplicitGuarantees => explicitGuarantees;

        private void OnValidate()
        {
            if (maxTotalCount < minTotalCount) maxTotalCount = minTotalCount;
        }
    }
}
