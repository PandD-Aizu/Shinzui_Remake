using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using UnityEngine;

namespace Shinzui.Tests.ItemSpawn
{
    [TestFixture]
    public class ItemSpawnSeedReproducibilityTests
    {
        private ItemSpawnPlanner _planner;
        private List<SpawnSurfaceData> _surfaces;
        private List<ItemSpawnTarget> _targets;

        [SetUp]
        public void SetUp()
        {
            _planner = new ItemSpawnPlanner();

            _targets = new List<ItemSpawnTarget>
            {
                new("Heal", ResourceCategory.Healing, baseWeight: 2.0f, spawnCost: 1),
                new("Ammo", ResourceCategory.Ammo, baseWeight: 3.0f, spawnCost: 2),
                new("Battery", ResourceCategory.LightResource, baseWeight: 1.5f, spawnCost: 1),
                new("KeyItem", ResourceCategory.Rare, baseWeight: 1.0f, spawnCost: 3, isNormalRandomCandidate: false)
            };

            var tri1 = new SpawnSurfaceTriangle(new SpawnVector3(0f, 0f, 0f), new SpawnVector3(20f, 0f, 0f), new SpawnVector3(0f, 20f, 0f), 1f, 1f, "SurfA", 0);
            var tri2 = new SpawnSurfaceTriangle(new SpawnVector3(20f, 20f, 0f), new SpawnVector3(0f, 20f, 0f), new SpawnVector3(20f, 0f, 0f), 1f, 1f, "SurfA", 1);
            var tri3 = new SpawnSurfaceTriangle(new SpawnVector3(50f, 0f, 0f), new SpawnVector3(80f, 0f, 0f), new SpawnVector3(50f, 30f, 0f), 1f, 1.5f, "SurfB", 0);

            _surfaces = new List<SpawnSurfaceData>
            {
                new("SurfA", 1.0f, new[] { tri1, tri2 }),
                new("SurfB", 1.5f, new[] { tri3 })
            };
        }

        [Test]
        public void IdenticalSeed_ProducesBitForBitIdenticalResults()
        {
            var guaranteed = new List<GuaranteedSpawnRule> { new("KeyItem", 1) };
            var config1 = new ItemSpawnRunConfig(
                seed: 424242,
                budget: 25,
                maxTotalSpawns: 15,
                guaranteedRules: guaranteed,
                playerStartMinDistance: 2.0f);

            var config2 = new ItemSpawnRunConfig(
                seed: 424242,
                budget: 25,
                maxTotalSpawns: 15,
                guaranteedRules: guaranteed,
                playerStartMinDistance: 2.0f);

            ItemSpawnRunResult result1 = _planner.PlanSpawns(config1, _targets, _surfaces);
            ItemSpawnRunResult result2 = _planner.PlanSpawns(config2, _targets, _surfaces);

            Assert.That(result1.TotalSpawnedCount, Is.EqualTo(result2.TotalSpawnedCount));
            Assert.That(result1.TotalCostSpent, Is.EqualTo(result2.TotalCostSpent));
            Assert.That(result1.RemainingBudget, Is.EqualTo(result2.RemainingBudget));

            for (int i = 0; i < result1.TotalSpawnedCount; i++)
            {
                SpawnedItemRecord item1 = result1.SpawnedItems[i];
                SpawnedItemRecord item2 = result2.SpawnedItems[i];

                Assert.That(item1.ItemId, Is.EqualTo(item2.ItemId), $"Index {i} ItemId mismatch");
                Assert.That(item1.Position.X, Is.EqualTo(item2.Position.X), $"Index {i} Pos.X mismatch");
                Assert.That(item1.Position.Y, Is.EqualTo(item2.Position.Y), $"Index {i} Pos.Y mismatch");
                Assert.That(item1.Position.Z, Is.EqualTo(item2.Position.Z), $"Index {i} Pos.Z mismatch");
                Assert.That(item1.YawDegrees, Is.EqualTo(item2.YawDegrees), $"Index {i} Yaw mismatch");
                Assert.That(item1.SourceSurfaceId, Is.EqualTo(item2.SourceSurfaceId), $"Index {i} SurfaceId mismatch");
                Assert.That(item1.SourceTriangleIndex, Is.EqualTo(item2.SourceTriangleIndex), $"Index {i} TriangleIndex mismatch");
                Assert.That(item1.SpawnCost, Is.EqualTo(item2.SpawnCost), $"Index {i} Cost mismatch");
            }
        }

        [Test]
        public void DifferentSeed_ProducesDifferentResults()
        {
            var config1 = new ItemSpawnRunConfig(seed: 1111, budget: 20, maxTotalSpawns: 10, playerStartMinDistance: 0f);
            var config2 = new ItemSpawnRunConfig(seed: 9999, budget: 20, maxTotalSpawns: 10, playerStartMinDistance: 0f);

            ItemSpawnRunResult result1 = _planner.PlanSpawns(config1, _targets, _surfaces);
            ItemSpawnRunResult result2 = _planner.PlanSpawns(config2, _targets, _surfaces);

            bool hasDifferentPosition = false;
            int minCount = Mathf.Min(result1.TotalSpawnedCount, result2.TotalSpawnedCount);

            for (int i = 0; i < minCount; i++)
            {
                if (SpawnVector3.Distance(result1.SpawnedItems[i].Position, result2.SpawnedItems[i].Position) > 0.01f)
                {
                    hasDifferentPosition = true;
                    break;
                }
            }

            Assert.IsTrue(hasDifferentPosition, "Different seeds should produce different positions.");
        }

        [Test]
        public void GlobalUnityEngineRandomState_IsNotMutatedByPlanner()
        {
            Random.InitState(55555);
            int rand1Before = Random.Range(0, 100000);
            Random.InitState(55555);

            // 実行
            var config = new ItemSpawnRunConfig(seed: 77777, budget: 30, maxTotalSpawns: 15);
            _planner.PlanSpawns(config, _targets, _surfaces);

            // UnityEngine.Randomの状態が影響を受けていないことの検証
            int rand1After = Random.Range(0, 100000);
            Assert.That(rand1After, Is.EqualTo(rand1Before));
        }
    }
}
