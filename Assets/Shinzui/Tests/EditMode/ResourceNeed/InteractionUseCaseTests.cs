using System.Threading.Tasks;
using NUnit.Framework;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Application.UseCases.Interaction;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.Domain.Entities;
using Shinzui.Domain.Entities.Inventory;
using Shinzui.Domain.ValueObjects.Inventory;
using Shinzui.Domain.ValueObjects.Player;
using Shinzui.Infrastructure.Repositories;

namespace Shinzui.Tests
{
    public sealed class InteractionUseCaseTests
    {
        [Test]
        public async Task SpiderWeb_WithEquippedMatch_ConsumesMatchAndSucceeds()
        {
            var catalog = new AddressableItemCatalog();
            var repository = new MemoryRepository();
            var player = CreatePlayer();
            var inventory = new InventoryEntity(4);
            var inventoryUseCase = new InventoryUseCase(inventory, catalog, repository, player);
            var specialUseCase = new SpecialItemUseCase(
                new SpecialItemEntity(),
                catalog,
                repository,
                player);
            var useCase = new InteractionUseCase(inventoryUseCase, specialUseCase, catalog);

            Assert.That(await inventoryUseCase.AddItemAsync("match_stick", 1), Is.True);
            inventoryUseCase.EquipItem(0);

            bool succeeded = await useCase.InteractAsync("spider_web", "");

            Assert.That(succeeded, Is.True);
            Assert.That(repository.LastInventorySave, Is.Not.Null);
            Assert.That(repository.LastInventorySave.Slots, Is.Empty);
        }

        [Test]
        public async Task SpiderWeb_WithoutEquippedMatch_DoesNotBurn()
        {
            var catalog = new AddressableItemCatalog();
            var repository = new MemoryRepository();
            var player = CreatePlayer();
            var inventoryUseCase = new InventoryUseCase(
                new InventoryEntity(4),
                catalog,
                repository,
                player);
            var specialUseCase = new SpecialItemUseCase(
                new SpecialItemEntity(),
                catalog,
                repository,
                player);
            var useCase = new InteractionUseCase(inventoryUseCase, specialUseCase, catalog);

            bool succeeded = await useCase.InteractAsync("spider_web", "");

            Assert.That(succeeded, Is.False);
        }

        private static PlayerEntity CreatePlayer()
        {
            return new PlayerEntity(
                new PlayerSpeedStatus(),
                new PlayerCrouchStatus(),
                new PlayerStamina());
        }

        private sealed class MemoryRepository : IInventoryRepository, ISpecialItemRepository
        {
            public InventorySaveData LastInventorySave { get; private set; }

            public Task SaveInventoryAsync(InventorySaveData data)
            {
                LastInventorySave = data;
                return Task.CompletedTask;
            }

            public Task<InventorySaveData> LoadInventoryAsync() =>
                Task.FromResult<InventorySaveData>(null);

            public Task SaveSpecialItemAsync(SpecialItemSaveData data) => Task.CompletedTask;

            public Task<SpecialItemSaveData> LoadSpecialItemAsync() =>
                Task.FromResult<SpecialItemSaveData>(null);
        }
    }
}
