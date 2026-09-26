using Shinzui.Infrastructure.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shinzui.Editor.SpiderDeity
{
    public static class SpiderWalkAssetBuilder
    {
        public const string ScenePath = "Assets/Shinzui/Scenes/SpiderDeityWalkPreview.unity";
        public const string PrefabPath = "Assets/Shinzui/Prefabs/SpiderDeity/SpiderDeity_Walk.prefab";

        [MenuItem("Tools/Shinzui/Spider Deity/Create or Update Walk Assets")]
        public static void BuildAssets()
        {
            if (!UnityEngine.Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(SpiderDeityIkAssetBuilder.PreviewScenePath);
                var preview = Object.FindFirstObjectByType<SpiderLegIkPreviewMotion>();
                var root = preview.gameObject;
                Object.DestroyImmediate(preview);
                root.AddComponent<SpiderProceduralWalk>();
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
                root.AddComponent<SpiderWalkPreviewMotion>();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("SPIDER_WALK_ASSETS_CREATED");
            }
            finally
            {
                if (!UnityEngine.Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        [MenuItem("Tools/Shinzui/Spider Deity/Open Walk Preview")]
        public static void OpenPreview()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
