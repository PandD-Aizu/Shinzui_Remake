using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using UnityEngine;

namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// アイテム配置検証時にバリデータへ渡されるコンテキスト情報
    /// </summary>
    public readonly struct SpawnValidationContext
    {
        public ItemSpawnDefinition Candidate { get; }
        public Vector3 ProposedPosition { get; }
        public Quaternion ProposedRotation { get; }
        public SpawnSurfaceTriangle SurfaceTriangle { get; }
        public IReadOnlyList<SpawnedItemData> AlreadySpawnedItems { get; }
        public ItemSpawnConfig Config { get; }
        public int AttemptIndex { get; }

        public SpawnValidationContext(
            ItemSpawnDefinition candidate,
            Vector3 proposedPosition,
            Quaternion proposedRotation,
            SpawnSurfaceTriangle surfaceTriangle,
            IReadOnlyList<SpawnedItemData> alreadySpawnedItems,
            ItemSpawnConfig config,
            int attemptIndex)
        {
            Candidate = candidate;
            ProposedPosition = proposedPosition;
            ProposedRotation = proposedRotation;
            SurfaceTriangle = surfaceTriangle;
            AlreadySpawnedItems = alreadySpawnedItems;
            Config = config;
            AttemptIndex = attemptIndex;
        }
    }
}
