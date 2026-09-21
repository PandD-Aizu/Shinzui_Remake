using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Infrastructure.Services;
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
            _player.transform.position = new Vector3(0, 0, -5);
            Physics.SyncTransforms();
            Assert.That(_runtime.CanSee(_player.transform.position, 2f), Is.False);
        }
        [Test] public void WallBlocksBothSightAndContactKill()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0, 1, 2.5f);
            _wall.transform.localScale = new Vector3(5, 4, .2f);
            Physics.SyncTransforms();
            Assert.That(_runtime.CanSee(_player.transform.position, 2f), Is.False);
            Assert.That(_runtime.HasClearContact(_player.transform.position, 2f), Is.False);
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
        [Test] public void UnreachableTargetDoesNotKeepPreviousPath()
        {
            Assert.That(_runtime.MoveTo(new Vector3(0, 0, 8)), Is.True);
            _runtime.Tick(1f);
            Assert.That(_runtime.MoveTo(new Vector3(1000, 0, 1000)), Is.False);
            Assert.That(_agent.hasPath, Is.False);
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
