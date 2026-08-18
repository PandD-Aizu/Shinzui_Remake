using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Tests.ItemSpawn
{
    [TestFixture]
    public class ItemSpawnWeightZeroExclusionTests
    {
        private ItemSpawnLotteryService _lotteryService;
        private SurfaceSamplerService _surfaceSampler;
        private ItemSpawnPlanner _planner;

        [SetUp]
        public void SetUp()
        {
            _lotteryService = new ItemSpawnLotteryService();
            _surfaceSampler = new SurfaceSamplerService();
            _planner = new ItemSpawnPlanner(_lotteryService, _surfaceSampler);
        }

        [Test]
        public void BaseWeightZero_ItemIsNeverSelectedInLottery()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("Item_ZeroWeight", ResourceCategory.Healing, baseWeight: 0.0f),
                new("Item_PositiveWeight", ResourceCategory.Healing, baseWeight: 1.0f)
            };

            var prng = new DeterministicPrng(12345);
            var itemCounts = new Dictionary<string, int>();
            var catCounts = new Dictionary<ResourceCategory, int>();

            for (int i = 0; i < 1000; i++)
            {
                bool success = _lotteryService.TrySelectRandomCandidate(
                    targets,
                    itemCounts,
                    catCounts,
                    100,
                    _ => 1.0f,
                    prng,
                    out ItemSpawnTarget selected,
                    out _, out _, out _, out _);

                Assert.IsTrue(success);
                Assert.That(selected.Id, Is.EqualTo("Item_PositiveWeight"));
                Assert.That(selected.Id, Is.Not.EqualTo("Item_ZeroWeight"));
            }
        }

        [Test]
        public void NeedWeightZero_ItemIsNeverSelectedInLottery()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("Healing_Item", ResourceCategory.Healing, baseWeight: 2.0f),
                new("Ammo_Item", ResourceCategory.Ammo, baseWeight: 2.0f)
            };

            var prng = new DeterministicPrng(9999);
            var itemCounts = new Dictionary<string, int>();
            var catCounts = new Dictionary<ResourceCategory, int>();

            // NeedWeight: Healing = 0.0, Ammo = 1.0
            float NeedProvider(ResourceCategory category) => category == ResourceCategory.Healing ? 0.0f : 1.0f;

            for (int i = 0; i < 1000; i++)
            {
                bool success = _lotteryService.TrySelectRandomCandidate(
                    targets,
                    itemCounts,
                    catCounts,
                    100,
                    NeedProvider,
                    prng,
                    out ItemSpawnTarget selected,
                    out _, out _, out _, out _);

                Assert.IsTrue(success);
                Assert.That(selected.Id, Is.EqualTo("Ammo_Item"));
                Assert.That(selected.Id, Is.Not.EqualTo("Healing_Item"));
            }
        }

        [Test]
        public void PaintWeightZero_TriangleIsNeverSelectedForPlacement()
        {
            var triangleZero = new SpawnSurfaceTriangle(
                new SpawnVector3(0f, 0f, 0f),
                new SpawnVector3(1f, 0f, 0f),
                new SpawnVector3(0f, 1f, 0f),
                paintWeight: 0.0f,
                regionWeight: 1.0f,
                "Surface1",
                0);

            var triangleValid = new SpawnSurfaceTriangle(
                new SpawnVector3(10f, 0f, 0f),
                new SpawnVector3(11f, 0f, 0f),
                new SpawnVector3(10f, 1f, 0f),
                paintWeight: 1.0f,
                regionWeight: 1.0f,
                "Surface1",
                1);

            var surface = new SpawnSurfaceData("Surface1", 1.0f, new[] { triangleZero, triangleValid });
            var validTriangles = _surfaceSampler.CollectValidTriangles(new[] { surface }, out int totalCount, out float totalArea);

            Assert.That(totalCount, Is.EqualTo(2));
            Assert.That(validTriangles.Count, Is.EqualTo(1));
            Assert.That(validTriangles[0].TriangleIndex, Is.EqualTo(1));

            var prng = new DeterministicPrng(42);
            for (int i = 0; i < 500; i++)
            {
                bool success = _surfaceSampler.TrySelectTriangle(validTriangles, prng, out SpawnSurfaceTriangle chosen);
                Assert.IsTrue(success);
                Assert.That(chosen.TriangleIndex, Is.EqualTo(1));
            }
        }

        [Test]
        public void DegenerateTriangle_ColinearVerticesHaveZeroAreaAndAreExcluded()
        {
            // 同一直線上の縮退三角形（面積0）
            var degenerate = new SpawnSurfaceTriangle(
                new SpawnVector3(0f, 0f, 0f),
                new SpawnVector3(1f, 0f, 0f),
                new SpawnVector3(2f, 0f, 0f),
                paintWeight: 1.0f,
                regionWeight: 1.0f,
                "Surface1",
                0);

            Assert.That(degenerate.Area, Is.LessThanOrEqualTo(1e-6f));
            Assert.That(degenerate.TotalWeight, Is.EqualTo(0f));
            Assert.IsFalse(degenerate.IsValid);
        }

        [Test]
        public void NonNormalCandidate_KeyItemIsExcludedFromNormalRandomLottery()
        {
            var targets = new List<ItemSpawnTarget>
            {
                new("Key_Item", ResourceCategory.Rare, baseWeight: 10.0f, isNormalRandomCandidate: false),
                new("Normal_Item", ResourceCategory.Utility, baseWeight: 1.0f, isNormalRandomCandidate: true)
            };

            var prng = new DeterministicPrng(777);
            var itemCounts = new Dictionary<string, int>();
            var catCounts = new Dictionary<ResourceCategory, int>();

            for (int i = 0; i < 500; i++)
            {
                bool success = _lotteryService.TrySelectRandomCandidate(
                    targets,
                    itemCounts,
                    catCounts,
                    100,
                    _ => 1.0f,
                    prng,
                    out ItemSpawnTarget selected,
                    out _, out _, out _, out _);

                Assert.IsTrue(success);
                Assert.That(selected.Id, Is.EqualTo("Normal_Item"));
            }
        }
    }
}
