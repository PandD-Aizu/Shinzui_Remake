using System;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.Inventory;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Infrastructure.Repositories
{
    public readonly struct CanonicalItemEntry
    {
        public ItemDefinition Definition { get; }
        public string AlternateLookupKey { get; }
        public ResourceCategory DefaultCategory { get; }
        public int DefaultUnitCount { get; }

        public CanonicalItemEntry(
            ItemDefinition definition,
            string alternateLookupKey,
            ResourceCategory defaultCategory,
            int defaultUnitCount = 1)
        {
            Definition = definition;
            AlternateLookupKey = alternateLookupKey;
            DefaultCategory = defaultCategory;
            DefaultUnitCount = Math.Max(1, defaultUnitCount);
        }
    }

    /// <summary>
    /// 現行ゲームに実在するアイテム定義および既定リソース分類の正規レジストリ（DRYな唯一の定義元）
    /// </summary>
    public static class CanonicalCatalogRegistry
    {
        public static readonly IReadOnlyList<CanonicalItemEntry> Entries = new CanonicalItemEntry[]
        {
            new(
                new ItemDefinition("potion_red", "赤色のポーション", "体力を回復する赤いポーション。", "Sprite/Medicine", ItemType.Consumable, 99),
                "potion_red",
                ResourceCategory.Healing,
                1
            ),
            new(
                new ItemDefinition("stone", "石", "ただの石", "Sprite/Medicine", ItemType.Equipment, 99),
                "石",
                ResourceCategory.Utility,
                1
            ),
            new(
                new ItemDefinition("potion_stamina", "スタミナ剤", "スタミナが一時的に減少しなくなる", "Sprite/Medicine", ItemType.Consumable, 99),
                "スタミナ剤",
                ResourceCategory.Utility,
                1
            ),
            new(
                new ItemDefinition("potion_botamochi", "牡丹餅", "スタミナを回復する", "Sprite/Medicine", ItemType.Consumable, 99),
                "牡丹餅",
                ResourceCategory.Utility,
                1
            ),
            new(
                new ItemDefinition("key_old", "古びた鍵", "トンネル内のどこかで使用できる鍵", "Sprite/Medicine", ItemType.Key, 1),
                "鍵",
                ResourceCategory.Rare,
                1
            ),
            new(
                new ItemDefinition("FlashlightBattery", "乾電池", "使い捨て電池、ストロボで使用する", "Sprite/Medicine", ItemType.Consumable, 99),
                "乾電池",
                ResourceCategory.LightResource,
                1
            ),
            new(
                new SpecialItemDefinition("special_death_charm", "古びたお守り", "一回だけ死を防ぐ", "Sprite/Medicine", new SpecialItemModifiers(1.0f, 1.0f, true)),
                "古びたお守り",
                ResourceCategory.Rare,
                1
            ),
            new(
                new SpecialItemDefinition("special_speed_boots", "古びた靴", "移動速度が上昇する", "Sprite/Medicine", new SpecialItemModifiers(1.2f, 1.0f, false)),
                "古びた靴",
                ResourceCategory.Rare,
                1
            ),
            new(
                new SpecialItemDefinition("special_stamina_core", "古びた水筒", "スタミナ回復速度が上昇する", "Sprite/Medicine", new SpecialItemModifiers(1.0f, 1.5f, false)),
                "古びた水筒",
                ResourceCategory.Rare,
                1
            )
        };

        public static Dictionary<string, ItemDefinition> CreateCatalogDatabase()
        {
            var dict = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Entries)
            {
                if (!string.IsNullOrEmpty(entry.AlternateLookupKey))
                {
                    dict[entry.AlternateLookupKey] = entry.Definition;
                }
                if (!string.IsNullOrEmpty(entry.Definition.Id) && !dict.ContainsKey(entry.Definition.Id))
                {
                    dict[entry.Definition.Id] = entry.Definition;
                }
            }
            return dict;
        }

        public static bool TryGetDefaultCategory(string itemId, out ResourceCategory category)
        {
            category = default;
            if (string.IsNullOrEmpty(itemId)) return false;

            foreach (var entry in Entries)
            {
                if (string.Equals(entry.Definition.Id, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    category = entry.DefaultCategory;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetDefaultUnitCount(string itemId, out int unitCount)
        {
            unitCount = 1;
            if (string.IsNullOrEmpty(itemId)) return false;

            foreach (var entry in Entries)
            {
                if (string.Equals(entry.Definition.Id, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    unitCount = entry.DefaultUnitCount;
                    return true;
                }
            }

            return false;
        }
    }
}
