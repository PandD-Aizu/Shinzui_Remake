using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Tests.ItemSpawn
{
    [TestFixture]
    public class ItemSpawnBudgetEnforcementTests
    {
        private ItemSpawnPlanner _planner;
        private SpawnSurfaceData _dummySurface;

        [SetUp]
        public void SetUp()
        {
            _planner = new ItemSpawnPlanner();
            _dummySurface = new SpawnSurfaceData(
                "Surface1",
                1.0f,
                new[]
                {
                    new SpawnSurfaceTriangle(
                        new SpawnVector3(0f, 0f, 0f),
                        new SpawnVector3(50f, 0f, 0f),
                        new SpawnVector3(0f, 50f, 0f),
                        1.0f, 1.0f, "Surface1", 0)
                });
        }

        [Test]
        public void StrictBudgetLimit_TotalCostNeverExceedsBudget()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("Cost3_Item", ResourceCategory.Utility, baseWeight: 1.0f, spawnCost: 3)
            };

            var config = new ItemSpawnRunConfig(
                seed: 123,
                budget: 10,
                minTotalSpawns: 0,
                maxTotalSpawns: 50,
                playerStartMinDistance: 0f);

            ItemSpawnRunResult result = _planner.PlanSpawns(config, targets, new[] { _dummySurface });

            // 予算10でコスト3のアイテム: 最大3個生成可能（コスト9消費、残予算1）
            Assert.That(result.TotalSpawnedCount, Is.EqualTo(3));
            Assert.That(result.TotalCostSpent, Is.EqualTo(9));
            Assert.That(result.RemainingBudget, Is.EqualTo(1));
            Assert.That(result.TotalCostSpent, Is.LessThanOrEqualTo(10));
        }

        [Test]
        public void ItemExceedingRemainingBudget_IsExcludedFromCandidateSelection()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("Cheap_Item", ResourceCategory.Utility, baseWeight: 1.0f, spawnCost: 2),
                new("Expensive_Item", ResourceCategory.Rare, baseWeight: 10.0f, spawnCost: 20)
            };

            var config = new ItemSpawnRunConfig(
                seed: 456,
                budget: 5,
                playerStartMinDistance: 0f);

            ItemSpawnRunResult result = _planner.PlanSpawns(config, targets, new[] { _dummySurface });

            // 予算5: Cheap_Item (コスト2) が2個生成されコスト4、残1。Expensive_Itemは一度も選ばれない
            Assert.That(result.TotalSpawnedCount, Is.EqualTo(2));
            Assert.That(result.TotalCostSpent, Is.EqualTo(4));
            Assert.That(result.RemainingBudget, Is.EqualTo(1));

            foreach (var item in result.SpawnedItems)
            {
                Assert.That(item.ItemId, Is.EqualTo("Cheap_Item"));
                Assert.That(item.ItemId, Is.Not.EqualTo("Expensive_Item"));
            }
        }

        [Test]
        public void GuaranteedRuleExceedingBudget_IsSafelySkippedWithDebugReason()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("SuperKey", ResourceCategory.Rare, baseWeight: 1.0f, spawnCost: 50, isNormalRandomCandidate: false)
            };

            var guaranteedRules = new List<GuaranteedSpawnRule>
            {
                new("SuperKey", 1)
            };

            var config = new ItemSpawnRunConfig(
                seed: 789,
                budget: 10, // 予算10に対しSuperKeyは50
                guaranteedRules: guaranteedRules,
                playerStartMinDistance: 0f);

            ItemSpawnRunResult result = _planner.PlanSpawns(config, targets, new[] { _dummySurface });

            Assert.That(result.TotalSpawnedCount, Is.EqualTo(0));
            Assert.That(result.TotalCostSpent, Is.EqualTo(0));
            Assert.That(result.RemainingBudget, Is.EqualTo(10));
            Assert.That(result.SkipReasons.Count, Is.GreaterThan(0));
            Assert.That(result.SkipReasons[0], Does.Contain("exceeds remaining budget"));
        }

        [Test]
        public void ZeroBudget_SpawnsNothingAndTerminatesSafely()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("Item", ResourceCategory.Utility, spawnCost: 1)
            };

            var config = new ItemSpawnRunConfig(seed: 1, budget: 0, playerStartMinDistance: 0f);
            ItemSpawnRunResult result = _planner.PlanSpawns(config, targets, new[] { _dummySurface });

            Assert.That(result.TotalSpawnedCount, Is.EqualTo(0));
            Assert.That(result.RemainingBudget, Is.EqualTo(0));
        }
    }
}
