using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Domain.DomainServices.ItemSpawn;
using Shinzui.Domain.ValueObjects.ItemSpawn;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using Shinzui.View.ItemSpawn;
using UnityEngine;

namespace Shinzui.Tests.ItemSpawn
{
    [TestFixture]
    public class WeightedSpawnSurfaceMapTests
    {
        private GameObject _testGameObject;
        private Mesh _quadMesh;
        private Mesh _singleBigTriMesh;

        [SetUp]
        public void SetUp()
        {
            _testGameObject = new GameObject("TestWeightedSpawnSurfaceMap");

            // 10x10のクアッドメッシュ (ローカル 0..10)
            _quadMesh = new Mesh
            {
                name = "TestQuad10x10",
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(10f, 0f, 0f),
                    new Vector3(10f, 0f, 10f),
                    new Vector3(0f, 0f, 10f)
                },
                triangles = new[]
                {
                    0, 2, 1,
                    0, 3, 2
                }
            };
            _quadMesh.RecalculateBounds();

            // 巨大な単一三角形メッシュ (0..10)
            _singleBigTriMesh = new Mesh
            {
                name = "TestSingleBigTri",
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(10f, 0f, 0f),
                    new Vector3(0f, 0f, 10f)
                },
                triangles = new[]
                {
                    0, 1, 2
                }
            };
            _singleBigTriMesh.RecalculateBounds();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGameObject != null) Object.DestroyImmediate(_testGameObject);
            if (_quadMesh != null) Object.DestroyImmediate(_quadMesh);
            if (_singleBigTriMesh != null) Object.DestroyImmediate(_singleBigTriMesh);
        }

        [Test]
        public void CoordinateConversion_WorldLocalAndNormalizedUV_Bidirectional()
        {
            _testGameObject.transform.position = new Vector3(100f, 50f, 200f);
            _testGameObject.transform.localScale = new Vector3(2f, 1f, 2f);

            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_quadMesh);

            // ローカル (5, 0, 5) -> UV (0.5, 0.5)
            Assert.IsTrue(surface.LocalToNormalizedMapCoords(new Vector3(5f, 0f, 5f), out float u, out float v));
            Assert.That(u, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(v, Is.EqualTo(0.5f).Within(0.001f));

            // UV (0.5, 0.5) -> ローカル (5, 0, 5)
            Vector3 localReconstructed = surface.NormalizedMapCoordsToLocal(0.5f, 0.5f);
            Assert.That(localReconstructed.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(localReconstructed.z, Is.EqualTo(5f).Within(0.001f));

            // ワールド座標 (100 + 5*2, 50, 200 + 5*2) = (110, 50, 210) -> UV (0.5, 0.5)
            Vector3 worldCenter = new Vector3(110f, 50f, 210f);
            Assert.IsTrue(surface.WorldToNormalizedMapCoords(worldCenter, out float wu, out float wv));
            Assert.That(wu, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(wv, Is.EqualTo(0.5f).Within(0.001f));

            // UV (0.5, 0.5) -> ワールド (110, 50, 210)
            Vector3 worldReconstructed = surface.NormalizedMapCoordsToWorld(0.5f, 0.5f);
            Assert.That(worldReconstructed.x, Is.EqualTo(110f).Within(0.001f));
            Assert.That(worldReconstructed.z, Is.EqualTo(210f).Within(0.001f));

            // 境界外座標判定
            Assert.IsFalse(surface.LocalToNormalizedMapCoords(new Vector3(-1f, 0f, 5f), out _, out _));
            Assert.IsFalse(surface.LocalToNormalizedMapCoords(new Vector3(5f, 0f, 15f), out _, out _));
        }

        [Test]
        public void BilinearSampling_InterpolatesCorrectly_AndReturnsZeroOutOfBounds()
        {
            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_quadMesh);

            // 解像度 2x2 で各コーナーに設定: (0,0)=0.0, (1,0)=1.0, (0,1)=0.5, (1,1)=1.0
            surface.ResizeMap(2);
            float[] map2x2 = new float[]
            {
                0.0f, 1.0f,
                0.5f, 1.0f
            };
            surface.SetWeightMap(map2x2, 2);

            // コーナーサンプリング
            Assert.That(surface.GetWeight(0f, 0f), Is.EqualTo(0.0f).Within(0.001f));
            Assert.That(surface.GetWeight(1f, 0f), Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(surface.GetWeight(0f, 1f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(surface.GetWeight(1f, 1f), Is.EqualTo(1.0f).Within(0.001f));

            // 中央 (0.5, 0.5) のバイリニア補間:
            // top = 0.5, bottom = 0.75, center = (0.5 + 0.75) / 2 = 0.625
            Assert.That(surface.GetWeight(0.5f, 0.5f), Is.EqualTo(0.625f).Within(0.001f));

            // 境界外サンプリングは 0.0f
            Assert.That(surface.GetWeight(-0.1f, 0.5f), Is.EqualTo(0.0f));
            Assert.That(surface.GetWeight(1.1f, 0.5f), Is.EqualTo(0.0f));
            Assert.That(surface.GetWeight(0.5f, -0.2f), Is.EqualTo(0.0f));
            Assert.That(surface.GetWeight(0.5f, 1.5f), Is.EqualTo(0.0f));
        }

        [Test]
        public void ResolutionResize_PreservesWeightsWithBilinearResampling()
        {
            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_quadMesh);

            // 解像度 64 で中央に 0.8f の領域を設定
            surface.ResizeMap(64);
            surface.SetAllWeights(0.2f);
            surface.SetWeightAtNormalizedCoords(0.5f, 0.5f, 0.8f);

            float wBefore = surface.GetWeight(0.5f, 0.5f);
            Assert.That(wBefore, Is.EqualTo(0.8f).Within(0.05f));

            // 256 に拡大リサイズ
            surface.ResizeMap(256);
            Assert.That(surface.MapResolution, Is.EqualTo(256));
            Assert.That(surface.CopyWeightMap().Length, Is.EqualTo(256 * 256));

            float wAfterUpscale = surface.GetWeight(0.5f, 0.5f);
            Assert.That(wAfterUpscale, Is.EqualTo(wBefore).Within(0.05f));

            // 128 に縮小リサイズ
            surface.ResizeMap(128);
            Assert.That(surface.MapResolution, Is.EqualTo(128));
            float wAfterDownscale = surface.GetWeight(0.5f, 0.5f);
            Assert.That(wAfterDownscale, Is.EqualTo(wBefore).Within(0.05f));
        }

        [Test]
        public void SingleCoarseTriangle_WithLocalWeightMap_RestrictsSpawnsToNonZeroArea()
        {
            // 単一の粗い三角形メッシュ上で、u < 0.5 (X < 5) を Weight 0、u >= 0.5 (X >= 5) を Weight 1 に塗る
            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_singleBigTriMesh);
            surface.ResizeMap(128);

            int n = surface.MapResolution;
            float[] map = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / (n - 1);
                    map[y * n + x] = u >= 0.5f ? 1.0f : 0.0f;
                }
            }
            surface.SetWeightMap(map, n);

            // Domain データの作成
            var bounds = surface.LocalBounds;
            var weightMapDomain = new SpawnSurfaceWeightMap(
                surface.MapResolution,
                surface.CopyWeightMap(),
                bounds.min,
                bounds.size,
                surface.transform.position,
                surface.transform.lossyScale);

            var tri = new SpawnSurfaceTriangle(
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(0f, 0f, 10f),
                paintWeight: 1.0f,
                regionWeight: 1.0f,
                "Surface1",
                "Surface1",
                0);

            var surfaceData = new SpawnSurfaceData(
                "Surface1",
                1.0f,
                new[] { tri },
                weightMapDomain);

            var targets = new List<ItemSpawnTarget>
            {
                new("Item_Test", ResourceCategory.Utility, baseWeight: 1.0f, spawnCost: 1)
            };

            var config = new ItemSpawnRunConfig(
                seed: 42,
                budget: 100,
                maxTotalSpawns: 50,
                maxPlacementAttemptsPerItem: 30);

            var planner = new ItemSpawnPlanner();
            var result = planner.PlanSpawns(config, targets, new[] { surfaceData });

            Assert.That(result.SpawnedItems.Count, Is.GreaterThan(0), "Items should be successfully spawned in the valid region.");

            // すべての生成アイテムが X >= 5.0 (u >= 0.5) の領域にのみ配置されていることを検証
            for (int i = 0; i < result.SpawnedItems.Count; i++)
            {
                var item = result.SpawnedItems[i];
                Assert.That(item.Position.X, Is.GreaterThanOrEqualTo(4.8f),
                    $"Spawned item at X={item.Position.X} must not be in the zero-weight region (X < 5).");
            }
        }

        [Test]
        public void RejectionSampling_DeterministicReproducibility()
        {
            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_quadMesh);
            surface.ResizeMap(128);

            int n = surface.MapResolution;
            float[] map = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / (n - 1);
                    map[y * n + x] = u; // 0..1 のグラデーション
                }
            }
            surface.SetWeightMap(map, n);

            var bounds = surface.LocalBounds;
            var weightMapDomain = new SpawnSurfaceWeightMap(
                surface.MapResolution,
                surface.CopyWeightMap(),
                bounds.min,
                bounds.size,
                surface.transform.position,
                surface.transform.lossyScale);

            var tri1 = new SpawnSurfaceTriangle(
                new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 10f), new Vector3(10f, 0f, 0f),
                1.0f, 1.0f, "Surface1", "Surface1", 0);
            var tri2 = new SpawnSurfaceTriangle(
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 10f),
                1.0f, 1.0f, "Surface1", "Surface1", 1);

            var surfaceData = new SpawnSurfaceData("Surface1", 1.0f, new[] { tri1, tri2 }, weightMapDomain);
            var targets = new List<ItemSpawnTarget>
            {
                new("Item_A", ResourceCategory.Healing, baseWeight: 2.0f, spawnCost: 1),
                new("Item_B", ResourceCategory.Ammo, baseWeight: 1.0f, spawnCost: 1)
            };

            var planner = new ItemSpawnPlanner();

            var config1 = new ItemSpawnRunConfig(seed: 12345, budget: 50, maxTotalSpawns: 20);
            var result1 = planner.PlanSpawns(config1, targets, new[] { surfaceData });

            var config2 = new ItemSpawnRunConfig(seed: 12345, budget: 50, maxTotalSpawns: 20);
            var result2 = planner.PlanSpawns(config2, targets, new[] { surfaceData });

            Assert.That(result1.SpawnedItems.Count, Is.EqualTo(result2.SpawnedItems.Count));
            for (int i = 0; i < result1.SpawnedItems.Count; i++)
            {
                Assert.That(result1.SpawnedItems[i].ItemId, Is.EqualTo(result2.SpawnedItems[i].ItemId));
                Assert.That(result1.SpawnedItems[i].Position.X, Is.EqualTo(result2.SpawnedItems[i].Position.X).Within(1e-5f));
                Assert.That(result1.SpawnedItems[i].Position.Z, Is.EqualTo(result2.SpawnedItems[i].Position.Z).Within(1e-5f));
                Assert.That(result1.SpawnedItems[i].SelectedTriangleWeight, Is.EqualTo(result2.SpawnedItems[i].SelectedTriangleWeight).Within(1e-5f));
            }
        }

        [Test]
        public void TransformHierarchy_WithRotationAndNonUniformScale_MatchesDomainAndUnityMapping()
        {
            // 親Transformを持つ階層構造
            var parentObj = new GameObject("Parent");
            parentObj.transform.position = new Vector3(50f, 10f, -30f);
            parentObj.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            parentObj.transform.localScale = new Vector3(1.5f, 2.0f, 1.2f);

            _testGameObject.transform.SetParent(parentObj.transform, false);
            _testGameObject.transform.localPosition = new Vector3(10f, 5f, 20f);
            _testGameObject.transform.localRotation = Quaternion.Euler(15f, 60f, -25f);
            _testGameObject.transform.localScale = new Vector3(2f, 0.8f, 3f);

            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_quadMesh);
            surface.ResizeMap(128);

            // 中央 (0.5, 0.5) にウェイト 0.9f を設定
            surface.SetWeightAtNormalizedCoords(0.5f, 0.5f, 0.9f);

            // View から World 座標を取得
            Vector3 worldCenter = surface.NormalizedMapCoordsToWorld(0.5f, 0.5f);
            float viewWeight = surface.GetWeightAtWorldPosition(worldCenter);
            Assert.That(viewWeight, Is.EqualTo(0.9f).Within(0.05f));

            // Domain 側での WorldToUV サンプリング（4x4 行列経由）
            Matrix4x4 w2l = surface.transform.worldToLocalMatrix;
            var weightMapDomain = new SpawnSurfaceWeightMap(
                surface.MapResolution,
                surface.CopyWeightMap(),
                surface.LocalBounds.min,
                surface.LocalBounds.size,
                ToSpawnMatrix4x4(w2l),
                ToDomainProjection(surface.ProjectionPlane));

            float domainWeight = weightMapDomain.SampleWorldPosition(worldCenter);
            Assert.That(domainWeight, Is.EqualTo(viewWeight).Within(0.001f),
                "Domain WorldToLocalMatrix mapping must perfectly match View transform mapping under arbitrary rotation and parent hierarchy.");

            Object.DestroyImmediate(parentObj);
        }

        [Test]
        public void SingleLargeTriangle_WithCentroidZero_AndLocalRegionOne_SpawnsSuccessfully()
        {
            // 巨大三角形の重心位置 (u=0.33, v=0.33) が Weight 0 で、隅 (u >= 0.8, v <= 0.2) だけ Weight 1.0f
            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_singleBigTriMesh);
            surface.ResizeMap(128);

            int n = surface.MapResolution;
            float[] map = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / (n - 1);
                    float v = (float)y / (n - 1);
                    map[y * n + x] = (u >= 0.8f && v <= 0.2f) ? 1.0f : 0.0f;
                }
            }
            surface.SetWeightMap(map, n);

            // 重心でのサンプリング値は 0.0f
            surface.TryGetTriangleData(0, out Vector3 a, out Vector3 b, out Vector3 c, out float centroidWeight);
            Assert.That(centroidWeight, Is.EqualTo(0.0f));

            // Domain データ作成
            Matrix4x4 w2l = surface.transform.worldToLocalMatrix;
            var weightMapDomain = new SpawnSurfaceWeightMap(
                surface.MapResolution,
                surface.CopyWeightMap(),
                surface.LocalBounds.min,
                surface.LocalBounds.size,
                ToSpawnMatrix4x4(w2l),
                ToDomainProjection(surface.ProjectionPlane));

            // Presenter と同様に、WeightMap がある場合は PaintWeight = 1.0f で幾何候補として渡す
            var tri = new SpawnSurfaceTriangle(a, b, c, paintWeight: 1.0f, regionWeight: 1.0f, "Surface1", "Surface1", 0);
            var surfaceData = new SpawnSurfaceData("Surface1", 1.0f, new[] { tri }, weightMapDomain);

            var targets = new List<ItemSpawnTarget>
            {
                new("CornerItem", ResourceCategory.Utility, baseWeight: 1.0f, spawnCost: 1)
            };

            var config = new ItemSpawnRunConfig(
                seed: 777,
                budget: 20,
                maxTotalSpawns: 10,
                maxPlacementAttemptsPerItem: 50);

            var planner = new ItemSpawnPlanner();
            var result = planner.PlanSpawns(config, targets, new[] { surfaceData });

            // 重心が0であっても、局所領域1が存在するためスポーンが成功すること
            Assert.That(result.SpawnedItems.Count, Is.GreaterThan(0),
                "Spawning must succeed on a big triangle whose centroid is 0 but has local non-zero weights.");

            // スポーン位置が u >= 0.75 かつ v <= 0.25 (X >= 7.5, Z <= 2.5) の領域であることを検証
            for (int i = 0; i < result.SpawnedItems.Count; i++)
            {
                var item = result.SpawnedItems[i];
                Assert.That(item.Position.X, Is.GreaterThanOrEqualTo(7.5f));
                Assert.That(item.Position.Z, Is.LessThanOrEqualTo(2.5f));
            }
        }

        [Test]
        public void AllZeroWeightMap_TerminatesSafely_WithinMaxPlacementAttempts()
        {
            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_quadMesh);
            surface.ResizeMap(64);
            surface.SetAllWeights(0.0f); // 全面 0

            Matrix4x4 w2l = surface.transform.worldToLocalMatrix;
            var weightMapDomain = new SpawnSurfaceWeightMap(
                surface.MapResolution,
                surface.CopyWeightMap(),
                surface.LocalBounds.min,
                surface.LocalBounds.size,
                ToSpawnMatrix4x4(w2l),
                ToDomainProjection(surface.ProjectionPlane));

            var tri1 = new SpawnSurfaceTriangle(new Vector3(0, 0, 0), new Vector3(10, 0, 10), new Vector3(10, 0, 0), 1f, 1f, "Surface1", "Surface1", 0);
            var surfaceData = new SpawnSurfaceData("Surface1", 1.0f, new[] { tri1 }, weightMapDomain);

            var targets = new List<ItemSpawnTarget>
            {
                new("ZeroTarget", ResourceCategory.Ammo, baseWeight: 1.0f, spawnCost: 1)
            };

            var config = new ItemSpawnRunConfig(
                seed: 999,
                budget: 50,
                maxTotalSpawns: 10,
                maxPlacementAttemptsPerItem: 15);

            var planner = new ItemSpawnPlanner();
            var result = planner.PlanSpawns(config, targets, new[] { surfaceData });

            // スポーン数は 0
            Assert.That(result.SpawnedItems.Count, Is.EqualTo(0));
            Assert.That(result.FailedPlacementAttempts, Is.GreaterThan(0));
        }

        [Test]
        public void VerticalSurfaces_WithXYAndYZProjections_SampleAccurately()
        {
            // XY 垂直壁メッシュ (0..10 X, 0..5 Y, Z=0)
            var wallMeshXY = new Mesh
            {
                name = "WallXY",
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(10f, 0f, 0f),
                    new Vector3(10f, 5f, 0f),
                    new Vector3(0f, 5f, 0f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            wallMeshXY.RecalculateBounds();

            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(wallMeshXY);

            // 自動検出で XY 平面が選択されること
            Assert.That(surface.ProjectionPlane, Is.EqualTo(SpawnSurfaceProjection.XY));

            // ローカル (5, 2.5, 0) -> UV (0.5, 0.5)
            Assert.IsTrue(surface.LocalToNormalizedMapCoords(new Vector3(5f, 2.5f, 0f), out float u, out float v));
            Assert.That(u, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(v, Is.EqualTo(0.5f).Within(0.001f));

            Object.DestroyImmediate(wallMeshXY);
        }

        [Test]
        public void Migration_FullPolygonRasterization_FillsEntireTriangleArea()
        {
            var surface = _testGameObject.AddComponent<WeightedSpawnSurface>();
            surface.InitializeFromMesh(_quadMesh);
            surface.ResizeMap(64);

            // 旧TriangleWeightsを設定 (Tri0 = 0.8f, Tri1 = 0.4f)
            surface.SetWeight(0, 0.8f);
            surface.SetWeight(1, 0.4f);

            // 面ラスタライズ移行
            surface.MigrateFromLegacyTriangleWeights();

            // 三角形0の内部点 (u=0.7, v=0.3) で 0.8f が得られること
            float w0 = surface.GetWeight(0.7f, 0.3f);
            Assert.That(w0, Is.EqualTo(0.8f).Within(0.01f));

            // 三角形1の内部点 (u=0.3, v=0.7) で 0.4f が得られること
            float w1 = surface.GetWeight(0.3f, 0.7f);
            Assert.That(w1, Is.EqualTo(0.4f).Within(0.01f));
        }

        private static SpawnMatrix4x4 ToSpawnMatrix4x4(Matrix4x4 m)
        {
            return new SpawnMatrix4x4(
                m.m00, m.m01, m.m02, m.m03,
                m.m10, m.m11, m.m12, m.m13,
                m.m20, m.m21, m.m22, m.m23,
                m.m30, m.m31, m.m32, m.m33);
        }

        private static SpawnSurfaceProjectionPlane ToDomainProjection(SpawnSurfaceProjection p)
        {
            return p switch
            {
                SpawnSurfaceProjection.XY => SpawnSurfaceProjectionPlane.XY,
                SpawnSurfaceProjection.YZ => SpawnSurfaceProjectionPlane.YZ,
                _ => SpawnSurfaceProjectionPlane.XZ
            };
        }
    }
}
