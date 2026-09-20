#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.Interfaces.Tunnel;
using Shinzui.DI.GenerateTunnel;
using Shinzui.View;
using Shinzui.View.GenerateTunnel;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace Shinzui.Tests.TunnelGeneration
{
    public sealed class StageTunnelGenerationTests
    {
        private const string PrefabPath = "Assets/Shinzui/Prefabs/StageTunnelGenerator.prefab";
        private readonly List<Scene> _scenes = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Scene scene in _scenes)
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            _scenes.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StageStartsOnceAndPreservesGameplayCamera()
        {
            Scene scene = CreateScene("Stage");
            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            var camera = new GameObject("Gameplay Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(new Vector3(0, 2, 0), Quaternion.Euler(0, 37, 0));
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            SceneManager.SetActiveScene(previous);

            var scope = CreateStage(scene);
            yield return null;
            yield return null;

            Assert.That(scope.Container, Is.Not.Null);
            Assert.That(scope.Container.Resolve<IGenerateTunnelUseCase>().HasGenerated, Is.True);
            var view = scope.GetComponent<TunnelMapView>();
            Transform geometry = view.GeometryRoot;
            Assert.That(geometry, Is.Not.Null);
            Assert.That(geometry.childCount, Is.GreaterThan(8));
            Assert.That(view.MapRootObject.scene, Is.EqualTo(scene));
            Assert.That(view.MapRootObject.activeInHierarchy, Is.True);
            var surface = view.MapRootObject.GetComponent<NavMeshSurface>();
            Assert.That(surface.navMeshData, Is.Not.Null);
            var data = surface.navMeshData;

            scope.Container.Resolve<TunnelGenerationEntryPoint>().Start();
            yield return null;
            Assert.That(view.GeometryRoot, Is.SameAs(geometry));
            Assert.That(surface.navMeshData, Is.SameAs(data));
            Assert.That(view.MapRootObject.GetComponentsInChildren<Transform>().Count(t => t.name == "Tunnel Map Geometry"), Is.EqualTo(1));
            Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
        }

        [UnityTest]
        public IEnumerator ExplicitNextFloorReplacesGeometryAndKeepsStartupIdempotent()
        {
            Scene scene = CreateScene("Next Floor Stage");
            var scope = CreateStage(scene);
            yield return null;
            yield return null;
            var view = scope.GetComponent<TunnelMapView>();
            var previousGeometry = view.GeometryRoot;
            var surface = view.MapRootObject.GetComponent<NavMeshSurface>();
            var previousNavigation = surface.navMeshData;

            Assert.That(scope.Regenerate(9182), Is.True);
            Assert.That(previousGeometry.gameObject.activeSelf, Is.False);
            Assert.That(view.GeometryRoot, Is.Not.SameAs(previousGeometry));
            Assert.That(surface.navMeshData, Is.Not.SameAs(previousNavigation));
            var useCase = scope.Container.Resolve<IGenerateTunnelUseCase>();
            Assert.That(useCase.GenerateOnce(new TunnelGenerationRequestDto(), out var map), Is.False);
            Assert.That(map.Seed, Is.EqualTo(9182));
            var currentGeometry = view.GeometryRoot;
            scope.Container.Resolve<TunnelGenerationEntryPoint>().Start();
            yield return null;
            Assert.That(view.GeometryRoot, Is.SameAs(currentGeometry));
            Assert.That(previousGeometry == null, Is.True);
            Assert.That(previousNavigation == null, Is.True);
            Assert.That(view.MapRootObject.GetComponentsInChildren<Transform>()
                .Count(t => t.name == "Tunnel Map Geometry"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WarpInputIsBoundOnceAndUnboundWithTheContainer()
        {
            Scene scene = CreateScene("Warp Stage");
            var scope = CreateStage(scene);
            yield return null;
            yield return null;
#if STEAMAUDIO_ENABLED
            // This synthetic player tests warp subscriptions, not external FMOD event banks.
            var audio = scope.GetComponent("TunnelAudioBinding") as Behaviour;
            if (audio != null) audio.enabled = false;
#endif
            var trigger = scope.GetComponent<TunnelMapView>().GeometryRoot
                .GetComponentInChildren<GeneratedWarpCorridorTrigger>();
            Assert.That(trigger, Is.Not.Null);
            var playerObject = new GameObject("Warp Test Player", typeof(BoxCollider), typeof(PlayerView));
            SceneManager.MoveGameObjectToScene(playerObject, scene);
            var player = playerObject.GetComponent<PlayerView>();
            int warpCount = 0;
            player.Warped += _ => warpCount++;

            trigger.SendMessage("OnTriggerEnter", playerObject.GetComponent<BoxCollider>());
            Assert.That(warpCount, Is.EqualTo(1));
            trigger.SendMessage("OnTriggerEnter", playerObject.GetComponent<BoxCollider>());
            Assert.That(warpCount, Is.EqualTo(1), "Repeated collider contacts must respect the cooldown.");

            scope.DisposeCore();
            yield return new WaitForSeconds(0.4f);
            trigger.SendMessage("OnTriggerEnter", playerObject.GetComponent<BoxCollider>());
            Assert.That(warpCount, Is.EqualTo(1), "Disposing the stage container must remove input subscriptions.");
        }

        [UnityTest]
        public IEnumerator AdditiveStagesOwnTheirMapsAndReloadWithFreshContainers()
        {
            Scene first = CreateScene("Stage A");
            Scene second = CreateScene("Stage B");
            var firstScope = CreateStage(first);
            var secondScope = CreateStage(second);
            secondScope.transform.position = new Vector3(1000, 0, 0);
            yield return null;
            yield return null;

            var firstView = firstScope.GetComponent<TunnelMapView>();
            var secondView = secondScope.GetComponent<TunnelMapView>();
            var firstRoot = firstView.MapRootObject;
            var secondRoot = secondView.MapRootObject;
            var firstData = firstRoot.GetComponent<NavMeshSurface>().navMeshData;
            var secondData = secondRoot.GetComponent<NavMeshSurface>().navMeshData;
            var firstUseCase = firstScope.Container.Resolve<IGenerateTunnelUseCase>();
            Assert.That(firstRoot.scene, Is.EqualTo(first));
            Assert.That(secondRoot.scene, Is.EqualTo(second));
            Assert.That(firstRoot, Is.Not.SameAs(secondRoot));
            Assert.That(secondScope.Container.Resolve<IGenerateTunnelUseCase>(), Is.Not.SameAs(firstUseCase));
            Assert.That(firstData, Is.Not.Null);
            Assert.That(secondData, Is.Not.Null);

            yield return SceneManager.UnloadSceneAsync(first);
            yield return null;
            Assert.That(firstRoot == null, Is.True);
            Assert.That(firstData == null, Is.True, "Runtime NavMesh must be released when its stage unloads.");
            Assert.That(secondRoot != null && secondData != null, Is.True);

            Scene reloaded = CreateScene("Stage A");
            var reloadedScope = CreateStage(reloaded);
            yield return null;
            yield return null;
            var reloadedUseCase = reloadedScope.Container.Resolve<IGenerateTunnelUseCase>();
            Assert.That(reloadedUseCase, Is.Not.SameAs(firstUseCase));
            Assert.That(reloadedUseCase.HasGenerated, Is.True);
            Assert.That(reloadedScope.GetComponent<TunnelMapView>().GeometryRoot, Is.Not.Null);
        }

        private Scene CreateScene(string name)
        {
            var scene = SceneManager.CreateScene(name);
            _scenes.Add(scene);
            return scene;
        }

        private static GenerateTunnelLifetimeScope CreateStage(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try { return Object.Instantiate(prefab).GetComponent<GenerateTunnelLifetimeScope>(); }
            finally { SceneManager.SetActiveScene(previous); }
        }
    }
}
#endif
