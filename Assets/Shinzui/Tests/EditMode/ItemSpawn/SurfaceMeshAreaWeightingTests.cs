using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;

namespace Shinzui.Tests.ItemSpawn
{
    [TestFixture]
    public class SurfaceMeshAreaWeightingTests
    {
        private SurfaceSamplerService _sampler;

        [SetUp]
        public void SetUp()
        {
            _sampler = new SurfaceSamplerService();
        }

        [Test]
        public void TriangleAreaWeighting_ProportionalToWorldTriangleArea()
        {
            // 三角形A: 面積 50 (底辺 10, 高さ 10)
            var triA = new SpawnSurfaceTriangle(
                new SpawnVector3(0f, 0f, 0f),
                new SpawnVector3(10f, 0f, 0f),
                new SpawnVector3(0f, 10f, 0f),
                paintWeight: 1.0f,
                regionWeight: 1.0f,
                "SurfaceA",
                0);

            // 三角形B: 面積 150 (底辺 30, 高さ 10)
            var triB = new SpawnSurfaceTriangle(
                new SpawnVector3(100f, 0f, 0f),
                new SpawnVector3(130f, 0f, 0f),
                new SpawnVector3(100f, 10f, 0f),
                paintWeight: 1.0f,
                regionWeight: 1.0f,
                "SurfaceB",
                1);

            var validTriangles = new List<SpawnSurfaceTriangle> { triA, triB };
            var prng = new DeterministicPrng(12345);

            int countA = 0;
            int countB = 0;
            const int trials = 10000;

            for (int i = 0; i < trials; i++)
            {
                _sampler.TrySelectTriangle(validTriangles, prng, out SpawnSurfaceTriangle chosen);
                if (chosen.SurfaceId == "SurfaceA") countA++;
                else if (chosen.SurfaceId == "SurfaceB") countB++;
            }

            // 期待値: triA = 50 / 200 = 25%, triB = 150 / 200 = 75%
            double ratioA = (double)countA / trials;
            double ratioB = (double)countB / trials;

            Assert.That(ratioA, Is.EqualTo(0.25).Within(0.02), $"Ratio A was {ratioA:P2}");
            Assert.That(ratioB, Is.EqualTo(0.75).Within(0.02), $"Ratio B was {ratioB:P2}");
        }

        [Test]
        public void MeshSubdivisionDensity_SubdividedSurfacesEqualInTotalAreaHaveEqualProbability()
        {
            // サーフェス1: 10x10の正方形を2つの大きな三角形に分割（各面積50、合計面積100）
            var surface1Tris = new List<SpawnSurfaceTriangle>
            {
                new(new SpawnVector3(0f, 0f, 0f), new SpawnVector3(10f, 0f, 0f), new SpawnVector3(0f, 10f, 0f), 1f, 1f, "Surface1", 0),
                new(new SpawnVector3(10f, 10f, 0f), new SpawnVector3(0f, 10f, 0f), new SpawnVector3(10f, 0f, 0f), 1f, 1f, "Surface1", 1)
            };
            var surface1 = new SpawnSurfaceData("Surface1", 1.0f, surface1Tris);

            // サーフェス2: 10x10の正方形を8つの小さな三角形に細分割（各面積12.5、合計面積100）
            var surface2Tris = new List<SpawnSurfaceTriangle>();
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    float x0 = 100f + x * 5f;
                    float x1 = x0 + 5f;
                    float y0 = y * 5f;
                    float y1 = y0 + 5f;
                    surface2Tris.Add(new SpawnSurfaceTriangle(new SpawnVector3(x0, y0, 0f), new SpawnVector3(x1, y0, 0f), new SpawnVector3(x0, y1, 0f), 1f, 1f, "Surface2", surface2Tris.Count));
                    surface2Tris.Add(new SpawnSurfaceTriangle(new SpawnVector3(x1, y1, 0f), new SpawnVector3(x0, y1, 0f), new SpawnVector3(x1, y0, 0f), 1f, 1f, "Surface2", surface2Tris.Count));
                }
            }
            var surface2 = new SpawnSurfaceData("Surface2", 1.0f, surface2Tris);

            var validTris = _sampler.CollectValidTriangles(new[] { surface1, surface2 }, out _, out _);
            var prng = new DeterministicPrng(8888);

            int countSurf1 = 0;
            int countSurf2 = 0;
            const int trials = 20000;

            for (int i = 0; i < trials; i++)
            {
                _sampler.TrySelectTriangle(validTris, prng, out SpawnSurfaceTriangle chosen);
                if (chosen.SurfaceId == "Surface1") countSurf1++;
                else if (chosen.SurfaceId == "Surface2") countSurf2++;
            }

            // メッシュの分割密度に関係なく、総面積が等しければ選択確率は50% : 50%
            double ratioSurf1 = (double)countSurf1 / trials;
            double ratioSurf2 = (double)countSurf2 / trials;

            Assert.That(ratioSurf1, Is.EqualTo(0.50).Within(0.015), $"Surface 1 ratio: {ratioSurf1:P2}");
            Assert.That(ratioSurf2, Is.EqualTo(0.50).Within(0.015), $"Surface 2 ratio: {ratioSurf2:P2}");
        }

        [Test]
        public void UniformBarycentricSampling_SamplePointsAverageToTriangleCentroid()
        {
            // 三角形 (0,0,0), (6,0,0), (0,6,0) の重心は (2, 2, 0)
            var tri = new SpawnSurfaceTriangle(
                new SpawnVector3(0f, 0f, 0f),
                new SpawnVector3(6f, 0f, 0f),
                new SpawnVector3(0f, 6f, 0f),
                1.0f, 1.0f, "Test", 0);

            var prng = new DeterministicPrng(31415);
            double sumX = 0.0;
            double sumY = 0.0;
            double sumZ = 0.0;
            const int samples = 20000;

            for (int i = 0; i < samples; i++)
            {
                SpawnVector3 pt = _sampler.SamplePointInTriangle(tri, prng, out _);
                sumX += pt.X;
                sumY += pt.Y;
                sumZ += pt.Z;
            }

            double meanX = sumX / samples;
            double meanY = sumY / samples;
            double meanZ = sumZ / samples;

            Assert.That(meanX, Is.EqualTo(2.0).Within(0.05));
            Assert.That(meanY, Is.EqualTo(2.0).Within(0.05));
            Assert.That(meanZ, Is.EqualTo(0.0).Within(0.01));
        }
    }
}
