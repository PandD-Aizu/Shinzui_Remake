using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Application.DTOs.ItemSpawn
{
    /// <summary>
    /// 最低保証ルールのDTO
    /// </summary>
    public sealed class ItemSpawnGuaranteeDto
    {
        public string ItemId { get; set; }
        public ResourceCategory? Category { get; set; }
        public int GuaranteedCount { get; set; } = 1;
    }
}
