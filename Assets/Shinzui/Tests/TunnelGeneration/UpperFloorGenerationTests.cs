#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.Interfaces.Tunnel;
using Shinzui.Application.UseCases.Tunnel;
using Shinzui.Presentation;
using Shinzui.Presentation.GenerateTunnel;
using Shinzui.View;
using Shinzui.View.GenerateTunnel;
using UnityEditor;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shinzui.Tests.TunnelGeneration
{
    public sealed class UpperFloorGenerationTests
    {
        private GameObject _host;
        private GameObject _player;
        private TunnelMapView _view;
        private GenerateTunnelPresenter _presenter;

        /// <summary>
        /// 実際のトンネルモデルを使って上下階を生成する
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Upper Floor Geometry Test");
            var generator = _host.AddComponent<TunnelGenerator>();
            var serialized = new SerializedObject(generator);
            serialized.FindProperty("basicTunnelModelScale").vector3Value = new Vector3(1, 0.75f, 0.75f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _view = _host.AddComponent<TunnelMapView>();
            _view.Configure(generator);
            _view.SetBuildTemplates(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/Prefabs/TunnelBaseModel.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/3DModels/Tunnel_Path_Long.fbx"));
            _presenter = new GenerateTunnelPresenter(new GenerateTunnelUseCase(null), _view, new TestNavigation());
            Assert.That(_presenter.ExecuteGeneration(new TunnelGenerationRequestDto
            {
                GenerateUpperFloor = true,
                GenerateLowerFloor = true,
                UpperFloorHeight = 14,
                LowerFloorDepth = 14,
                TunnelLength = 40,
                MinTunnelLength = 60,
                MaxTunnelLength = 76,
                CorridorLength = 4,
                Seed = 2777
            }), Is.True);
            Physics.SyncTransforms();
        }

        /// <summary>
        /// テストで生成した階層と当たり判定を破棄する
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _presenter?.Dispose();
            if (_player != null) Object.DestroyImmediate(_player);
            if (_view != null && _view.MapRootObject != null) Object.DestroyImmediate(_view.MapRootObject);
            if (_host != null) Object.DestroyImmediate(_host);
        }

        /// <summary>
        /// 落下口の中心で上階の床と下階の天井を通り抜けて着地できることを確認する
        /// </summary>
        [Test]
        public void ProductionGeometry_DropsReachTheLowerFloor()
        {
            Assert.That(_presenter.CurrentMap.DropOpenings.Count, Is.EqualTo(4));
            foreach (var opening in _presenter.CurrentMap.DropOpenings)
            {
                var origin = new Vector3(opening.CenterX, opening.UpperFloorY + 2.2f, opening.CenterZ);
                Assert.That(Physics.Raycast(origin, Vector3.down, out var hit, 30), Is.True);
                Assert.That(hit.point.y, Is.LessThan(opening.LowerFloorY + 2.0f), "A stale floor or ceiling collider seals the shaft");

                var controller = CreatePlayer(origin);
                for (int i = 0; i < 200; i++) controller.Move(Vector3.down * 0.15f);
                Assert.That(controller.transform.position.y, Is.InRange(opening.LowerFloorY - 0.1f, opening.LowerFloorY + 2.0f));
                Object.DestroyImmediate(_player);
                _player = null;
            }
        }

        /// <summary>
        /// 下階の接続口から傾斜を登り上階の入口へ歩いて移動できることを確認する
        /// </summary>
        [Test]
        public void ProductionGeometry_CharacterCanWalkFromLowerMouthToUpperFloor()
        {
            var ramp = _presenter.CurrentMap.Tunnels.Single(t => t.Slope != 0 && t.Slope * t.SlopeExitSign > 0);
            AssertRampTraversal(ramp, 14.0f, false);
        }

        /// <summary>
        /// 下階へ下ってから通常階へ歩いて戻れることを確認する
        /// </summary>
        [Test]
        public void ProductionGeometry_CharacterCanDescendAndReturnToBaseFloor()
        {
            var ramp = _presenter.CurrentMap.Tunnels.Single(t => t.Slope != 0 && t.Slope * t.SlopeExitSign < 0);
            AssertRampTraversal(ramp, -14.0f, true);
        }

        /// <summary>
        /// 異なる長さと向きのトンネルでも接続口の床高と内幅が通路に一致することを確認する
        /// </summary>
        [Test]
        public void ProductionGeometry_AllFlatMouthsMatchCorridorDimensions()
        {
            var map = _presenter.CurrentMap;
            Assert.That(map.Tunnels.Count, Is.EqualTo(24));
            Assert.That(map.Tunnels.Where(t => t.Slope == 0).Select(t => t.Length).Distinct().Count(), Is.GreaterThan(1));
            Assert.That(map.Dimensions.CorridorLength, Is.EqualTo(4));
            Assert.That(map.Dimensions.CorridorWidth, Is.EqualTo(3));

            foreach (var tunnel in map.Tunnels.Where(t => t.Slope == 0))
            foreach (var marker in tunnel.EntranceMarkers.Where(m => m.IsOpen))
            {
                Vector3 forward = new Vector3(tunnel.DirX, 0, tunnel.DirZ);
                Vector3 side = new Vector3(tunnel.DirZ, 0, -tunnel.DirX) * (marker.Index < 3 ? -1 : 1);
                Vector3 port = new Vector3(tunnel.PositionX, tunnel.PositionY, tunnel.PositionZ)
                    + forward * marker.LocalPosZ + side * (map.Dimensions.TunnelWidth * 0.5f - 0.1f);
                Assert.That(Physics.Raycast(port + Vector3.up * 1.0f, Vector3.down, out var floor, 2), Is.True, tunnel.Name);
                Assert.That(floor.point.y, Is.EqualTo(tunnel.PositionY + 0.15f).Within(0.03f), tunnel.Name);
                foreach (int sign in new[] { -1, 1 })
                {
                    Assert.That(Physics.Raycast(port + Vector3.up, forward * sign, out var wall, 4), Is.True, tunnel.Name);
                    Assert.That(wall.distance, Is.EqualTo(map.Dimensions.CorridorWidth * 0.5f).Within(0.06f),
                        $"{tunnel.Name} mouth {marker.Index}, sign {sign}, collider {wall.collider.name}, point {wall.point}, port {port}");
                }
            }
        }

        /// <summary>
        /// 平坦な各接続口を通路側から歩いて通過できることを確認する
        /// </summary>
        [Test]
        public void ProductionGeometry_CharacterCanCrossFlatConnections()
        {
            var map = _presenter.CurrentMap;
            foreach (var tunnel in map.Tunnels.Where(t => t.Slope == 0))
            foreach (var marker in tunnel.EntranceMarkers.Where(m => m.IsOpen))
            {
                Vector3 forward = new Vector3(tunnel.DirX, 0, tunnel.DirZ);
                Vector3 side = new Vector3(tunnel.DirZ, 0, -tunnel.DirX) * (marker.Index < 3 ? -1 : 1);
                Vector3 port = new Vector3(tunnel.PositionX, tunnel.PositionY, tunnel.PositionZ)
                    + forward * marker.LocalPosZ + side * (map.Dimensions.TunnelWidth * 0.5f);
                Vector3 start = port + side * 0.6f + Vector3.up * 0.2f;
                var controller = CreatePlayer(start);
                float distance = map.Dimensions.TunnelWidth * 0.4f;
                Move(controller, -side, distance);
                Assert.That(Vector3.Dot(controller.transform.position - start, -side), Is.GreaterThan(distance - 0.3f),
                    $"{tunnel.Name} mouth {marker.Index}");
                Assert.That(controller.transform.position.y, Is.InRange(tunnel.PositionY - 0.1f, tunnel.PositionY + 0.4f));
                Object.DestroyImmediate(_player);
                _player = null;
            }
        }

        /// <summary>
        /// ループ用ゲートが全接続口より外側のトンネル端へ追従することを確認する
        /// </summary>
        [Test]
        public void ProductionGeometry_LoopGatesRemainBeyondAllSideMouths()
        {
            foreach (var tunnel in _presenter.CurrentMap.Tunnels.Where(t => t.Slope == 0))
            {
                Transform root = _view.GeometryRoot.Find(tunnel.Name);
                var gates = root.GetComponentsInChildren<TunnelGateView>(true);
                Assert.That(gates.Length, Is.EqualTo(2), tunnel.Name);
                foreach (var gate in gates)
                {
                    var box = gate.GetComponent<BoxCollider>();
                    Vector3 center = root.InverseTransformPoint(gate.transform.TransformPoint(box.center));
                    float halfDepth = root.InverseTransformVector(gate.transform.TransformVector(Vector3.forward * box.size.z)).magnitude * 0.5f;
                    float outerMouth = tunnel.EntranceMarkers.Max(m => Mathf.Abs(m.LocalPosZ))
                        + _presenter.CurrentMap.Dimensions.CorridorWidth * 0.5f;
                    Assert.That(Mathf.Abs(center.z), Is.EqualTo(tunnel.Length * 0.5f).Within(0.01f), tunnel.Name);
                    Assert.That(Mathf.Abs(center.z) - halfDepth, Is.GreaterThan(outerMouth + 0.5f), tunnel.Name);
                    Assert.That(gate.TargetGate, Is.Not.Null, tunnel.Name);
                    Assert.That(gate.TargetGate.TargetGate, Is.EqualTo(gate), tunnel.Name);
                }
            }
        }

        /// <summary>
        /// 断面の縦横倍率は設定値が異なる場合も同じ倍率になることを確認する
        /// </summary>
        [Test]
        public void TunnelScale_PreservesOriginalCrossSectionRatio()
        {
            var generator = _host.GetComponent<TunnelGenerator>();
            var serialized = new SerializedObject(generator);
            serialized.FindProperty("basicTunnelModelScale").vector3Value = new Vector3(2, 0.8f, 0.4f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(generator.BasicTunnelModelScale, Is.EqualTo(new Vector3(2, 0.8f, 0.8f)));
        }

        /// <summary>
        /// 通路の向きや階層が異なってもペア先の通路内へワープすることを確認する
        /// </summary>
        /// <param name="angle">ペア先の相対角度</param>
        [TestCase(0)]
        [TestCase(90)]
        [TestCase(180)]
        public void ProductionGeometry_WarpArrivesInsidePairedCorridor(float angle)
        {
            var warps = _view.GeometryRoot.GetComponentsInChildren<GeneratedCorridorInfo>()
                .Where(c => c.Kind == GeneratedCorridorKind.Warp).ToArray();
            Assert.That(warps, Is.Not.Empty);
            foreach (var corridor in warps)
            {
                Assert.That(corridor.PairedCorridor, Is.Not.Null);
                Assert.That(corridor.PairedCorridor.WarpPairId, Is.EqualTo(corridor.WarpPairId));
                Assert.That(corridor.PairedCorridor.PairedCorridor, Is.SameAs(corridor));
            }

            var source = warps[0];
            var target = source.PairedCorridor;
            target.transform.rotation = source.transform.rotation * Quaternion.Euler(0, angle, 0);
            target.transform.position += Vector3.up * 14;
            var controller = CreatePlayer(source.transform.TransformPoint(new Vector3(0.3f, 0.2f, -0.9f)));
            _player.AddComponent<PlayerView>();
            source.GetComponentInChildren<GeneratedWarpCorridorTrigger>().SendMessage("OnTriggerEnter", controller);

            Vector3 arrival = target.transform.InverseTransformPoint(_player.transform.position);
            Assert.That(Vector3.Distance(arrival, new Vector3(0, 0.2f, -1.25f)), Is.LessThan(0.001f));
            Assert.That(Physics.Raycast(_player.transform.position + Vector3.up, Vector3.down, out var floor, 2), Is.True);
            Assert.That(floor.transform.IsChildOf(target.transform), Is.True);
        }

        /// <summary>
        /// クールダウン後にペア先から元の通路へ戻れることを確認する
        /// </summary>
        [UnityTest]
        public IEnumerator ProductionGeometry_WarpCanReturnThroughItsPair()
        {
            var source = _view.GeometryRoot.GetComponentsInChildren<GeneratedCorridorInfo>()
                .First(c => c.Kind == GeneratedCorridorKind.Warp);
            var target = source.PairedCorridor;
            var controller = CreatePlayer(source.transform.TransformPoint(new Vector3(-0.3f, 0.2f, -0.9f)));
            _player.AddComponent<PlayerView>();
            var sourceTrigger = source.GetComponentInChildren<GeneratedWarpCorridorTrigger>();
            sourceTrigger.SendMessage("OnTriggerEnter", controller);
            Vector3 arrival = _player.transform.position;
            target.GetComponentInChildren<GeneratedWarpCorridorTrigger>().SendMessage("OnTriggerEnter", controller);
            Assert.That(_player.transform.position, Is.EqualTo(arrival), "Immediate contact must not cause a return warp");

            yield return new WaitForSeconds(0.4f);
            _player.transform.position = target.transform.TransformPoint(new Vector3(0, 0.2f, -0.9f));
            target.GetComponentInChildren<GeneratedWarpCorridorTrigger>().SendMessage("OnTriggerEnter", controller);
            Assert.That(Vector3.Distance(source.transform.InverseTransformPoint(_player.transform.position),
                new Vector3(0, 0.2f, -1.25f)), Is.LessThan(0.001f));
        }

        /// <summary>
        /// 外部ワープで通過したループゲートが二重ワープを起こさないことを確認する
        /// </summary>
        [UnityTest]
        public IEnumerator ProductionGeometry_ExternalWarpDoesNotSweepThroughLoopGates()
        {
            CreatePlayer(new Vector3(3000, 1, 0));
            var player = _player.AddComponent<PlayerView>();
            TunnelGateView first = CreateLoopGate(new Vector3(3000, 5, 50));
            TunnelGateView second = CreateLoopGate(new Vector3(3000, 5, 500));
            var serialized = new SerializedObject(first);
            serialized.FindProperty("targetGate").objectReferenceValue = second;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var loop = new TunnelLoopPresenter(player);
            loop.Initialize();
            try
            {
                yield return new WaitForSeconds(0.2f);
                player.Warp(new Vector3(0, 0, 100));
                loop.Tick();
                Assert.That(_player.transform.position.z, Is.EqualTo(100).Within(0.001f));
                yield return new WaitForSeconds(0.2f);
                loop.Tick();
                Assert.That(_player.transform.position.z, Is.EqualTo(100).Within(0.001f));

                // クールダウン後に実際にゲートを跨ぐ移動は従来どおりループする
                _player.transform.position = new Vector3(3000, 1, 51);
                loop.Tick();
                _player.transform.position = new Vector3(3000, 1, 49);
                loop.Tick();
                Assert.That(_player.transform.position.z, Is.EqualTo(498.98f).Within(0.001f));
            }
            finally { loop.Dispose(); }
        }

        /// <summary>
        /// 現在と直前のシードで全ワープ通路をループ判定と併用して検証する
        /// </summary>
        [UnityTest]
        public IEnumerator ProductionGeometry_AllWarpPairsRemainInDestinationCorridors()
        {
            var controller = CreatePlayer(Vector3.zero);
            var player = _player.AddComponent<PlayerView>();
            var loop = new TunnelLoopPresenter(player);
            loop.Initialize();
            try
            {
                foreach (int seed in new[] { 2776, 2777 })
                {
                    _presenter.ExecuteGeneration(new TunnelGenerationRequestDto
                    {
                        Seed = seed, GenerateUpperFloor = true, GenerateLowerFloor = true,
                        UpperFloorHeight = 14, LowerFloorDepth = 14,
                        TunnelLength = 40, MinTunnelLength = 60, MaxTunnelLength = 76, CorridorLength = 4
                    }, true);
                    var warps = _view.GeometryRoot.GetComponentsInChildren<GeneratedCorridorInfo>()
                        .Where(c => c.Kind == GeneratedCorridorKind.Warp).ToArray();
                    Assert.That(warps, Is.Not.Empty);
                    foreach (var source in warps)
                    {
                        Assert.That(source.PairedCorridor, Is.Not.Null, $"seed {seed}: {source.name}");
                        yield return new WaitForSeconds(0.4f);
                        _player.transform.position = source.transform.TransformPoint(new Vector3(0.3f, 0.2f, -0.9f));
                        loop.Initialize();
                        source.GetComponentInChildren<GeneratedWarpCorridorTrigger>().SendMessage("OnTriggerEnter", controller);
                        loop.Tick();
                        Transform target = source.PairedCorridor.transform;
                        Assert.That(Vector3.Distance(target.InverseTransformPoint(_player.transform.position),
                            new Vector3(0, 0.2f, -1.25f)), Is.LessThan(0.001f), $"seed {seed}: {source.name}");
                        Assert.That(Physics.Raycast(_player.transform.position + Vector3.up, Vector3.down, out var floor, 2), Is.True);
                        Assert.That(floor.transform.IsChildOf(target), Is.True, $"seed {seed}: {source.name}");
                        Assert.That(Vector3.Dot(_player.transform.forward, -target.forward), Is.GreaterThan(0.999f));
                    }
                }
            }
            finally { loop.Dispose(); }
        }

        /// <summary>
        /// 本体とカメラ制御が出口方向へ揃い、次フレームにも維持されることを確認する
        /// </summary>
        /// <param name="yaw">出口の向き</param>
        /// <param name="parented">カメラをプレイヤーの子にするか</param>
        [TestCase(0, true)]
        [TestCase(90, false)]
        [TestCase(180, true)]
        [TestCase(270, false)]
        public void ProductionGeometry_WarpAlignsPlayerAndCinemachine(float yaw, bool parented)
        {
            CreatePlayer(new Vector3(3000, 0.2f, 0));
            var player = _player.AddComponent<PlayerView>();
            var head = new GameObject("Warp Camera Target").transform;
            head.SetParent(_player.transform, false);
            head.localPosition = Vector3.up * 1.6f;
            var camera = new GameObject("Warp Camera").AddComponent<Camera>();
            camera.transform.SetParent(parented ? head : _host.transform, false);
            camera.transform.position = head.position;
            camera.transform.rotation = Quaternion.Euler(20, 35, 0);
            var serialized = new SerializedObject(player);
            serialized.FindProperty("mainCamera").objectReferenceValue = camera;
            serialized.FindProperty("cameraTarget").objectReferenceValue = head;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var virtualCamera = new GameObject("Warp Virtual Camera").AddComponent<CinemachineCamera>();
            virtualCamera.transform.SetParent(_host.transform, false);
            virtualCamera.Follow = head;
            var panTilt = virtualCamera.gameObject.AddComponent<CinemachinePanTilt>();
            panTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.World;
            virtualCamera.InternalUpdateCameraState(Vector3.up, -1);
            virtualCamera.ForceCameraPosition(camera.transform.position, camera.transform.rotation);

            Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            player.Warp(Vector3.forward * 100, forward);
            Assert.That(Vector3.Dot(player.transform.forward, forward), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(Vector3.ProjectOnPlane(player.CameraForward, Vector3.up).normalized, forward), Is.GreaterThan(0.999f));
            Assert.That(Mathf.DeltaAngle(camera.transform.eulerAngles.x, 20), Is.EqualTo(0).Within(0.01f));
            virtualCamera.InternalUpdateCameraState(Vector3.up, -1);
            Vector3 nextForward = virtualCamera.State.RawOrientation * Vector3.forward;
            Assert.That(Vector3.Dot(Vector3.ProjectOnPlane(nextForward, Vector3.up).normalized, forward), Is.GreaterThan(0.999f));
        }

        /// <summary>
        /// ループ判定用のゲートを作成する
        /// </summary>
        /// <param name="position">配置する位置</param>
        /// <returns>ループ判定用のゲート</returns>
        private TunnelGateView CreateLoopGate(Vector3 position)
        {
            var gate = new GameObject("Warp Sweep Test Gate");
            gate.transform.SetParent(_host.transform, false);
            gate.transform.position = position;
            gate.AddComponent<BoxCollider>().size = new Vector3(10, 10, 0.2f);
            return gate.AddComponent<TunnelGateView>();
        }

        /// <summary>
        /// 通路の天井を側面口に合わせ、接続面の上向き視線が外へ抜けないことを確認する
        /// </summary>
        [Test]
        public void ProductionGeometry_PassageRoofsCloseMouthSeams()
        {
            var map = _presenter.CurrentMap;
            float height = _view.ResolvePassageHeight(map.Dimensions.TunnelHeight);
            Assert.That(height, Is.LessThan(map.Dimensions.TunnelHeight));
            foreach (var tunnel in map.Tunnels.Where(t => t.Slope == 0))
            foreach (var marker in tunnel.EntranceMarkers.Where(m => m.IsOpen))
            {
                Vector3 forward = new Vector3(tunnel.DirX, 0, tunnel.DirZ);
                Vector3 side = new Vector3(tunnel.DirZ, 0, -tunnel.DirX) * (marker.Index < 3 ? -1 : 1);
                Vector3 port = new Vector3(tunnel.PositionX, tunnel.PositionY, tunnel.PositionZ)
                    + forward * marker.LocalPosZ + side * (map.Dimensions.TunnelWidth * 0.5f);
                foreach (float offset in new[] { -0.02f, 0.02f })
                foreach (float lateral in new[] { -1.3f, 0, 1.3f })
                {
                    Vector3 origin = port + side * offset + forward * lateral + Vector3.up * 0.65f;
                    Assert.That(Physics.Raycast(origin, Vector3.up, out var hit, height), Is.True, tunnel.Name);
                    Assert.That(hit.point.y, Is.LessThanOrEqualTo(tunnel.PositionY + height + 0.02f), tunnel.Name);
                }
            }
        }

        /// <summary>
        /// 落下口の囲いが下階のアーチ天井より内側へ垂れ下がらないことを確認する
        /// </summary>
        [Test]
        public void ProductionGeometry_DropShaftEndsFollowLowerCeiling()
        {
            foreach (var opening in _presenter.CurrentMap.DropOpenings)
            {
                var tunnel = _presenter.CurrentMap.Tunnels.Single(t => t.Id == opening.LowerTunnelId);
                Transform lower = _view.GeometryRoot.Find(tunnel.Name);
                Transform shaft = _view.GeometryRoot.Cast<Transform>().Single(t => t.name == "Drop Opening Shaft"
                    && Mathf.Abs(t.localPosition.x - opening.CenterX) < 0.01f && Mathf.Abs(t.localPosition.z - opening.CenterZ) < 0.01f);
                int checkedVertices = 0;
                foreach (var filter in shaft.GetComponentsInChildren<MeshFilter>())
                {
                    Vector3[] vertices = filter.sharedMesh.vertices;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        if (i % 4 >= 2) continue;
                        Vector3 point = filter.transform.TransformPoint(vertices[i]);
                        if (Mathf.Abs(point.x - opening.CenterX) < opening.Size * 0.5f + 0.19f
                            && Mathf.Abs(point.z - opening.CenterZ) < opening.Size * 0.5f + 0.19f) continue;
                        var ray = new Ray(new Vector3(point.x, opening.LowerFloorY + 0.65f, point.z), Vector3.up);
                        float ceiling = float.PositiveInfinity;
                        foreach (var collider in lower.GetComponentsInChildren<Collider>())
                            if (!collider.isTrigger && collider.Raycast(ray, out var hit, 12) && hit.normal.y < -0.1f)
                                ceiling = Mathf.Min(ceiling, hit.point.y);
                        Assert.That(ceiling, Is.Not.EqualTo(float.PositiveInfinity), tunnel.Name);
                        Assert.That(point.y, Is.EqualTo(ceiling - 0.01f).Within(0.03f), tunnel.Name);
                        checkedVertices++;
                    }
                }
                Assert.That(checkedVertices, Is.GreaterThan(0));
            }
        }

        /// <summary>
        /// 接続口から隣接階層へ歩き、必要に応じて通常階へ戻る
        /// </summary>
        /// <param name="ramp">歩行する傾斜トンネル</param>
        /// <param name="targetFloorY">接続先の床高</param>
        /// <param name="returnToBase">通常階へ戻るか</param>
        private void AssertRampTraversal(TunnelNodeDto ramp, float targetFloorY, bool returnToBase)
        {
            var map = _presenter.CurrentMap;
            var mouth = ramp.EntranceMarkers.Single(m => m.IsOpen);
            var forward = new Vector3(ramp.DirX, 0, ramp.DirZ);
            var side = new Vector3(ramp.DirZ, 0, -ramp.DirX) * (mouth.Index < 3 ? -1 : 1);
            var center = new Vector3(ramp.PositionX, ramp.PositionY, ramp.PositionZ);
            var lowPort = center + forward * mouth.LocalPosZ + side * (map.Dimensions.TunnelWidth * 0.5f);
            var start = lowPort + side;
            start.y = 2.2f;
            Assert.That(Physics.Raycast(start, Vector3.down, out var floor, 5), Is.True);
            start.y = floor.point.y + 0.05f;
            var controller = CreatePlayer(start);

            // 側面口から中央へ入り、上下の接続先の入口を越えるまで移動する
            Move(controller, -side, map.Dimensions.TunnelWidth * 0.5f + 1.0f);
            float horizontalLength = ramp.Length / Mathf.Sqrt(1 + ramp.Slope * ramp.Slope);
            float run = horizontalLength * 0.5f - ramp.SlopeExitSign * mouth.LocalPosZ;
            Move(controller, forward * ramp.SlopeExitSign, run + 2.0f);
            Assert.That(controller.transform.position.y, Is.InRange(targetFloorY - 0.5f, targetFloorY + 2.0f));
            Assert.That(Vector3.Dot(controller.transform.position - center, forward * ramp.SlopeExitSign),
                Is.GreaterThan(horizontalLength * 0.5f + 0.3f), "The capsule must cross the upper doorway, not stop in front of it");
            if (returnToBase)
            {
                Move(controller, -forward * ramp.SlopeExitSign, run + 2.0f);
                Assert.That(controller.transform.position.y, Is.InRange(-0.1f, 2.0f));
            }
        }

        /// <summary>
        /// テスト用の歩行カプセルを配置する
        /// </summary>
        /// <param name="position">足元の位置</param>
        /// <returns>歩行の当たり判定</returns>
        private CharacterController CreatePlayer(Vector3 position)
        {
            _player = new GameObject("Upper Floor Test Capsule");
            _player.transform.position = position;
            var controller = _player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = Vector3.up * 0.9f;
            controller.slopeLimit = 45;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.03f;
            Physics.SyncTransforms();
            return controller;
        }

        /// <summary>
        /// 重力をかけながら一定距離だけ歩く
        /// </summary>
        /// <param name="controller">歩行する当たり判定</param>
        /// <param name="direction">水平の移動方向</param>
        /// <param name="distance">移動距離</param>
        private static void Move(CharacterController controller, Vector3 direction, float distance)
        {
            for (float remaining = distance; remaining > 0.001f; remaining -= 0.15f)
                controller.Move(direction * Mathf.Min(remaining, 0.15f) + Vector3.down * 0.08f);
        }

        private sealed class TestNavigation : ITunnelNavigationBuilder
        {
            /// <summary>
            /// 物理形状のテストではナビゲーションのベイクを省略する
            /// </summary>
            /// <returns>生成を続行するためtrue</returns>
            public bool BuildNavMesh() => true;
        }
    }
}
#endif
