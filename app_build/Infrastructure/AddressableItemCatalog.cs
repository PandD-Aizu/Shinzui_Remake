using System.Collections.Generic;
using System.Threading.Tasks;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Domain.ValueObjects.Inventory;

namespace Shinzui.Infrastructure.Repositories
{
    public class AddressableItemCatalog : IItemCatalog
    {
        private readonly Dictionary<string, ItemDefinition> _itemDatabase = new()
        {
            { "potion_red", new ItemDefinition("potion_red", "赤色のポーション", "体力を回復する赤いポーション。", "Icon_Potion_Red", ItemType.Consumable, 99) },
            { "potion_blue", new ItemDefinition("potion_blue", "青色のポーション", "魔力を回復する青いポーション。", "Icon_Potion_Blue", ItemType.Consumable, 99) },
            { "sword_iron", new ItemDefinition("sword_iron", "鉄の剣", "ありふれた鉄製の剣。", "Icon_Sword_Iron", ItemType.Equipment, 1) },
            { "key_old", new ItemDefinition("key_old", "古びた鍵", "錆びついた古い鉄の鍵。", "Icon_Key_Old", ItemType.Key, 1) }
        };

        public Task<ItemDefinition> GetItemAsync(string itemId)
        {
            if (_itemDatabase.TryGetValue(itemId, out var item))
            {
                return Task.FromResult(item);
            }
            return Task.FromResult<ItemDefinition>(null);
        }
    }
}
