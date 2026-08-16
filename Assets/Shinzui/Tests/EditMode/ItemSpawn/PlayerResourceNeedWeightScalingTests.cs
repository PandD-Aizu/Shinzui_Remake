using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using Shinzui.Infrastructure.ItemSpawn;

namespace Shinzui.Tests.ItemSpawn
{
    [TestFixture]
    public class PlayerResourceNeedWeightScalingTests
    {
        private ItemSpawnLotteryService _lotteryService;

        [SetUp]
        public void SetUp()
        {
            _lotteryService = new ItemSpawnLotteryService();
        }

        [Test]
        public void NeedWeightScaling_DynamicallyModulatesSelectionFrequencies()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("Healing_Item", ResourceCategory.Healing, baseWeight: 2.0f),
                new("Ammo_Item", ResourceCategory.Ammo, baseWeight: 2.0f)
            };

            // Healing NeedWeight = 0.5 (final weight 1.0)
            // Ammo NeedWeight = 2.0 (final weight 4.0)
            // Expected ratio: 1.0 / 5.0 = 20% vs 4.0 / 5.0 = 80%
            float NeedProvider(ResourceCategory cat) => cat switch
            {
                ResourceCategory.Healing => 0.5f,
                ResourceCategory.Ammo => 2.0f,
                _ => 1.0f
            };

            var prng = new DeterministicPrng(55555);
            int countHealing = 0;
            int countAmmo = 0;
            const int trials = 10000;

            var emptyItems = new Dictionary<string, int>();
            var emptyCats = new Dictionary<ResourceCategory, int>();

            for (int i = 0; i < trials; i++)
            {
                _lotteryService.TrySelectRandomCandidate(
                    targets,
                    emptyItems,
                    emptyCats,
                    100,
                    NeedProvider,
                    prng,
                    out ItemSpawnTarget selected,
                    out _, out _, out _, out _);

                if (selected.Id == "Healing_Item") countHealing++;
                else if (selected.Id == "Ammo_Item") countAmmo++;
            }

            double ratioHealing = (double)countHealing / trials;
            double ratioAmmo = (double)countAmmo / trials;

            Assert.That(ratioHealing, Is.EqualTo(0.20).Within(0.02), $"Healing ratio: {ratioHealing:P2}");
            Assert.That(ratioAmmo, Is.EqualTo(0.80).Within(0.02), $"Ammo ratio: {ratioAmmo:P2}");
        }

        [Test]
        public void NullNeedWeightProvider_FallsBackToNeutralWeight1()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("Item_A", ResourceCategory.Healing, baseWeight: 2.0f),
                new("Item_B", ResourceCategory.Ammo, baseWeight: 2.0f)
            };

            var prng = new DeterministicPrng(1111);
            int countA = 0;
            int countB = 0;
            const int trials = 10000;

            var emptyItems = new Dictionary<string, int>();
            var emptyCats = new Dictionary<ResourceCategory, int>();

            for (int i = 0; i < trials; i++)
            {
                _lotteryService.TrySelectRandomCandidate(
                    targets,
                    emptyItems,
                    emptyCats,
                    100,
                    null, // null provider
                    prng,
                    out ItemSpawnTarget selected,
                    out _, out float needWeight, out _, out _);

                Assert.That(needWeight, Is.EqualTo(1.0f));
                if (selected.Id == "Item_A") countA++;
                else if (selected.Id == "Item_B") countB++;
            }

            double ratioA = (double)countA / trials;
            double ratioB = (double)countB / trials;

            // ニュートラル時は 50% : 50%
            Assert.That(ratioA, Is.EqualTo(0.50).Within(0.02));
            Assert.That(ratioB, Is.EqualTo(0.50).Within(0.02));
        }

        [Test]
        public void PlayerResourceNeedWeightAdapter_SafeNeutralFallback()
        {
            var adapter = new PlayerResourceNeedWeightAdapter(null);
            Assert.That(adapter.GetNeedWeight(ResourceCategory.Healing), Is.EqualTo(1.0f));
            Assert.That(adapter.GetNeedWeight(ResourceCategory.Ammo), Is.EqualTo(1.0f));
            Assert.That(adapter.GetNeedWeight(ResourceCategory.LightResource), Is.EqualTo(1.0f));
        }
    }
}
