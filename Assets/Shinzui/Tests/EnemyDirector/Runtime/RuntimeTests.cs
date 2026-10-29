using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Infrastructure.Services;
using Shinzui.Infrastructure.Enemy;
using Shinzui.View;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Shinzui.Tests.EnemyDirector
{
    public sealed class RuntimeTests
    {
        private GameObject _enemy;
        private GameObject _player;
        private GameObject _wall;
        private NavMeshData _data;
        private NavMeshDataInstance _instance;
        private NavMeshAgent _agent;
        private UnityEnemyRuntime _runtime;
        private bool _enabled;

        [UnitySetUp] public IEnumerator SetUp()
        {
            var settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource> { new()
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(new Vector3(0, -.5f, 0), Quaternion.identity, Vector3.one),
                size = new Vector3(40, 1, 40), area = 0
            } };
            _data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Vector3.zero, Vector3.one * 50), Vector3.zero, Quaternion.identity);
            _instance = NavMesh.AddNavMeshData(_data);
            _enemy = new GameObject("DirectorTestEnemy");
            _agent = _enemy.AddComponent<NavMeshAgent>();
            _agent.agentTypeID = settings.agentTypeID;
            _agent.Warp(Vector3.zero);
            _player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _player.name = "DirectorTestPlayer";
            _player.transform.position = new Vector3(0, 0, 5);
            _enabled = true;
            _runtime = new UnityEnemyRuntime(_agent, _enemy.transform, _player.transform, () => _enabled, new EnemyDirectorSettings());
            Physics.SyncTransforms();
            yield return null;
            Assert.That(_runtime.IsReady, Is.True, "Test agent must be on its isolated NavMesh");
        }
        [TearDown] public void TearDown()
        {
            if (_wall != null) Object.DestroyImmediate(_wall);
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_enemy);
            _instance.Remove();
            Object.DestroyImmediate(_data);
        }
        [Test] public void DetectsFrontButNotBehind()
        {
            Assert.That(_runtime.CanSee(_player.transform.position, 2f), Is.True);
            var sight = _runtime.SampleSight(_player.transform.position, 2f, Vector3.forward);
            Assert.That(sight.VisibleRatio, Is.EqualTo(1f));
            Assert.That(sight.SeenPosition, Is.EqualTo(_player.transform.position));
            Assert.That(sight.SeenVelocity, Is.EqualTo(Vector3.forward));
            _player.transform.position = new Vector3(0, 0, -5);
            Physics.SyncTransforms();
            Assert.That(_runtime.CanSee(_player.transform.position, 2f), Is.False);
            Assert.That(_runtime.SampleSight(_player.transform.position, 2f, Vector3.forward).SeenPosition,
                Is.EqualTo(Vector3.zero));
        }
        [Test] public void WallBlocksBothSightAndContactKill()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0, 1, 2.5f);
            _wall.transform.localScale = new Vector3(5, 4, .2f);
            Physics.SyncTransforms();
            Assert.That(_runtime.CanSee(_player.transform.position, 2f), Is.False);
            Assert.That(_runtime.HasClearContact(_player.transform.position, 2f), Is.False);
            Assert.That(_runtime.HasLineTo(_player.transform.position), Is.False);
            var sight = _runtime.SampleSight(_player.transform.position, 2f, Vector3.one);
            Assert.That(sight.VisibleRatio, Is.Zero);
            Assert.That(sight.SeenVelocity, Is.EqualTo(Vector3.zero));
        }
        [Test] public void TriggerDoesNotOccludeSight()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0, 1, 2.5f);
            _wall.GetComponent<Collider>().isTrigger = true;
            Physics.SyncTransforms();
            Assert.That(_runtime.CanSee(_player.transform.position, 2f), Is.True);
        }
        [Test] public void DisabledAgentAndViewAreNotAvailable()
        {
            _enabled = false;
            Assert.That(_runtime.IsReady, Is.False);
            Assert.That(_runtime.CanSee(_player.transform.position, 2f), Is.False);
            _enabled = true;
            _agent.enabled = false;
            _runtime.SetSpeed(.5f, true);
            _runtime.Stop();
            Assert.That(_runtime.MoveTo(Vector3.forward), Is.False);
        }
        /// <summary>見回しは位置を変えず水平方向だけを更新</summary>
        [Test] public void FaceTowardsRotatesWithoutMoving()
        {
            Vector3 original = _runtime.Position;
            _runtime.Stop();
            _runtime.SetSpeed(1f, true);
            _runtime.FaceTowards(original + new Vector3(5f, 3f, 0f));
            Assert.That(_runtime.Position, Is.EqualTo(original));
            Assert.That(Vector3.Angle(_enemy.transform.forward, Vector3.right), Is.LessThan(.1f));
            Assert.That(_agent.isStopped, Is.True);
        }

        [Test] public void UnreachableTargetDoesNotKeepPreviousPath()
        {
            Assert.That(_runtime.MoveTo(new Vector3(0, 0, 8)), Is.True);
            _runtime.Tick(1f);
            Assert.That(_runtime.MoveTo(new Vector3(1000, 0, 1000)), Is.False);
            Assert.That(_agent.hasPath, Is.False);
        }
        /// <summary>舞台裏ではAgentと表示、接触を無効化し、配置成功後だけ元の状態へ戻る</summary>
        [Test] public void BackstageDisablesAndRestoresComponents()
        {
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(_enemy.transform, false);
            var renderer = mesh.GetComponent<Renderer>();
            var collider = mesh.GetComponent<Collider>();
            var animator = mesh.AddComponent<Animator>();
            animator.enabled = false;
            var view = _enemy.AddComponent<EnemyView>();
            view.PrepareBackstage();
            var cameraObject = new GameObject("BackstageTestCamera");
            cameraObject.transform.SetParent(_player.transform, false);
            cameraObject.transform.position = new Vector3(0f, 1f, -5f);
            var camera = cameraObject.AddComponent<Camera>();
            Assert.That(view.IsVisibleAt(Vector3.zero, camera, _player.transform, -1), Is.True);
            Assert.That(view.IsVisibleAt(Vector3.back * 10f, camera, _player.transform, -1), Is.False);
            Assert.That(view.IsVisibleAt(Vector3.zero, null, _player.transform, -1), Is.True);
            _runtime.SetBackstage(true);
            view.SetBackstage(true);
            Assert.That(_agent.enabled, Is.False);
            Assert.That(renderer.enabled || collider.enabled || animator.enabled, Is.False);
            view.enabled = false;
            Assert.That(renderer.enabled || collider.enabled, Is.False);
            view.enabled = true;
            Assert.That(_runtime.HasClearContact(_player.transform.position, 2f), Is.False);
            Assert.That(_runtime.WarpTo(Vector3.one * 1000f), Is.False);
            Assert.That(_agent.enabled, Is.False);
            Assert.That(NavMesh.SamplePosition(Vector3.forward * 10f, out NavMeshHit emergePoint, 1f, NavMesh.AllAreas), Is.True);
            Assert.That(_runtime.WarpTo(emergePoint.position), Is.True);
            Assert.That(_agent.enabled, Is.False);
            _runtime.SetBackstage(false);
            view.SetBackstage(false);
            Assert.That(_runtime.IsReady, Is.True);
            Assert.That(renderer.enabled && collider.enabled, Is.True);
            Assert.That(animator.enabled, Is.False);
            Assert.That(_runtime.PathDistanceTo(Vector3.zero), Is.EqualTo(10f).Within(.2f));
        }

        /// <summary>マップDTOがないシーンでも可視点と未接続点を出現候補から除外</summary>
        [Test] public void EmergeProviderRejectsVisibleOrUnreachablePoints()
        {
            var provider = new NavMeshSearchPointProvider(agentTypeId: _agent.agentTypeID);
            Assert.That(provider.TryFindEmergePoint(Vector3.zero, 5f, 15f, false, _ => true, out _), Is.False);
            Assert.That(provider.TryFindEmergePoint(Vector3.zero, 5f, 15f, false, _ => false, out Vector3 point), Is.True);
            Assert.That(Vector3.Distance(Vector3.zero, point), Is.InRange(5f, 15f));
            Assert.That(_runtime.PathDistanceTo(point), Is.LessThan(float.MaxValue));
            Assert.That(provider.TryFindEmergePoint(Vector3.one * 1000f, 5f, 15f, false, _ => false, out _), Is.False);

            // マップの小部屋候補にはMapRootの変換を反映する
            var map = new TunnelMapDto { SmallRooms = new[] { new SmallRoomDto { CenterX = 5f, CenterZ = 5f } } };
            var generated = new NavMeshSearchPointProvider(() => map,
                () => Matrix4x4.Translate(Vector3.right * 10f), _agent.agentTypeID);
            var candidates = new Vector3[16];
            int count = generated.GetSearchPoints(Vector3.zero, 20f, true, candidates);
            bool hasRoom = false;
            for (int i = 0; i < count; i++) hasRoom |= Vector3.Distance(candidates[i], new Vector3(15f, 0f, 5f)) < .2f;
            Assert.That(hasRoom, Is.True);
        }

        [UnityTest] public IEnumerator RuntimeRecoversAfterNavMeshBecomesAvailable()
        {
            _agent.enabled = false;
            _instance.Remove();
            _enemy.transform.position = new Vector3(100, 0, 100);
            _agent.enabled = true;
            yield return null;
            Assert.That(_runtime.IsReady, Is.False);
            _runtime.Tick(1f);
            _instance = NavMesh.AddNavMeshData(_data, _enemy.transform.position, Quaternion.identity);
            _runtime.Tick(1.1f);
            yield return null;
            Assert.That(_runtime.IsReady, Is.True);
        }
    }
}
