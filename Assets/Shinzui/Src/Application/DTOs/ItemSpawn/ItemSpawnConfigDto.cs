using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.ItemSpawn;

namespace Shinzui.Application.DTOs.ItemSpawn
{
    [Serializable]
    public class ItemSpawnConfigDto
    {
        public int Seed = 2777;
        public int Budget = 100;
        public int MinTotalSpawns = 1;
        public int MaxTotalSpawns = 50;
        public int MaxPlacementAttemptsPerItem = 30;
        public int MaxTotalPlacementAttempts = 300;
        public float PlayerStartMinDistance = 5.0f;
        public float PlayerStartX = 0f;
        public float PlayerStartY = 0f;
        public float PlayerStartZ = 0f;
        public List<GuaranteedSpawnRuleDto> GuaranteedRules = new();

        public ItemSpawnRunConfig ToDomain()
        {
            var rules = new List<GuaranteedSpawnRule>();
            if (GuaranteedRules != null)
            {
                foreach (var r in GuaranteedRules)
                {
                    if (r != null && !string.IsNullOrEmpty(r.ItemId))
                    {
                        rules.Add(r.ToDomain());
                    }
                }
            }

            return new ItemSpawnRunConfig(
                Seed,
                Budget,
                MinTotalSpawns,
                MaxTotalSpawns,
                MaxPlacementAttemptsPerItem,
                MaxTotalPlacementAttempts,
                PlayerStartMinDistance,
                new SpawnVector3(PlayerStartX, PlayerStartY, PlayerStartZ),
                rules);
        }
    }
}
