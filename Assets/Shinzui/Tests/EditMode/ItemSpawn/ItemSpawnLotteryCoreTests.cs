using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using UnityEngine;

namespace Shinzui.Tests.ItemSpawn
{
    [TestFixture]
    public class ItemSpawnLotteryCoreTests
    {
        private ItemSpawnLotteryCore _lotteryCore;

        [SetUp]
        public void SetUp()
        {
            _lotteryCore = new ItemSpawnLotteryCore();
        }

        private static List<SpawnSurfaceTriangle> CreateStandardSurfaceTriangles()
        {
            // 2つの三角形からなる平面 (0,0,0)-(10,0,0)-(0,0,10) と (10,0,0)-(10,0,10)-(0,0,10)
            // 面積はそれぞれ 50
            return new List<SpawnSurfaceTriangle>
            {
                new SpawnSurfaceTriangle(
                    new Vector3(0, 0, 0),
                    new Vector3(10, 0, 0),
                    new Vector3(0, 0, 10),
                    paintWeight: 1.0f,
                    regionWeight: 1.0f,
                    surfaceId: 1,
                    surfaceName: "Floor_A",
                    triangleIndex: 0),
                new SpawnSurfaceTriangle(
                    new Vector3(10, 0, 0),
                    new Vector3(10, 0, 10),
                    new Vector3(0, 0, 10),
                    paintWeight: 1.0f,
                    regionWeight: 1.0f,
                    surfaceId: 1,
                    surfaceName: "Floor_A",
                    triangleIndex: 1)
            };
        }

        [Test]
        public void Weight0_ZeroWeightItemsAndTriangles_NeverSelected()
        {
            var triangles = new List<SpawnSurfaceTriangle>
            {
                // 有効三角形
                new SpawnSurfaceTriangle(new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(0, 0, 5), 1.0f, 1.0f, 1, "Valid", 0),
                // PaintWeight = 0 の無効三角形
                new SpawnSurfaceTriangle(new Vector3(10, 0, 0), new Vector3(15, 0, 0), new Vector3(10, 0, 5), 0.0f, 1.0f, 2, "ZeroPaint", 1),
                // RegionWeight = 0 の無効三角形
                new SpawnSurfaceTriangle(new Vector3(20, 0, 0), new Vector3(25, 0, 0), new Vector3(20, 0, 5), 1.0f, 0.0f, 3, "ZeroRegion", 2),
                // 面積0（縮退）三角形
                new SpawnSurfaceTriangle(new Vector3(30, 0, 0), new Vector3(30, 0, 0), new Vector3(30, 0, 0), 1.0f, 1.0f, 4, "Degenerate", 3)
            };

            var definitions = new List<ItemSpawnDefinition>
            {
                new ItemSpawnDefinition("valid_item", 10.0f, ResourceCategory.Healing, spawnCost: 1, maxCount: 100),
                new ItemSpawnDefinition("zero_weight_item", 0.0f, ResourceCategory.Ammo, spawnCost: 1, maxCount: 100)
            };

            var config = new ItemSpawnConfig(budget: 50, maxTotalCount: 50, maxPlacementAttempts: 10);
            var rng = new ItemSpawnPRNG(12345);
            var validator = new DistancePositionValidator();

            var report = _lotteryCore.Execute(config, definitions, triangles, null, rng, validator);

            Assert.That(report.SpawnedItems.Count, Is.GreaterThan(0));
            foreach (var spawned in report.SpawnedItems)
            {
                // 重み0のアイテムは一切生成されないこと
                Assert.That(spawned.ItemId, Is.Not.EqualTo("zero_weight_item"));
                Assert.That(spawned.ItemId, Is.EqualTo("valid_item"));

                // 重み0や縮退のサーフェスからは一切生成されないこと
                Assert.That(spawned.SurfaceName, Is.EqualTo("Valid"));
                Assert.That(spawned.SurfaceId, Is.EqualTo(1));
            }
        }

        [Test]
        public void WeightDifferenceStatistics_HigherWeightChosenProportionally()
        {
            var triangles = CreateStandardSurfaceTriangles();
            var definitions = new List<ItemSpawnDefinition>
            {
                new ItemSpawnDefinition("item_high", 3.0f, ResourceCategory.Utility, spawnCost: 1, maxCount: 10000),
                new ItemSpawnDefinition("item_low", 1.0f, ResourceCategory.Utility, spawnCost: 1, maxCount: 10000)
            };

            var config = new ItemSpawnConfig(budget: 2000, maxTotalCount: 2000, maxPlacementAttempts: 5, minPlayerStartDistance: 0f);
            var rng = new ItemSpawnPRNG(777);
            var validator = new CompositePositionValidator(); // 距離制限なしで純粋な抽選比率を測定

            var report = _lotteryCore.Execute(config, definitions, triangles, null, rng, validator);

            int highCount = report.SpawnedItems.Count(i => i.ItemId == "item_high");
            int lowCount = report.SpawnedItems.Count(i => i.ItemId == "item_low");
            int total = highCount + lowCount;

            Assert.That(total, Is.EqualTo(2000));
            double highRatio = (double)highCount / total;

            // 期待値 3/(3+1) = 0.75。99.9%信頼区間内（0.70〜0.80）に収まることを検証
            Assert.That(highRatio, Is.InRange(0.70, 0.80), $"High weight ratio {highRatio} should be close to 0.75");
        }

        [Test]
        public void MeshSubdivisionDensity_AreaWeightingInvariance()
        {
            // サーフェス1: 単一の大きな三角形（面積 20）
            var triSingle = new SpawnSurfaceTriangle(
                new Vector3(0, 0, 0),
                new Vector3(20, 0, 0),
                new Vector3(0, 0, 2),
                paintWeight: 1.0f,
                regionWeight: 1.0f,
                surfaceId: 1,
                surfaceName: "SingleLarge",
                triangleIndex: 0);

            // サーフェス2: 同一の総面積（20）を2つに分割した三角形（面積 12 と 面積 8）
            var triSub1 = new SpawnSurfaceTriangle(
                new Vector3(100, 0, 0),
                new Vector3(112, 0, 0),
                new Vector3(100, 0, 2),
                paintWeight: 1.0f,
                regionWeight: 1.0f,
                surfaceId: 2,
                surfaceName: "Subdivided",
                triangleIndex: 0);

            var triSub2 = new SpawnSurfaceTriangle(
                new Vector3(112, 0, 0),
                new Vector3(120, 0, 0),
                new Vector3(100, 0, 2),
                paintWeight: 1.0f,
                regionWeight: 1.0f,
                surfaceId: 2,
                surfaceName: "Subdivided",
                triangleIndex: 1);

            Assert.That(triSingle.Area, Is.EqualTo(20f).Within(0.01f));
            Assert.That(triSub1.Area + triSub2.Area, Is.EqualTo(20f).Within(0.01f));

            var allTriangles = new List<SpawnSurfaceTriangle> { triSingle, triSub1, triSub2 };
            var definitions = new List<ItemSpawnDefinition>
            {
                new ItemSpawnDefinition("sample_item", 1.0f, ResourceCategory.Healing, spawnCost: 1, maxCount: 10000)
            };

            var config = new ItemSpawnConfig(budget: 3000, maxTotalCount: 3000, maxPlacementAttempts: 5, minPlayerStartDistance: 0f);
            var rng = new ItemSpawnPRNG(999);
            var validator = new CompositePositionValidator();

            var report = _lotteryCore.Execute(config, definitions, allTriangles, null, rng, validator);

            int singleCount = report.SpawnedItems.Count(i => i.SurfaceName == "SingleLarge");
            int subCount = report.SpawnedItems.Count(i => i.SurfaceName == "Subdivided");
            int total = singleCount + subCount;

            Assert.That(total, Is.EqualTo(3000));
            double singleRatio = (double)singleCount / total;

            // 総面積が等しいため、分割数によらず確率は 50% 付近（0.46〜0.54）となる
            Assert.That(singleRatio, Is.InRange(0.46, 0.54), $"Single surface ratio {singleRatio} should be close to 0.50");
        }

        [Test]
        public void NeedWeightStatistics_ScalesItemSelectionDistribution()
        {
            var triangles = CreateStandardSurfaceTriangles();
            var definitions = new List<ItemSpawnDefinition>
            {
                new ItemSpawnDefinition("item_healing", 1.0f, ResourceCategory.Healing, spawnCost: 1, maxCount: 5000),
                new ItemSpawnDefinition("item_ammo", 1.0f, ResourceCategory.Ammo, spawnCost: 1, maxCount: 5000)
            };

            // HealingのNeedWeightを3.0f, AmmoのNeedWeightを1.0fに設定
            var needWeights = new Dictionary<ResourceCategory, float>
            {
                { ResourceCategory.Healing, 3.0f },
                { ResourceCategory.Ammo, 1.0f }
            };

            var config = new ItemSpawnConfig(budget: 2000, maxTotalCount: 2000, maxPlacementAttempts: 5, minPlayerStartDistance: 0f);
            var rng = new ItemSpawnPRNG(54321);
            var validator = new CompositePositionValidator();

            var report = _lotteryCore.Execute(config, definitions, triangles, needWeights, rng, validator);

            int healingCount = report.SpawnedItems.Count(i => i.ItemId == "item_healing");
            int ammoCount = report.SpawnedItems.Count(i => i.ItemId == "item_ammo");
            int total = healingCount + ammoCount;

            Assert.That(total, Is.EqualTo(2000));
            double healingRatio = (double)healingCount / total;

            // 期待値 3/(3+1) = 0.75。0.70〜0.80の範囲であることを検証
            Assert.That(healingRatio, Is.InRange(0.70, 0.80), $"Healing need weight ratio {healingRatio} should be close to 0.75");
            Assert.That(report.CategoryNeedWeights[ResourceCategory.Healing], Is.EqualTo(3.0f));
            Assert.That(report.CategoryNeedWeights[ResourceCategory.Ammo], Is.EqualTo(1.0f));
        }

        [Test]
        public void Budget_NeverExceeded_UnderAnyCircumstance()
        {
            var triangles = CreateStandardSurfaceTriangles();
            var definitions = new List<ItemSpawnDefinition>
            {
                new ItemSpawnDefinition("costly_item", 1.0f, ResourceCategory.Weapon, spawnCost: 7, maxCount: 100)
            };

            // 予算20の場合、コスト7のアイテムは最大2個（コスト14）しか生成できず、3個目（21）は予算超過で不可
            var config = new ItemSpawnConfig(budget: 20, maxTotalCount: 100, maxPlacementAttempts: 5);
            var rng = new ItemSpawnPRNG(111);
            var validator = new DistancePositionValidator();

            var report = _lotteryCore.Execute(config, definitions, triangles, null, rng, validator);

            Assert.That(report.TotalSpawnedCount, Is.EqualTo(2));
            Assert.That(report.TotalCostUsed, Is.EqualTo(14));
            Assert.That(report.RemainingBudget, Is.EqualTo(6));
            Assert.That(report.TotalCostUsed, Is.LessThanOrEqualTo(config.Budget));

            // 最低保証で予算超過するケースの安全スキップ検証
            var overBudgetDefinitions = new List<ItemSpawnDefinition>
            {
                new ItemSpawnDefinition("huge_guarantee", 1.0f, ResourceCategory.Rare, spawnCost: 50, minCount: 1)
            };
            var smallBudgetConfig = new ItemSpawnConfig(budget: 30, maxTotalCount: 10);
            var overReport = _lotteryCore.Execute(smallBudgetConfig, overBudgetDefinitions, triangles, null, rng, validator);

            Assert.That(overReport.TotalSpawnedCount, Is.EqualTo(0));
            Assert.That(overReport.TotalCostUsed, Is.EqualTo(0));
            Assert.That(overReport.RemainingBudget, Is.EqualTo(30));
            Assert.That(overReport.SkipReasons.Any(r => r.Contains("budget")), Is.True);
        }

        [Test]
        public void SeedReproducibility_SameSeedProducesIdenticalOutput()
        {
            var triangles = CreateStandardSurfaceTriangles();
            var definitions = new List<ItemSpawnDefinition>
            {
                new ItemSpawnDefinition("item_a", 2.0f, ResourceCategory.Healing, spawnCost: 2, maxCount: 20),
                new ItemSpawnDefinition("item_b", 1.0f, ResourceCategory.Utility, spawnCost: 1, maxCount: 20),
                new ItemSpawnDefinition("item_c", 3.0f, ResourceCategory.Ammo, spawnCost: 3, maxCount: 20)
            };

            var config = new ItemSpawnConfig(budget: 40, maxTotalCount: 20, maxPlacementAttempts: 20, minPlayerStartDistance: 1.0f);
            var validator = new DistancePositionValidator();

            int seed = 42891;
            var report1 = _lotteryCore.Execute(config, definitions, triangles, null, new ItemSpawnPRNG(seed), validator);
            var report2 = _lotteryCore.Execute(config, definitions, triangles, null, new ItemSpawnPRNG(seed), validator);

            // 同一シードなら全項目が完全に一致すること
            Assert.That(report1.TotalSpawnedCount, Is.EqualTo(report2.TotalSpawnedCount));
            Assert.That(report1.TotalCostUsed, Is.EqualTo(report2.TotalCostUsed));
            Assert.That(report1.RemainingBudget, Is.EqualTo(report2.RemainingBudget));

            for (int i = 0; i < report1.SpawnedItems.Count; i++)
            {
                var item1 = report1.SpawnedItems[i];
                var item2 = report2.SpawnedItems[i];

                Assert.That(item1.ItemId, Is.EqualTo(item2.ItemId));
                Assert.That(item1.WorldPosition.x, Is.EqualTo(item2.WorldPosition.x).Within(1e-5f));
                Assert.That(item1.WorldPosition.y, Is.EqualTo(item2.WorldPosition.y).Within(1e-5f));
                Assert.That(item1.WorldPosition.z, Is.EqualTo(item2.WorldPosition.z).Within(1e-5f));
                Assert.That(item1.WorldRotation.x, Is.EqualTo(item2.WorldRotation.x).Within(1e-5f));
                Assert.That(item1.WorldRotation.y, Is.EqualTo(item2.WorldRotation.y).Within(1e-5f));
                Assert.That(item1.WorldRotation.z, Is.EqualTo(item2.WorldRotation.z).Within(1e-5f));
                Assert.That(item1.WorldRotation.w, Is.EqualTo(item2.WorldRotation.w).Within(1e-5f));
                Assert.That(item1.TriangleIndex, Is.EqualTo(item2.TriangleIndex));
            }

            // 異なるシードなら異なる結果になること
            var reportDifferent = _lotteryCore.Execute(config, definitions, triangles, null, new ItemSpawnPRNG(99999), validator);
            bool isDifferent = report1.SpawnedItems.Count != reportDifferent.SpawnedItems.Count ||
                               report1.SpawnedItems[0].WorldPosition != reportDifferent.SpawnedItems[0].WorldPosition;
            Assert.That(isDifferent, Is.True);
        }

        [Test]
        public void PlacementTotalFailureTermination_TerminatesWithoutHanging()
        {
            var triangles = CreateStandardSurfaceTriangles();
            var definitions = new List<ItemSpawnDefinition>
            {
                new ItemSpawnDefinition("impossible_item", 1.0f, ResourceCategory.Utility, spawnCost: 1, maxCount: 10)
            };

            var config = new ItemSpawnConfig(budget: 50, maxTotalCount: 10, maxPlacementAttempts: 15);
            var rng = new ItemSpawnPRNG(123);

            // 常に配置失敗（障害物等）を返すモックバリデータ
            var alwaysFailValidator = new AlwaysFailValidator();

            var report = _lotteryCore.Execute(config, definitions, triangles, null, rng, alwaysFailValidator);

            // 無限ループせず正常終了し、生成数0、失敗試行回数が記録されていること
            Assert.That(report.TotalSpawnedCount, Is.EqualTo(0));
            Assert.That(report.FailedPlacementAttempts, Is.GreaterThan(0));
            Assert.That(report.RemainingBudget, Is.EqualTo(50));
            Assert.That(report.SkipReasons.Count, Is.GreaterThan(0));
        }

        [Test]
        public void MinimumGuarantees_ExecutedBeforeNormalLottery()
        {
            var triangles = CreateStandardSurfaceTriangles();
            var definitions = new List<ItemSpawnDefinition>
            {
                // 通常抽選対象外のキーアイテム（最低保証1個）
                new ItemSpawnDefinition("key_silver", 0f, ResourceCategory.Rare, spawnCost: 5, minCount: 1, isNormalRandomCandidate: false),
                // 通常アイテム
                new ItemSpawnDefinition("common_herb", 1f, ResourceCategory.Healing, spawnCost: 1, maxCount: 50)
            };

            var config = new ItemSpawnConfig(budget: 10, maxTotalCount: 10, maxPlacementAttempts: 10);
            var rng = new ItemSpawnPRNG(456);
            var validator = new DistancePositionValidator();

            var report = _lotteryCore.Execute(config, definitions, triangles, null, rng, validator);

            // キーアイテムが最初に最低保証として生成されていること
            Assert.That(report.SpawnedItems.Count, Is.GreaterThan(0));
            var firstItem = report.SpawnedItems[0];
            Assert.That(firstItem.ItemId, Is.EqualTo("key_silver"));
            Assert.That(firstItem.IsGuaranteed, Is.True);

            // 残りの予算（10 - 5 = 5）で通常アイテムが生成されていること
            Assert.That(report.TotalCostUsed, Is.EqualTo(10));
            Assert.That(report.SpawnedItems.Count(i => i.ItemId == "common_herb"), Is.EqualTo(5));
        }

        private sealed class AlwaysFailValidator : ISpawnPositionValidator
        {
            public bool ValidatePosition(in SpawnValidationContext context, out string failReason)
            {
                failReason = "Mock failure: Position occupied by obstacle.";
                return false;
            }
        }
    }
}
