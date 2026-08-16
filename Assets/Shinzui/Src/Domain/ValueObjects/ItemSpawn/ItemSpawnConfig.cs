using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// アイテム生成実行時の全体制約・設定情報（POCO）
    /// </summary>
    public sealed class ItemSpawnConfig
    {
        public int Budget { get; }
        public int MinTotalCount { get; }
        public int MaxTotalCount { get; }
        public int MaxPlacementAttempts { get; }
        public float MinPlayerStartDistance { get; }
        public Vector3? PlayerStartPosition { get; }
        public int PhysicsLayerMask { get; }
        public QueryTriggerInteraction TriggerInteraction { get; }
        public bool CheckPhysicsCollision { get; }
        public IReadOnlyList<ItemSpawnGuarantee> Guarantees { get; }

        public ItemSpawnConfig(
            int budget = 50,
            int minTotalCount = 0,
            int maxTotalCount = 50,
            int maxPlacementAttempts = 50,
            float minPlayerStartDistance = 3.0f,
            Vector3? playerStartPosition = null,
            int physicsLayerMask = ~0,
            QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore,
            bool checkPhysicsCollision = true,
            IReadOnlyList<ItemSpawnGuarantee> guarantees = null)
        {
            Budget = Math.Max(0, budget);
            MinTotalCount = Math.Max(0, minTotalCount);
            MaxTotalCount = Math.Max(MinTotalCount, maxTotalCount);
            MaxPlacementAttempts = Math.Max(1, maxPlacementAttempts);
            MinPlayerStartDistance = Math.Max(0f, minPlayerStartDistance);
            PlayerStartPosition = playerStartPosition;
            PhysicsLayerMask = physicsLayerMask;
            TriggerInteraction = triggerInteraction;
            CheckPhysicsCollision = checkPhysicsCollision;
            Guarantees = guarantees ?? Array.Empty<ItemSpawnGuarantee>();
        }
    }
}
