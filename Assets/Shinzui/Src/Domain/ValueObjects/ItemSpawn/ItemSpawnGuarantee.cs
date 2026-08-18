using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// 最低保証ルール定義（特定アイテムIDまたは特定カテゴリの確定生成要求）
    /// </summary>
    public sealed class ItemSpawnGuarantee
    {
        public string ItemId { get; }
        public ResourceCategory? Category { get; }
        public int GuaranteedCount { get; }

        public ItemSpawnGuarantee(string itemId, int guaranteedCount)
        {
            ItemId = itemId;
            Category = null;
            GuaranteedCount = System.Math.Max(1, guaranteedCount);
        }

        public ItemSpawnGuarantee(ResourceCategory category, int guaranteedCount)
        {
            ItemId = null;
            Category = category;
            GuaranteedCount = System.Math.Max(1, guaranteedCount);
        }
    }
}
