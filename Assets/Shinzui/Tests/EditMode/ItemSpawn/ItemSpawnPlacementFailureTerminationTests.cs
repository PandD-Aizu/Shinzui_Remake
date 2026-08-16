using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Tests.ItemSpawn
{
    [TestFixture]
    public class ItemSpawnPlacementFailureTerminationTests
    {
        private ItemSpawnPlanner _planner;
        private SpawnSurfaceData _surface;

        [SetUp]
        public void SetUp()
        {
            _planner = new ItemSpawnPlanner();
            _surface = new SpawnSurfaceData(
                "TestSurf",
                1.0f,
                new[]
                {
                    new SpawnSurfaceTriangle(
                        new SpawnVector3(0f, 0f, 0f),
                        new SpawnVector3(10f, 0f, 0f),
                        new SpawnVector3(0f, 10f, 0f),
                        1.0f, 1.0f, "TestSurf", 0)
                });
        }

        private sealed class AlwaysRejectingValidator : ISpawnPlacementValidator
        {
            public bool Validate(
                SpawnVector3 position,
                SpawnVector3 normal,
                ItemSpawnTarget target,
                IReadOnlyList<SpawnedItemRecord> alreadySpawned,
                ItemSpawnRunConfig config,
                out string failureReason)
            {
                failureReason = "Always reject by test validator.";
                return false;
            }
        }

        [Test]
        public void AllPlacementsRejected_TerminatesCleanlyWithinMaxAttempts()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("ItemA", ResourceCategory.Utility, baseWeight: 1.0f, spawnCost: 1)
            };

            var config = new ItemSpawnRunConfig(
                seed: 12345,
                budget: 100,
                maxTotalSpawns: 50,
                maxPlacementAttemptsPerItem: 10,
                maxTotalPlacementAttempts: 50);

            var rejectValidator = new AlwaysRejectingValidator();

            ItemSpawnRunResult result = _planner.PlanSpawns(
                config,
                targets,
                new[] { _surface },
                customValidator: rejectValidator);

            Assert.That(result.TotalSpawnedCount, Is.EqualTo(0));
            Assert.That(result.TotalCostSpent, Is.EqualTo(0));
            Assert.That(result.RemainingBudget, Is.EqualTo(100));
            Assert.That(result.FailedPlacementAttempts, Is.GreaterThanOrEqualTo(50));
            Assert.That(result.SkipReasons.Count, Is.GreaterThan(0));
        }

        [Test]
        public void PlayerStartMinDistance_RejectsPositionsInsideSafeZone()
        {
            // サーフェスは (0, 0, 0) 付近（距離最大10）
            // プレイヤー位置を (0, 0, 0) に置き、安全距離を 20 に設定 -> 全て拒絶される
            var targets = new List<ItemSpawnTarget>
            {
                new("Item", ResourceCategory.Utility, baseWeight: 1.0f, spawnCost: 1)
            };

            var config = new ItemSpawnRunConfig(
                seed: 999,
                budget: 20,
                maxPlacementAttemptsPerItem: 5,
                maxTotalPlacementAttempts: 25,
                playerStartMinDistance: 20.0f,
                playerStartPosition: new SpawnVector3(0f, 0f, 0f));

            ItemSpawnRunResult result = _planner.PlanSpawns(config, targets, new[] { _surface });

            Assert.That(result.TotalSpawnedCount, Is.EqualTo(0));
            Assert.That(result.FailedPlacementAttempts, Is.GreaterThan(0));
        }
    }
}
