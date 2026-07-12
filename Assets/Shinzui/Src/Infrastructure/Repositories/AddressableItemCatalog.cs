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
            { "stone", new ItemDefinition("stone", "Stone", "A stone.", "Sprite/Medicine", ItemType.Equipment, 99) },
            { "potion_red", new ItemDefinition("potion_red", "Red Potion", "A red potion that restores health.", "Sprite/Medicine", ItemType.Consumable, 99) },
            { "potion_blue", new ItemDefinition("potion_blue", "Blue Potion", "A blue potion that restores magic energy.", "Sprite/Medicine", ItemType.Consumable, 99) },
            { "potion_stamina", new ItemDefinition("potion_stamina", "Stamina Potion", "A stamina potion that restores stamina.", "Sprite/Medicine", ItemType.Consumable, 99) },
            { "sword_iron", new ItemDefinition("sword_iron", "Iron Sword", "A common sword made of iron.", "Sprite/Medicine", ItemType.Equipment, 1) },
            { "key_old", new ItemDefinition("key_old", "Old Key", "A rusty old iron key.", "Sprite/Medicine", ItemType.Key, 1) },
            { "FlashlightBattery", new ItemDefinition("FlashlightBattery", "Spare Battery", "A spare battery for the flashlight.", "Sprite/Medicine", ItemType.Consumable, 99) },
            { "special_death_charm", new SpecialItemDefinition("special_death_charm", "Death Charm", "Prevents death once, then disappears.", "Sprite/Medicine", new SpecialItemModifiers(1.0f, 1.0f, true)) },
            { "special_speed_boots", new SpecialItemDefinition("special_speed_boots", "Speed Boots", "Increases movement speed while held.", "Sprite/Medicine", new SpecialItemModifiers(1.2f, 1.0f, false)) },
            { "special_stamina_core", new SpecialItemDefinition("special_stamina_core", "Stamina Core", "Increases stamina recovery speed while held.", "Sprite/Medicine", new SpecialItemModifiers(1.0f, 1.5f, false)) }
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
