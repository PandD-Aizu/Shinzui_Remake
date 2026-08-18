using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using UnityEngine;

namespace Shinzui.Application.DTOs.ItemSpawn
{
    /// <summary>
    /// アイテム生成定義のDTO
    /// </summary>
    public sealed class ItemSpawnDefinitionDto
    {
        public string Id { get; set; }
        public float BaseWeight { get; set; } = 1.0f;
        public ResourceCategory Category { get; set; } = ResourceCategory.Utility;
        public int SpawnCost { get; set; } = 1;
        public int MinCount { get; set; } = 0;
        public int MaxCount { get; set; } = 99;
        public int CategoryMinCount { get; set; } = 0;
        public int CategoryMaxCount { get; set; } = 99;
        public float MinDistance { get; set; } = 1.0f;
        public bool IsNormalRandomCandidate { get; set; } = true;
        public Vector3 BoundsSize { get; set; } = new Vector3(0.5f, 0.5f, 0.5f);
        public float HeightOffset { get; set; } = 0f;
        public PlacementRotationMode RotationMode { get; set; } = PlacementRotationMode.AlignWithNormalAndRandomYaw;
        public PlacementCollisionShape CollisionShape { get; set; } = PlacementCollisionShape.Box;
        public GameObject Prefab { get; set; }
    }
}
