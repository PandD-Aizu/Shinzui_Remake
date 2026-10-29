using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Application.Title;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.Domain.Entities;
using Shinzui.Domain.Entities.Inventory;
using Shinzui.Domain.ValueObjects.Inventory;
using Shinzui.Domain.ValueObjects.Player;
using Shinzui.Infrastructure.Repositories;
using Shinzui.Infrastructure.SaveSystem;

namespace Shinzui.Tests.SaveSystem
{
    public sealed class SaveSystemTests
    {
        private string _directory;

        /// <summary>実データと独立した保存先を用意する</summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ShinzuiSaveTests", Guid.NewGuid().ToString("N"));
            GameStartRequest.NewGame();
        }

        /// <summary>テスト用の保存データを削除する</summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            GameStartRequest.NewGame();
        }

        /// <summary>通常アイテム、装備、特殊アイテムを両形式で往復する</summary>
        /// <param name="json">JSON形式か</param>
        [TestCase(true), TestCase(false)]
        public void RoundTripPreservesInventoryAndEquipment(bool json)
        {
            var store = new SaveFileStore(_directory, json);
            store.Save(2, Data());
            var loaded = store.Load(2);
            Assert.That(loaded.InventorySaveData.Slots[0].ItemId, Is.EqualTo("stone"));
            Assert.That(loaded.InventorySaveData.Slots[0].Quantity, Is.EqualTo(2));
            Assert.That(loaded.InventorySaveData.Slots[0].SlotIndex, Is.EqualTo(3));
            Assert.That(loaded.InventorySaveData.EquippedSlotIndex, Is.EqualTo(3));
            Assert.That(loaded.SpecialItemSaveData.ItemId, Is.EqualTo("boots"));
            Assert.That(Path.GetExtension(store.GetPath(2)), Is.EqualTo(json ? ".json" : ".bin"));
        }

        /// <summary>上書きと削除が他スロットへ影響しないことを確認する</summary>
        /// <param name="json">JSON形式か</param>
        [TestCase(true), TestCase(false)]
        public void OverwriteAndDeleteAreSlotLocal(bool json)
        {
            var store = new SaveFileStore(_directory, json);
            store.Save(0, Data());
            store.Save(1, Data());
            store.Save(0, new GameSaveData());
            Assert.That(store.Load(0).InventorySaveData.Slots, Is.Empty);
            Assert.That(store.Load(1).InventorySaveData.Slots.Length, Is.EqualTo(1));
            store.Delete(0);
            Assert.That(store.Exists(0), Is.False);
            Assert.That(store.Exists(1), Is.True);
        }

        /// <summary>存在しないデータを仮アイテムに置き換えないことを確認する</summary>
        [Test]
        public void MissingAndInvalidSlotsFail()
        {
            var store = new SaveFileStore(_directory, true);
            Assert.That(store.Exists(0), Is.False);
            Assert.Throws<DirectoryNotFoundException>(() => store.Load(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.GetPath(3));
        }

        /// <summary>破損ファイルをロード成功と扱わないことを確認する</summary>
        /// <param name="json">JSON形式か</param>
        [TestCase(true), TestCase(false)]
        public void CorruptAndUnsupportedFilesFail(bool json)
        {
            var store = new SaveFileStore(_directory, json);
            store.Save(0, Data());
            if (json) File.WriteAllText(store.GetPath(0), "{\"Version\":999}");
            else File.WriteAllBytes(store.GetPath(0), new byte[] { 1, 2, 3, 4 });
            Assert.Throws<InvalidDataException>(() => store.Load(0));
        }

        /// <summary>再開時に持ち物、装備、特殊効果を復元し新規開始時には解除する</summary>
        /// <returns>確認完了タスク</returns>
        [Test]
        public async Task ContinueRestoresLiveStateAndNewGameClearsIt()
        {
            var store = new SaveFileStore(_directory, true);
            store.Save(2, Data());
            var player = new PlayerEntity(new PlayerSpeedStatus(), new PlayerCrouchStatus(), new PlayerStamina());
            var repository = new MemoryInventoryRepository();
            var inventory = new InventoryUseCase(new InventoryEntity(10), new Catalog(), repository, player);
            var special = new SpecialItemUseCase(new SpecialItemEntity(), new Catalog(), repository, player);
            var manager = new SaveManager(inventory, special, store);
            GameStartRequest.Continue(2);
            await manager.InitializeAsync();
            Assert.That(inventory.CreateSaveData().Slots[0].Quantity, Is.EqualTo(2));
            Assert.That(inventory.CreateSaveData().EquippedSlotIndex, Is.EqualTo(3));
            Assert.That(special.CreateSaveData().ItemId, Is.EqualTo("boots"));
            Assert.That(player.SpecialItemModifiers.MoveSpeedMultiplier, Is.EqualTo(1.5f));

            // メモリ更新だけでは確定済みセーブを変更しない
            await special.ClearAsync();
            Assert.That(store.Load(2).SpecialItemSaveData.ItemId, Is.EqualTo("boots"));
            await manager.Save(2);
            Assert.That(store.Load(2).SpecialItemSaveData.ItemId, Is.Empty);
            GameStartRequest.NewGame();
            await manager.InitializeAsync();
            Assert.That(inventory.CreateSaveData().Slots, Is.Empty);
            Assert.That(inventory.CreateSaveData().EquippedSlotIndex, Is.EqualTo(-1));
            Assert.That(special.CreateSaveData().ItemId, Is.Empty);
            Assert.That(player.SpecialItemModifiers.MoveSpeedMultiplier, Is.EqualTo(1f));
        }

        /// <summary>テスト用の持ち物を作る</summary>
        /// <returns>装備と特殊アイテムを含むデータ</returns>
        private static GameSaveData Data() => new GameSaveData
        {
            InventorySaveData = new InventorySaveData
            {
                EquippedSlotIndex = 3,
                Slots = new[] { new InventorySlotSaveData { SlotIndex = 3, ItemId = "stone", Quantity = 2 } }
            },
            SpecialItemSaveData = new SpecialItemSaveData { ItemId = "boots" }
        };

        private sealed class Catalog : IItemCatalog
        {
            /// <summary>テスト用のアイテム定義を返す</summary>
            /// <param name="itemId">アイテムID</param>
            /// <returns>対応するアイテム定義</returns>
            public Task<ItemDefinition> GetItemAsync(string itemId) => Task.FromResult<ItemDefinition>(itemId == "boots"
                ? new SpecialItemDefinition("boots", "靴", "", "", new SpecialItemModifiers(1.5f, 1f, false))
                : new ItemDefinition("stone", "石", "", "", ItemType.Equipment, 99));
        }
    }
}

