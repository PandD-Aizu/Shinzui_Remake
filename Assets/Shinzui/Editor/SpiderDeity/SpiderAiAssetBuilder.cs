using System;
using Shinzui.Infrastructure.Animation;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Shinzui.Editor.SpiderDeity
{
    public static class SpiderAiAssetBuilder
    {
        public const string ScenePath = "Assets/Shinzui/Scenes/SpiderDeityAiPreview.unity";
        public const string PrefabPath = "Assets/Shinzui/Prefabs/SpiderDeity/SpiderDeity_AI.prefab";
        private const string NavPath = "Assets/Shinzui/Prefabs/SpiderDeity/SpiderDeityPreviewNavMesh.asset";

        [MenuItem("Tools/Shinzui/Spider Deity/Create or Update AI Assets")]
        public static void BuildAssets()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring AI assets.");
            // An additive scratch scene leaves existing unsaved scenes untouched.
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var terrain = new GameObject("Navigation Terrain");
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Sloped Ground 7 degrees";
                floor.transform.SetParent(terrain.transform);
                floor.transform.localScale = new Vector3(6f, 0.1f, 6f);
                floor.transform.rotation = Quaternion.Euler(0, 0, 7f);
                floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpiderDeityIkAssetBuilder.Folder + "/PreviewGround.mat");
                var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = "Sight and Navigation Blocker";
                obstacle.transform.SetParent(terrain.transform);
                obstacle.transform.position = new Vector3(0.1f, 0.55f, 0.3f);
                obstacle.transform.localScale = new Vector3(0.65f, 1.05f, 0.65f);
                var surface = terrain.AddComponent<NavMeshSurface>();
                surface.agentTypeID = 0;
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.overrideVoxelSize = true;
                surface.voxelSize = 0.05f;
                Physics.SyncTransforms();
                surface.BuildNavMesh();
                if (!surface.navMeshData) throw new InvalidOperationException("No preview navigation data generated.");
                var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
                if (existing)
                {
                    surface.RemoveData();
                    EditorUtility.CopySerialized(surface.navMeshData, existing);
                    Object.DestroyImmediate(surface.navMeshData);
                    surface.navMeshData = existing;
                    EditorUtility.SetDirty(existing);
                    surface.AddData();
                }
                else AssetDatabase.CreateAsset(surface.navMeshData, NavPath);

                var actor = new GameObject("SpiderDeity_AI");
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SpiderWalkAssetBuilder.PrefabPath), scene);
                visual.transform.SetParent(actor.transform, false);
                visual.AddComponent<SpiderBodyGrounding>();
                var agent = actor.AddComponent<NavMeshAgent>();
                agent.enabled = false;
                agent.agentTypeID = 0;
                agent.radius = 0.5f;
                agent.height = 1.05f;
                agent.speed = 0.08f;
                agent.acceleration = 0.3f;
                agent.angularSpeed = 12f;
                agent.stoppingDistance = 0.025f;
                agent.updateRotation = false;
                agent.updateUpAxis = false;
                agent.autoTraverseOffMeshLink = false;
                var driver = actor.AddComponent<SpiderNavigationDriver>();
                PrefabUtility.SaveAsPrefabAssetAndConnect(actor, PrefabPath, InteractionMode.AutomatedAction);
                Vector3 spawn = new Vector3(-1f, -0.07f, -0.7f);
                if (!NavMesh.SamplePosition(spawn, out var start, 0.4f, NavMesh.AllAreas))
                    throw new InvalidOperationException("Preview spawn is not on NavMesh.");
                actor.transform.position = start.position;
                var pointA = new GameObject("Patrol A").transform;
                pointA.position = new Vector3(-1, -0.07f, 1.1f);
                var pointB = new GameObject("Patrol B").transform;
                pointB.position = spawn;
                driver.SetPatrolPoints(new[] { pointA, pointB });
                var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                target.name = "Player Target - move this to test detection";
                target.transform.position = new Vector3(1.4f, 0.25f, 1.4f);
                target.transform.localScale = new Vector3(0.15f, 0.3f, 0.15f);
                Object.DestroyImmediate(target.GetComponent<Collider>());
                driver.Target = target.transform;
                PrefabUtility.RecordPrefabInstancePropertyModifications(driver);

                var camera = new GameObject("AI Preview Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(3.1f, 2.6f, -4.4f);
                camera.transform.LookAt(new Vector3(-0.5f, 0.35f, 0.1f));
                camera.nearClipPlane = 0.01f;
                camera.fieldOfView = 38f;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var light = new GameObject("AI Preview Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 2f;
                light.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("SPIDER_AI_ASSETS_CREATED");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
            }
        }

        [MenuItem("Tools/Shinzui/Spider Deity/Open AI Preview Additively")]
        public static void OpenPreview() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }
}
