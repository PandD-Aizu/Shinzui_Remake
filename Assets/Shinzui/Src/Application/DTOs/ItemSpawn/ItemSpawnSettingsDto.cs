using System.Collections.Generic;
using UnityEngine;

namespace Shinzui.Application.DTOs.ItemSpawn
{
    /// <summary>
    /// アイテム生成実行設定のDTO
    /// </summary>
    public sealed class ItemSpawnSettingsDto
    {
        public int Budget { get; set; } = 50;
        public int MinTotalCount { get; set; } = 0;
        public int MaxTotalCount { get; set; } = 50;
        public int MaxPlacementAttempts { get; set; } = 50;
        public float MinPlayerStartDistance { get; set; } = 3.0f;
        public Vector3? PlayerStartPosition { get; set; }
        public int PhysicsLayerMask { get; set; } = ~0;
        public QueryTriggerInteraction TriggerInteraction { get; set; } = QueryTriggerInteraction.Ignore;
        public bool CheckPhysicsCollision { get; set; } = true;
        public List<ItemSpawnGuaranteeDto> Guarantees { get; set; } = new();
    }
}
