#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.Interfaces.Tunnel;
using Shinzui.Application.UseCases.Tunnel;
using Shinzui.Presentation.GenerateTunnel;
using Shinzui.View.GenerateTunnel;
using UnityEditor;
using UnityEngine;

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
            serialized.FindProperty("basicTunnelModelScale").vector3Value = new Vector3(3, 1, 1);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _view = _host.AddComponent<TunnelMapView>();
            _view.Configure(generator);
            _view.SetBuildTemplates(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/Prefabs/TunnelBaseModel.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/3DModels/Tunnel_Path_Long.fbx"));
            _presenter = new GenerateTunnelPresenter(new GenerateTunnelUseCase(null), _view, new TestNavigation());
            Assert.That(_presenter.ExecuteGeneration(new TunnelGenerationRequestDto
            {
                GenerateUpperFloor = true,
                UpperFloorHeight = 14,
                SmallRoomWidth = 20,
                SmallRoomLength = 20,
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
            Assert.That(_presenter.CurrentMap.DropOpenings.Count, Is.EqualTo(2));
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
            var map = _presenter.CurrentMap;
            var ramp = map.Tunnels.Single(t => t.Slope != 0);
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

            // 側面口からトンネルの中央へ入り、上端を越えるまで移動する
            Move(controller, -side, map.Dimensions.TunnelWidth * 0.5f + 1.0f);
            float horizontalLength = ramp.Length / Mathf.Sqrt(1 + ramp.Slope * ramp.Slope);
            float run = horizontalLength * 0.5f - Mathf.Sign(ramp.Slope) * mouth.LocalPosZ;
            Move(controller, forward * Mathf.Sign(ramp.Slope), run + 2.0f);
            Assert.That(controller.transform.position.y, Is.GreaterThanOrEqualTo(13.5f));
            Assert.That(Vector3.Dot(controller.transform.position - center, forward * Mathf.Sign(ramp.Slope)),
                Is.GreaterThan(horizontalLength * 0.5f + 0.3f), "The capsule must cross the upper doorway, not stop in front of it");
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
