namespace Shinzui.Domain.ValueObjects.Inventory
{
    public enum ItemType
    {
        Consumable, // 消費アイテム
        Equipment,  // 装備品
        Special,    // 特殊アイテム
        Story,      // ストーリーアイテム
        Key,        // 鍵・重要アイテム
    }

    public record ItemDefinition
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public string IconAssetAddress { get; init; } // Addressables のアドレスキー等
        public ItemType Type { get; init; }
        public int MaxStackSize { get; init; } = 99;
        public bool IsConsumable => Type == ItemType.Consumable;

        public ItemDefinition() { }

        public ItemDefinition(
            string id,
            string name,
            string description,
            string iconAssetAddress,
            ItemType type,
            int maxStackSize)
        {
            Id = id;
            Name = name;
            Description = description;
            IconAssetAddress = iconAssetAddress;
            Type = type;
            MaxStackSize = maxStackSize;
        }
    }
}
