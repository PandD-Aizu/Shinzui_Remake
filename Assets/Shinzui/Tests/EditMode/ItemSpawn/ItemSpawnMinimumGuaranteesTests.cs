using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Tests.ItemSpawn
{
    [TestFixture]
    public class ItemSpawnMinimumGuaranteesTests
    {
        private ItemSpawnPlanner _planner;
        private SpawnSurfaceData _surface;

        [SetUp]
        public void SetUp()
        {
            _planner = new ItemSpawnPlanner();
            _surface = new SpawnSurfaceData(
                "BigSurface",
                1.0f,
                new[]
                {
                    new SpawnSurfaceTriangle(
                        new SpawnVector3(0f, 0f, 0f),
                        new SpawnVector3(100f, 0f, 0f),
                        new SpawnVector3(0f, 100f, 0f),
                        1.0f, 1.0f, "BigSurface", 0)
                });
        }

        [Test]
        public void GuaranteedItems_ArePlacedPriorToNormalRandomItems()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("NormalUtility", ResourceCategory.Utility, baseWeight: 10.0f, spawnCost: 1),
                new("KeyItemAlpha", ResourceCategory.Rare, baseWeight: 1.0f, spawnCost: 2, isNormalRandomCandidate: false),
                new("KeyItemBeta", ResourceCategory.Rare, baseWeight: 1.0f, spawnCost: 3, isNormalRandomCandidate: false)
            };

            var guarantees = new List<GuaranteedSpawnRule>
            {
                new("KeyItemAlpha", 1),
                new("KeyItemBeta", 1)
            };

            var config = new ItemSpawnRunConfig(
                seed: 42,
                budget: 20,
                maxTotalSpawns: 10,
                guaranteedRules: guarantees,
                playerStartMinDistance: 0f);

            ItemSpawnRunResult result = _planner.PlanSpawns(config, targets, new[] { _surface });

            Assert.That(result.TotalSpawnedCount, Is.GreaterThanOrEqualTo(2));
            // 最初の2つが最低保証アイテム
            Assert.That(result.SpawnedItems[0].ItemId, Is.EqualTo("KeyItemAlpha"));
            Assert.That(result.SpawnedItems[1].ItemId, Is.EqualTo("KeyItemBeta"));

            // 3番目以降は通常ランダムアイテム
            for (int i = 2; i < result.TotalSpawnedCount; i++)
            {
                Assert.That(result.SpawnedItems[i].ItemId, Is.EqualTo("NormalUtility"));
            }
        }

        [Test]
        public void NonNormalCandidate_CannotBeSpawnedWithoutGuarantee()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("NormalItem", ResourceCategory.Utility, baseWeight: 1.0f, spawnCost: 1, isNormalRandomCandidate: true),
                new("SecretRelic", ResourceCategory.Rare, baseWeight: 100.0f, spawnCost: 1, isNormalRandomCandidate: false)
            };

            var config = new ItemSpawnRunConfig(
                seed: 9999,
                budget: 20,
                maxTotalSpawns: 10,
                guaranteedRules: null, // 最低保証なし
                playerStartMinDistance: 0f);

            ItemSpawnRunResult result = _planner.PlanSpawns(config, targets, new[] { _surface });

            Assert.That(result.TotalSpawnedCount, Is.GreaterThan(0));
            foreach (var item in result.SpawnedItems)
            {
                Assert.That(item.ItemId, Is.EqualTo("NormalItem"));
                Assert.That(item.ItemId, Is.Not.EqualTo("SecretRelic"));
            }
        }

        [Test]
        public void MultipleGuarantees_SafelySkipsWhenBudgetIsInsufficient()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("GuaranteeA", ResourceCategory.Utility, spawnCost: 5, isNormalRandomCandidate: false),
                new("GuaranteeB", ResourceCategory.Utility, spawnCost: 10, isNormalRandomCandidate: false)
            };

            var guarantees = new List<GuaranteedSpawnRule>
            {
                new("GuaranteeA", 1), // コスト5 -> 残予算7
                new("GuaranteeB", 1)  // コスト10 > 残予算7 -> スキップ
            };

            var config = new ItemSpawnRunConfig(
                seed: 1234,
                budget: 12,
                guaranteedRules: guarantees,
                playerStartMinDistance: 0f);

            ItemSpawnRunResult result = _planner.PlanSpawns(config, targets, new[] { _surface });

            Assert.That(result.TotalSpawnedCount, Is.EqualTo(1));
            Assert.That(result.SpawnedItems[0].ItemId, Is.EqualTo("GuaranteeA"));
            Assert.That(result.TotalCostSpent, Is.EqualTo(5));
            Assert.That(result.RemainingBudget, Is.EqualTo(7));
            Assert.That(result.SkipReasons.Count, Is.GreaterThan(0));
            Assert.That(result.SkipReasons[0], Does.Contain("GuaranteeB"));
        }
    }
}
