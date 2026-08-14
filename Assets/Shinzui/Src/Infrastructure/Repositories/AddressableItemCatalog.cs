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
            { "potion_red", new ItemDefinition("potion_red", "赤色のポーション", "体力を回復する赤いポーション。", "Sprite/Medicine", ItemType.Consumable, 99) },
            { "石", new ItemDefinition("stone", "石", "ただの石", "Sprite/Medicine", ItemType.Equipment, 99) },
            { "スタミナ剤", new ItemDefinition("potion_stamina", "スタミナ剤", "スタミナが一時的に減少しなくなる", "Sprite/Medicine", ItemType.Consumable, 99) },
            { "牡丹餅", new ItemDefinition("potion_botamochi", "牡丹餅", "スタミナを回復する", "Sprite/Medicine", ItemType.Consumable, 99) },
            { "鍵", new ItemDefinition("key_old", "古びた鍵", "トンネル内のどこかで使用できる鍵", "Sprite/Medicine", ItemType.Key, 1) },
            { "乾電池", new ItemDefinition("FlashlightBattery", "乾電池", "使い捨て電池、ストロボで使用する", "Sprite/Medicine", ItemType.Consumable, 99) },
            { "マッチ棒", new ItemDefinition("MatchStick", "マッチ棒", "火を灯すための道具、何かを燃やせそうだ", "Sprite/Medicine", ItemType.Equipment, 99) },
            
            { "古びたお守り", new SpecialItemDefinition("special_death_charm", "古びたお守り", "一回だけ死を防ぐ", "Sprite/Medicine", new SpecialItemModifiers(1.0f, 1.0f, true)) },
            { "古びた靴", new SpecialItemDefinition("special_speed_boots", "古びた靴", "移動速度が上昇する", "Sprite/Medicine", new SpecialItemModifiers(1.2f, 1.0f, false)) },
            { "古びた水筒", new SpecialItemDefinition("special_stamina_core", "古びた水筒", "スタミナ回復速度が上昇する", "Sprite/Medicine", new SpecialItemModifiers(1.0f, 1.5f, false)) }
        };

        public Task<ItemDefinition> GetItemAsync(string itemId)
        {
            if (_itemDatabase.TryGetValue(itemId, out var item))
            {
                return Task.FromResult(item);
            }

            foreach (var definition in _itemDatabase.Values)
            {
                if (definition.Id == itemId || definition.Name == itemId)
                {
                    return Task.FromResult(definition);
                }
            }

            return Task.FromResult<ItemDefinition>(null);
        }
    }
}
