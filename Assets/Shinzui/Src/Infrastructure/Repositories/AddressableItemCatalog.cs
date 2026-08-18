using System.Collections.Generic;
using System.Threading.Tasks;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Domain.ValueObjects.Inventory;

namespace Shinzui.Infrastructure.Repositories
{
    public class AddressableItemCatalog : IItemCatalog
    {
        private readonly Dictionary<string, ItemDefinition> _itemDatabase = CanonicalCatalogRegistry.CreateCatalogDatabase();

        public Task<ItemDefinition> GetItemAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return Task.FromResult<ItemDefinition>(null);
            }

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
