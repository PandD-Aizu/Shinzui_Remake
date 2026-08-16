using System;
using System.Collections.Generic;
using Shinzui.Application.DTOs.ItemSpawn;
using UnityEngine;

namespace Shinzui.Infrastructure.ItemSpawn
{
    [Serializable]
    public class GuaranteedSpawnItemEntry
    {
        [SerializeField] private ItemSpawnDefinitionSO itemDefinition;
        [SerializeField, Min(1)] private int count = 1;

        public ItemSpawnDefinitionSO ItemDefinition => itemDefinition;
        public int Count => count;

        public GuaranteedSpawnRuleDto ToDto()
        {
            return new GuaranteedSpawnRuleDto
            {
                ItemId = itemDefinition != null ? itemDefinition.ItemId : string.Empty,
                Count = count
            };
        }
    }

    /// <summary>
    /// アイテムスポーン生成条件（予算・スポーン数・試行上限・最低保証・物理マスク等）のScriptableObject。
    /// </summary>
    [CreateAssetMenu(fileName = "ItemSpawnConfig", menuName = "Shinzui/ItemSpawn/ItemSpawnConfig")]
    public class ItemSpawnConfigSO : ScriptableObject
    {
        [Header("Seed & Budget")]
        [SerializeField] private int seed = 2777;
        [SerializeField, Min(0)] private int budget = 100;

        [Header("Spawn Count Limits")]
        [SerializeField, Min(0)] private int minTotalSpawns = 1;
        [SerializeField, Min(1)] private int maxTotalSpawns = 50;

        [Header("Placement Attempts")]
        [SerializeField, Min(1)] private int maxPlacementAttemptsPerItem = 30;
        [SerializeField, Min(10)] private int maxTotalPlacementAttempts = 300;

        [Header("Player Safety")]
        [SerializeField, Min(0f)] private float playerStartMinDistance = 5.0f;

        [Header("Minimum Guarantees")]
        [SerializeField] private List<GuaranteedSpawnItemEntry> guaranteedSpawns = new();

        [Header("Physics Collision Detection")]
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        public int Seed
        {
            get => seed;
            set => seed = value;
        }

        public int Budget => budget;
        public int MinTotalSpawns => minTotalSpawns;
        public int MaxTotalSpawns => maxTotalSpawns;
        public int MaxPlacementAttemptsPerItem => maxPlacementAttemptsPerItem;
        public int MaxTotalPlacementAttempts => maxTotalPlacementAttempts;
        public float PlayerStartMinDistance => playerStartMinDistance;
        public IReadOnlyList<GuaranteedSpawnItemEntry> GuaranteedSpawns => guaranteedSpawns;
        public LayerMask ObstacleMask => obstacleMask;
        public QueryTriggerInteraction TriggerInteraction => triggerInteraction;

        public ItemSpawnConfigDto ToDto(Vector3 playerStartPosition)
        {
            var dto = new ItemSpawnConfigDto
            {
                Seed = seed,
                Budget = budget,
                MinTotalSpawns = minTotalSpawns,
                MaxTotalSpawns = maxTotalSpawns,
                MaxPlacementAttemptsPerItem = maxPlacementAttemptsPerItem,
                MaxTotalPlacementAttempts = maxTotalPlacementAttempts,
                PlayerStartMinDistance = playerStartMinDistance,
                PlayerStartX = playerStartPosition.x,
                PlayerStartY = playerStartPosition.y,
                PlayerStartZ = playerStartPosition.z,
                GuaranteedRules = new List<GuaranteedSpawnRuleDto>()
            };

            if (guaranteedSpawns != null)
            {
                foreach (var g in guaranteedSpawns)
                {
                    if (g != null && g.ItemDefinition != null)
                    {
                        dto.GuaranteedRules.Add(g.ToDto());
                    }
                }
            }

            return dto;
        }
    }
}
