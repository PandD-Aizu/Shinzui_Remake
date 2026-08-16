using System;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// 通常抽選に先立って優先配置される最低保証（必須生成）ルール。
    /// </summary>
    public record GuaranteedSpawnRule
    {
        public string ItemId { get; init; }
        public int Count { get; init; }

        public GuaranteedSpawnRule(string itemId, int count = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("GuaranteedSpawnRule ItemId cannot be null or empty.", nameof(itemId));
            }

            ItemId = itemId;
            Count = Math.Max(1, count);
        }
    }
}
