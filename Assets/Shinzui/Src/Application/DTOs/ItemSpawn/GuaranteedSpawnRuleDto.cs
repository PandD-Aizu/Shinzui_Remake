using System;
using Shinzui.Domain.ValueObjects.ItemSpawn;

namespace Shinzui.Application.DTOs.ItemSpawn
{
    [Serializable]
    public class GuaranteedSpawnRuleDto
    {
        public string ItemId;
        public int Count = 1;

        public GuaranteedSpawnRule ToDomain()
        {
            return new GuaranteedSpawnRule(ItemId, Count);
        }
    }
}
