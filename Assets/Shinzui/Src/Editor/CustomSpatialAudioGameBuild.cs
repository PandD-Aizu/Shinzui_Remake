using System;
using System.IO;
using Shinzui.CustomSpatialAudio.GameProbe;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shinzui.Editor
{
    public static class CustomSpatialAudioGameBuild
    {
        [MenuItem("Shinzui/Audio/Custom Spatial Audio/Build integrated Windows game")]
        public static void Build()
        {
            CustomSpatialAudioAssets.EnsureConfiguration();
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.IL2CPP)
                throw new InvalidOperationException("The integration build requires Windows IL2CPP.");
            const string bootPath = "Assets/Shinzui/Tests/CustomSpatialAudio/GamePlayer/CustomAudioGameBoot.unity";
            const string target = "Builds/CustomSpatialAudioGame/Shinzui.exe";
            var active = SceneManager.GetActiveScene();
            var boot = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var go = new GameObject("Integrated game launcher"); SceneManager.MoveGameObjectToScene(go, boot);
            go.AddComponent<CustomAudioGamePlayerProbe>(); EditorSceneManager.SaveScene(boot, bootPath);
            EditorSceneManager.CloseScene(boot, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            AssetDatabase.SaveAssets(); Directory.CreateDirectory(Path.GetDirectoryName(target));
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            var previous = addressables ? addressables.BuildAddressablesWithPlayerBuild : default;
            try
            {
                if (addressables) addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { bootPath, "Assets/Shinzui/Scenes/LatestStageGenerateTemp.unity" },
                    target = BuildTarget.StandaloneWindows64, locationPathName = target, options = BuildOptions.Development });
                if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Integrated game build failed: " + result.summary.result);
            }
            finally { if (addressables) addressables.BuildAddressablesWithPlayerBuild = previous; }
            Debug.Log("[CustomSpatialAudio] Integrated Windows IL2CPP game built: " + Path.GetFullPath(target));
        }
    }
}
