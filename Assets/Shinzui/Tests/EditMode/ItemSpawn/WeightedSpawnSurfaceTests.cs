using NUnit.Framework;
using Shinzui.View.ItemSpawn;
using UnityEngine;

namespace Shinzui.Tests.ItemSpawn
{
    [TestFixture]
    public class WeightedSpawnSurfaceTests
    {
        private GameObject _testGameObject;
        private Mesh _quadMesh;
        private Mesh _triMesh;

        [SetUp]
        public void SetUp()
        {
            _testGameObject = new GameObject("TestWeightedSpawnSurface");

            // 2つの三角形からなるクアッドメッシュ (4頂点, 6インデックス)
            _quadMesh = new Mesh
            {
                name = "TestQuadMesh",
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, 0f, 0f),
                    new Vector3(1f, 1f, 0f),
                    new Vector3(0f, 1f, 0f)
                },
                triangles = new[]
                {
                    0, 1, 2,
                    0, 2, 3
                }
            };

            // 1つの三角形からなるメッシュ (3頂点, 3インデックス)
            _triMesh = new Mesh
            {
                name = "TestTriMesh",
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(2f, 0f, 0f),
                    new Vector3(0f, 2f, 0f)
                },
                triangles = new[]
                {
                    0, 1, 2
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGameObject != null)
            {
                Object.DestroyImmediate(_testGameObject);
            }
            if (_quadMesh != null)
            {
                Object.DestroyImmediate(_quadMesh);
            }
            if (_triMesh != null)
            {
                Object.DestroyImmediate(_triMesh);
            }
        }

        [Test]
        public void WeightedSpawnSurface_CanBeInitializedWithoutMeshFilter()
        {
            // MeshFilterを持たないGameObjectに追加
            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            Assert.That(_testGameObject.GetComponent<MeshFilter>(), Is.Null, "MeshFilter must not be required.");

            // メッシュで初期化
            surface.InitializeFromMesh(_quadMesh);

            Assert.That(surface.IsDataValid, Is.True);
            Assert.That(surface.SurfaceMesh, Is.EqualTo(_quadMesh));
            Assert.That(surface.SourceMesh, Is.EqualTo(_quadMesh)); // 後方互換性プロパティ
            Assert.That(surface.TriangleCount, Is.EqualTo(2));
        }

        [Test]
        public void WeightedSpawnSurface_WeightManipulationAndCopying()
        {
            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_quadMesh);

            surface.SetWeight(0, 0.8f);
            surface.SetWeight(1, 0.4f);

            Assert.That(surface.GetWeight(0), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(surface.GetWeight(1), Is.EqualTo(0.4f).Within(0.0001f));

            // CopyWeights は独立したクローンを返す
            float[] copied = surface.CopyWeights();
            Assert.That(copied.Length, Is.EqualTo(2));
            Assert.That(copied[0], Is.EqualTo(0.8f).Within(0.0001f));

            copied[0] = 0.1f;
            Assert.That(surface.GetWeight(0), Is.EqualTo(0.8f), "Mutating copied weights must not affect surface data.");

            // SetAllWeights の検証
            surface.SetAllWeights(0.6f);
            Assert.That(surface.GetWeight(0), Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(surface.GetWeight(1), Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void WeightedSpawnSurface_TryGetTriangleData_CalculatesWorldSpaceCoordinates()
        {
            _testGameObject.transform.position = new Vector3(10f, 20f, 30f);
            _testGameObject.transform.localScale = new Vector3(2f, 2f, 2f);

            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_quadMesh);
            surface.SetWeight(0, 0.75f);

            bool success = surface.TryGetTriangleData(0, out Vector3 a, out Vector3 b, out Vector3 c, out float weight);

            Assert.That(success, Is.True);
            Assert.That(weight, Is.EqualTo(0.75f));

            // 元の頂点: (0,0,0), (1,0,0), (1,1,0)
            // ワールド変換後 (x2 + (10,20,30)): (10,20,30), (12,20,30), (12,22,30)
            Assert.That(a, Is.EqualTo(new Vector3(10f, 20f, 30f)));
            Assert.That(b, Is.EqualTo(new Vector3(12f, 20f, 30f)));
            Assert.That(c, Is.EqualTo(new Vector3(12f, 22f, 30f)));
        }

        [Test]
        public void WeightedSpawnSurface_InvalidatesData_OnTopologyMismatch()
        {
            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_quadMesh);
            Assert.That(surface.IsDataValid, Is.True);

            // 異なるトポロジーのメッシュが設定された場合
            surface.SetSurfaceMeshDirectly(_triMesh);
            Assert.That(surface.IsDataValid, Is.False, "Topology mismatch must make IsDataValid false.");

            // 再初期化により正常に戻る
            surface.InitializeFromMesh(_triMesh);
            Assert.That(surface.IsDataValid, Is.True);
            Assert.That(surface.TriangleCount, Is.EqualTo(1));
        }

        [Test]
        public void WeightedSpawnSurface_OutOfBoundsAndNegativeWeights_SafeHandling()
        {
            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_quadMesh);

            // 境界外インデックスの安全性
            Assert.That(surface.GetWeight(-1), Is.EqualTo(0f));
            Assert.That(surface.GetWeight(100), Is.EqualTo(0f));

            Assert.That(surface.TryGetTriangleData(-1, out _, out _, out _, out _), Is.False);
            Assert.That(surface.TryGetTriangleData(100, out _, out _, out _, out _), Is.False);

            // 負の重みは 0 にクランプ
            surface.SetWeight(0, -10f);
            Assert.That(surface.GetWeight(0), Is.EqualTo(0f));

            // 負の RegionWeight は 0 にクランプ
            surface.SetRegionWeight(-2.5f);
            Assert.That(surface.RegionWeight, Is.EqualTo(0f));
        }
    }
}
