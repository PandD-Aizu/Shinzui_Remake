using Shinzui.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using Shinzui.Infrastructure.Rendering;
using Shinzui.Infrastructure.Lighting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Shinzui.Editor
{
    /// <summary>既存シーンを変更せず敵モーション検証用の独立シーンを作成</summary>
    public static class HumanEnemyMotionTestAssets
    {
        public const string ScenePath = "Assets/Shinzui/Scenes/MotionTest/HumanEnemyMotion.unity";

        /// <summary>床・照明・検証カメラと本番Prefab参照を保存</summary>
        [MenuItem("Shinzui/Enemies/Human Event/Create Motion Test Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
            if (!AssetDatabase.IsValidFolder("Assets/Shinzui/Scenes/MotionTest"))
                AssetDatabase.CreateFolder("Assets/Shinzui/Scenes", "MotionTest");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                // 十分な明るさと広い床で骨格・接地・突進を確認
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.35f, .38f, .42f);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Test Floor";
                floor.layer = 8;
                floor.transform.position = new Vector3(0f, -.25f, 0f);
                floor.transform.localScale = new Vector3(24f, .5f, 24f);
                var light = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional;
                HdrpLightUtility.SetIntensity(light, 1.5f, LightUnit.Lux);
                light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                var camera = new GameObject("Test Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(3f, 1.6f, -4f);
                camera.transform.LookAt(new Vector3(0f, 1f, 2f));
                camera.nearClipPlane = .03f;
                camera.farClipPlane = 80f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .045f, .06f);
                HdrpCameraUtility.SetPostProcessing(camera, false);

                // DIやステージ生成に依存せず本番の導入Viewへプレイヤーを供給
                var playerObject = new GameObject("Test Player", typeof(CharacterController), typeof(PlayerView));
                var controller = playerObject.GetComponent<CharacterController>();
                controller.height = 1.8f;
                controller.radius = .3f;
                controller.center = Vector3.up * .9f;
                var player = playerObject.GetComponent<PlayerView>();
                var playerSettings = new SerializedObject(player);
                playerSettings.FindProperty("characterController").objectReferenceValue = controller;
                playerSettings.FindProperty("mainCamera").objectReferenceValue = camera;
                playerSettings.FindProperty("cameraTarget").objectReferenceValue = camera.transform;
                playerSettings.FindProperty("groundLayers").intValue = 1 << 8;
                playerSettings.ApplyModifiedPropertiesWithoutUndo();

                var test = new GameObject("Motion Test Controls", typeof(HumanEnemyMotionTest)).GetComponent<HumanEnemyMotionTest>();
                var settings = new SerializedObject(test);
                settings.FindProperty("player").objectReferenceValue = player;
                settings.FindProperty("introductionPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(HumanEnemyEventAssets.IntroPrefab).GetComponent<StageIntroView>();
                var prefabs = settings.FindProperty("enemyPrefabs");
                prefabs.arraySize = 2;
                prefabs.GetArrayElementAtIndex(0).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(HumanEnemyEventAssets.EnemyPrefab).GetComponent<EnemyView>();
                prefabs.GetArrayElementAtIndex(1).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/Prefabs/BlackHoleEnemy.prefab").GetComponent<EnemyView>();
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
