using System;
using System.IO;
using Shinzui.CustomSpatialAudio.Probe;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shinzui.CustomSpatialAudio.Probe.Editor
{
    public static class CustomSpatialAudioProbeBuilder
    {
        const string Root = "Assets/Shinzui/Tests/CustomSpatialAudio";
        const string ScenePath = Root + "/CustomSpatialAudioProbe.unity";
        const string OutputRoot = "Artifacts/CustomSpatialAudio/Improved";

        [MenuItem("Shinzui/Audio/Custom Spatial Audio/Validate and record")]
        public static void Validate() => CustomSpatialAudioProbe.Run(OutputRoot + "/Editor", false);

        [MenuItem("Shinzui/Audio/Custom Spatial Audio/Audition and record (3 seconds)")]
        public static void Audition() => CustomSpatialAudioProbe.Run(OutputRoot + "/Editor", true);

        // One batch-mode entrypoint for the native callback and IL2CPP integration checks.
        public static void ValidateAndBuild()
        {
            Validate();
            Audition();
            Build();
        }

        [MenuItem("Shinzui/Audio/Custom Spatial Audio/Create probe scene")]
        public static void CreateScene()
        {
            // A fresh batch Editor has an untitled scene; Unity refuses additive NewScene
            // beside it. Only that noninteractive, clean scene may be replaced.
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool replaceBatchScene = UnityEngine.Application.isBatchMode && !active.isDirty;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                replaceBatchScene ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                var root = new GameObject("Custom spatial audio experiment");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<CustomSpatialAudioProbeEntryPoint>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally { if (!replaceBatchScene) EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Shinzui/Audio/Custom Spatial Audio/Build Windows IL2CPP probe")]
        public static void Build()
        {
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.IL2CPP)
                throw new InvalidOperationException("The probe requires the project's Windows IL2CPP backend.");
            CreateScene();
            const string target = "Builds/CustomSpatialAudioProbe/CustomSpatialAudioProbe.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var previous = settings ? settings.BuildAddressablesWithPlayerBuild : default;
            try
            {
                if (settings) settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = target,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development,
                    extraScriptingDefines = new[] { "SHINZUI_CUSTOM_AUDIO_PROBE" }
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Custom audio IL2CPP build failed: " + report.summary.result);
                Debug.Log("[CustomSpatialAudio] IL2CPP build succeeded: " + Path.GetFullPath(target));
            }
            finally { if (settings) settings.BuildAddressablesWithPlayerBuild = previous; }
        }
    }
}
