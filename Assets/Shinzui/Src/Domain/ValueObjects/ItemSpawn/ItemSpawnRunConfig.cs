using System;
using System.Collections.Generic;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// アイテム生成実行時のドメイン設定値オブジェクト。
    /// </summary>
    public record ItemSpawnRunConfig
    {
        public int Seed { get; init; }
        public int Budget { get; init; }
        public int MinTotalSpawns { get; init; }
        public int MaxTotalSpawns { get; init; }
        public int MaxPlacementAttemptsPerItem { get; init; }
        public int MaxTotalPlacementAttempts { get; init; }
        public float PlayerStartMinDistance { get; init; }
        public SpawnVector3 PlayerStartPosition { get; init; }
        public IReadOnlyList<GuaranteedSpawnRule> GuaranteedRules { get; init; }

        public ItemSpawnRunConfig(
            int seed,
            int budget = 100,
            int minTotalSpawns = 1,
            int maxTotalSpawns = 50,
            int maxPlacementAttemptsPerItem = 30,
            int maxTotalPlacementAttempts = 300,
            float playerStartMinDistance = 5.0f,
            SpawnVector3? playerStartPosition = null,
            IReadOnlyList<GuaranteedSpawnRule> guaranteedRules = null)
        {
            Seed = seed;
            Budget = Math.Max(0, budget);
            MinTotalSpawns = Math.Max(0, minTotalSpawns);
            MaxTotalSpawns = Math.Max(MinTotalSpawns, maxTotalSpawns);
            MaxPlacementAttemptsPerItem = Math.Max(1, maxPlacementAttemptsPerItem);
            MaxTotalPlacementAttempts = Math.Max(MaxPlacementAttemptsPerItem, maxTotalPlacementAttempts);
            PlayerStartMinDistance = Math.Max(0f, playerStartMinDistance);
            PlayerStartPosition = playerStartPosition ?? SpawnVector3.Zero;
            GuaranteedRules = guaranteedRules ?? Array.Empty<GuaranteedSpawnRule>();
        }
    }
}
