#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.Interfaces.Tunnel;
using Shinzui.Application.UseCases.Tunnel;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.Domain.Entities.Inventory;
using Shinzui.Domain.ValueObjects.Inventory;
using Shinzui.DI;
using Shinzui.Temp;
using Shinzui.Presentation.GenerateTunnel;
using Shinzui.View;
using Shinzui.View.GenerateTunnel;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Shinzui.Tests.TunnelGeneration
{
    public sealed class TunnelGoalGenerationTests
    {
        private GameObject _host;
        private TunnelMapView _view;
        private GenerateTunnelPresenter _presenter;

        /// <summary>
        /// 実モデルを使って上下階とゴールを生成する
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Tunnel Goal Test");
            var generator = _host.AddComponent<TunnelGenerator>();
            var settings = new SerializedObject(generator);
            settings.FindProperty("basicTunnelModelScale").vector3Value = new Vector3(1, 0.75f, 0.75f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            _view = _host.AddComponent<TunnelMapView>();
            _view.Configure(generator);
            _view.SetBuildTemplates(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/Prefabs/TunnelBaseModel.prefab"), null);
            _presenter = new GenerateTunnelPresenter(new GenerateTunnelUseCase(null), _view, new TestNavigation());
            Assert.That(_presenter.ExecuteGeneration(new TunnelGenerationRequestDto
            {
                GenerateUpperFloor = true, GenerateLowerFloor = true, UpperFloorHeight = 14, LowerFloorDepth = 14,
                TunnelLength = 40, MinTunnelLength = 60, MaxTunnelLength = 76, CorridorLength = 4
            }), Is.True);
        }

        /// <summary>
        /// 生成物と購読を破棄する
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _presenter?.Dispose();
            if (_view != null && _view.MapRootObject != null) Object.DestroyImmediate(_view.MapRootObject);
            if (_host != null) Object.DestroyImmediate(_host);
        }

        /// <summary>
        /// 一つのトンネルの片端だけにゴールがあり、そのトンネルだけループしないことを確認する
        /// </summary>
        [Test]
        public void GoalIsUniqueAndDisablesOnlyItsTunnelLoop()
        {
            var goals = _view.GeometryRoot.GetComponentsInChildren<GeneratedTunnelGoal>();
            Assert.That(goals.Length, Is.EqualTo(1));
            Transform block = goals[0].transform.parent;
            Transform root = block.parent;
            var node = _presenter.CurrentMap.Tunnels.Single(t => t.Name == root.name);
            Assert.That(node.Slope, Is.Zero);
            Assert.That(node.IsSpecial, Is.False);
            Assert.That(Mathf.Abs(block.localPosition.z) + block.localScale.z * 0.5f,
                Is.EqualTo(node.Length * 0.5f).Within(0.01f));
            Assert.That(root.Find("Goal Tunnel End Wall"), Is.Not.Null);
            var gates = root.GetComponentsInChildren<TunnelGateView>(true);
            Assert.That(gates.Length, Is.EqualTo(2));
            Assert.That(gates.All(g => !g.gameObject.activeInHierarchy), Is.True);
            Assert.That(_view.GeometryRoot.GetComponentsInChildren<TunnelGateView>().Length, Is.GreaterThan(0));
        }

        /// <summary>
        /// プレイヤー以外の接触では遷移しないことを確認する
        /// </summary>
        [Test]
        public void NonPlayerCannotActivateGoal()
        {
            var other = new GameObject("Non Player");
            other.transform.SetParent(_host.transform, false);
            var goal = _view.GeometryRoot.GetComponentInChildren<GeneratedTunnelGoal>();
            goal.SendMessage("OnTriggerEnter", other.AddComponent<BoxCollider>());
            Assert.That(goal.IsTransitioning, Is.False);
        }

        /// <summary>
        /// ドアを迂回してゴールへ触れても開錠前は遷移しないことを確認する
        /// </summary>
        [Test]
        public void PlayerCannotActivateLockedGoal()
        {
            var player = new GameObject("Locked Goal Player");
            player.transform.SetParent(_host.transform, false);
            player.AddComponent<PlayerView>();
            var goal = _view.GeometryRoot.GetComponentInChildren<GeneratedTunnelGoal>();
            goal.SendMessage("OnTriggerEnter", player.AddComponent<BoxCollider>());
            Assert.That(goal.IsUnlocked, Is.False);
            Assert.That(goal.IsTransitioning, Is.False);
        }

        /// <summary>
        /// 鍵とドアがゴール手前に並び、鍵がない場合は開錠できないことを確認する
        /// </summary>
        [Test]
        public void KeyAndDoorArePlacedBeforeLockedGoal()
        {
            var goal = _view.GeometryRoot.GetComponentInChildren<GeneratedTunnelGoal>();
            var spawner = _host.AddComponent<TempEscapeSpawner>();
            spawner.enabled = false;
            spawner.SpawnGoalItems(goal);
            var key = spawner.GetComponentInChildren<TempEscapeKeyItem>();
            var door = spawner.GetComponentInChildren<TempEscapeExit>();
            Transform block = goal.transform.parent;
            Transform tunnel = block.parent;
            float sign = Mathf.Sign(block.localPosition.z);
            float keyZ = sign * tunnel.InverseTransformPoint(key.transform.position).z;
            float doorZ = sign * tunnel.InverseTransformPoint(door.transform.position).z;
            Assert.That(keyZ, Is.LessThan(doorZ));
            Assert.That(doorZ, Is.LessThan(Mathf.Abs(block.localPosition.z)));
            Assert.That(key.InteractableId, Is.EqualTo(TempEscapeKey.ItemId));
            door.ExecuteInteractEffect();
            Assert.That(goal.IsUnlocked, Is.False);
            Assert.That(door.CanInteract, Is.True);
        }

        /// <summary>
        /// 取得した既存の鍵を1本消費してドアが開き、ゴールが有効になることを確認する
        /// </summary>
        [UnityTest]
        public IEnumerator CollectedKeyOpensGoalDoor()
        {
            var scopeObject = new GameObject("Goal Inventory");
            scopeObject.SetActive(false);
            scopeObject.transform.SetParent(_host.transform, false);
            var scope = scopeObject.AddComponent<GoalInventoryScope>();
            scope.Inventory = new InventoryUseCase(new InventoryEntity(2), new GoalItemCatalog(), new GoalInventoryRepository(), null);
            scopeObject.SetActive(true);
            var goal = _view.GeometryRoot.GetComponentInChildren<GeneratedTunnelGoal>();
            var spawner = _host.AddComponent<TempEscapeSpawner>();
            spawner.enabled = false;
            spawner.SpawnGoalItems(goal);
            var key = spawner.GetComponentInChildren<TempEscapeKeyItem>();
            var door = spawner.GetComponentInChildren<TempEscapeExit>();

            var acquire = scope.Inventory.AddItemAsync(key.InteractableId, 1);
            yield return new WaitUntil(() => acquire.IsCompleted);
            Assert.That(acquire.Result, Is.True);
            key.ExecuteInteractEffect();
            Assert.That(TempEscapeKey.HasKey(), Is.True);
            Scene sceneBefore = SceneManager.GetActiveScene();
            var ray = new Ray(door.GetComponentInChildren<Renderer>().bounds.center + door.transform.forward * 0.6f,
                -door.transform.forward);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(ray, out var closedHit, 1.2f), Is.True);
            Assert.That(closedHit.collider.GetComponentInParent<TempEscapeExit>(), Is.SameAs(door));
            door.ExecuteInteractEffect();
            float deadline = Time.realtimeSinceStartup + 3;
            while (!goal.IsUnlocked && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(goal.IsUnlocked, Is.True);
            Assert.That(TempEscapeKey.HasKey(), Is.False);
            Assert.That(door.CanInteract, Is.False);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(ray, 1.2f), Is.False, "The opened door must leave the central passage clear");
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(sceneBefore.handle));
        }

        /// <summary>
        /// プレイヤーの接触でStageSelectへ実際に遷移することを確認する
        /// </summary>
        [UnityTest]
        public IEnumerator PlayerContactLoadsStageSelect()
        {
            var player = new GameObject("Goal Test Player");
            player.transform.SetParent(_host.transform, false);
            player.AddComponent<PlayerView>();
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) player.layer = playerLayer;
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = Vector3.up * 0.9f;
            controller.radius = 0.3f;
            var goal = _view.GeometryRoot.GetComponentInChildren<GeneratedTunnelGoal>();
            goal.Unlock();
            Transform block = goal.transform.parent;
            Transform tunnel = block.parent;
            float sign = Mathf.Sign(block.localPosition.z);
            player.transform.position = tunnel.TransformPoint(new Vector3(block.localPosition.x, 0.2f,
                block.localPosition.z - sign * 3));
            Vector3 direction = tunnel.forward * sign;
            Physics.SyncTransforms();

            // 接触イベントを直接呼ばず、実際にブロックへ歩いて判定する
            for (int i = 0; i < 40 && goal != null && !goal.IsTransitioning; i++)
            {
                controller.Move(direction * 0.15f + Vector3.down * 0.05f);
                yield return null;
            }

            float deadline = Time.realtimeSinceStartup + 10;
            while (SceneManager.GetActiveScene().name != "StageSelect" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("StageSelect"));
        }

        private sealed class TestNavigation : ITunnelNavigationBuilder
        {
            /// <summary>
            /// ゴールの検証ではNavMeshの構築を省略する
            /// </summary>
            /// <returns>生成を継続するためtrue</returns>
            public bool BuildNavMesh() => true;
        }

        private sealed class GoalInventoryScope : PlayerLifetimeScope
        {
            public InventoryUseCase Inventory;

            /// <summary>
            /// 開錠テスト用のインベントリのみ登録する
            /// </summary>
            /// <param name="builder">登録先</param>
            protected override void Configure(IContainerBuilder builder) => builder.RegisterInstance(Inventory);
        }

        private sealed class GoalItemCatalog : IItemCatalog
        {
            /// <summary>
            /// 既存の古びた鍵の定義を返す
            /// </summary>
            /// <param name="itemId">アイテムID</param>
            /// <returns>鍵の定義</returns>
            public Task<ItemDefinition> GetItemAsync(string itemId) => Task.FromResult(
                new ItemDefinition(TempEscapeKey.ItemId, "Key", "", "", ItemType.Key, 1));
        }

        private sealed class GoalInventoryRepository : IInventoryRepository
        {
            /// <summary>
            /// テストでは外部へ保存しない
            /// </summary>
            /// <param name="data">保存内容</param>
            /// <returns>完了した処理</returns>
            public Task SaveInventoryAsync(InventorySaveData data) => Task.CompletedTask;

            /// <summary>
            /// 空の保存内容を返す
            /// </summary>
            /// <returns>空の保存内容</returns>
            public Task<InventorySaveData> LoadInventoryAsync() => Task.FromResult(new InventorySaveData());
        }
    }
}
#endif
