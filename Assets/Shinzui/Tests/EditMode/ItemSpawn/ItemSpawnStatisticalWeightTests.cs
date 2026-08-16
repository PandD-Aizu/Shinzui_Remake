using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Tests.ItemSpawn
{
    [TestFixture]
    public class ItemSpawnStatisticalWeightTests
    {
        private ItemSpawnLotteryService _lotteryService;

        [SetUp]
        public void SetUp()
        {
            _lotteryService = new ItemSpawnLotteryService();
        }

        [Test]
        public void TwoItems_WeightRatio1To3_MatchesStatisticalDistribution()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("Item_1", ResourceCategory.Utility, baseWeight: 1.0f),
                new("Item_3", ResourceCategory.Utility, baseWeight: 3.0f)
            };

            var prng = new DeterministicPrng(42);
            int countItem1 = 0;
            int countItem3 = 0;
            const int trials = 10000;

            var emptyItems = new Dictionary<string, int>();
            var emptyCats = new Dictionary<ResourceCategory, int>();

            for (int i = 0; i < trials; i++)
            {
                _lotteryService.TrySelectRandomCandidate(
                    targets,
                    emptyItems,
                    emptyCats,
                    1000,
                    _ => 1.0f,
                    prng,
                    out ItemSpawnTarget selected,
                    out _, out _, out _, out _);

                if (selected.Id == "Item_1") countItem1++;
                else if (selected.Id == "Item_3") countItem3++;
            }

            double ratio1 = (double)countItem1 / trials;
            double ratio3 = (double)countItem3 / trials;

            // 期待値: Item_1 = 25%, Item_3 = 75%
            // 許容誤差 ±2.0% (0.02)
            Assert.That(ratio1, Is.EqualTo(0.25).Within(0.02), $"Item_1 ratio was {ratio1:P2}");
            Assert.That(ratio3, Is.EqualTo(0.75).Within(0.02), $"Item_3 ratio was {ratio3:P2}");
        }

        [Test]
        public void ThreeItems_WeightRatio10_20_70_MatchesStatisticalDistribution()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("Item_10", ResourceCategory.Healing, baseWeight: 10.0f),
                new("Item_20", ResourceCategory.Ammo, baseWeight: 20.0f),
                new("Item_70", ResourceCategory.Utility, baseWeight: 70.0f)
            };

            var prng = new DeterministicPrng(98765);
            int count10 = 0;
            int count20 = 0;
            int count70 = 0;
            const int trials = 20000;

            var emptyItems = new Dictionary<string, int>();
            var emptyCats = new Dictionary<ResourceCategory, int>();

            for (int i = 0; i < trials; i++)
            {
                _lotteryService.TrySelectRandomCandidate(
                    targets,
                    emptyItems,
                    emptyCats,
                    1000,
                    _ => 1.0f,
                    prng,
                    out ItemSpawnTarget selected,
                    out _, out _, out _, out _);

                if (selected.Id == "Item_10") count10++;
                else if (selected.Id == "Item_20") count20++;
                else if (selected.Id == "Item_70") count70++;
            }

            double r10 = (double)count10 / trials;
            double r20 = (double)count20 / trials;
            double r70 = (double)count70 / trials;

            Assert.That(r10, Is.EqualTo(0.10).Within(0.015), $"Item_10 ratio: {r10:P2}");
            Assert.That(r20, Is.EqualTo(0.20).Within(0.015), $"Item_20 ratio: {r20:P2}");
            Assert.That(r70, Is.EqualTo(0.70).Within(0.015), $"Item_70 ratio: {r70:P2}");
        }
    }
}
