using System;
using System.IO;
using FMODUnity;
using Shinzui.AudioProbe.Infrastructure;
using Shinzui.Infrastructure.SpatialAudio;
using SteamAudio;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shinzui.SpatialAudio.Tests.Editor
{
    [InitializeOnLoad]
    public static class SpatialAudioProbeBuilder
    {
        const string Root = "Assets/Shinzui/Tests/SpatialAudio";
        const string ScenePath = Root + "/SpatialAudioLifecycle.unity";
        const string Running = "SpatialAudio.CliRunning";
        static SpatialAudioProbeBuilder() { EditorApplication.update += Poll; }
        public static void Create()
        {
            Directory.CreateDirectory(Root + "/Data"); AssetDatabase.Refresh();
            SteamAudioSettings.Singleton.realTimeMaxSources = 8;
            EditorUtility.SetDirty(SteamAudioSettings.Singleton);
            EventManager.RefreshBanks();
            var catalog = AssetDatabase.LoadAssetAtPath<SpatialAudioCatalog>(Root + "/Data/ProbeCatalog.asset");
            if (!catalog) { catalog = ScriptableObject.CreateInstance<SpatialAudioCatalog>(); AssetDatabase.CreateAsset(catalog, Root + "/Data/ProbeCatalog.asset"); }
            catalog.Entries = new[] { Entry("pulse", "event:/OrganicReverbProbe"), Entry("loop", "event:/SpatialAudioLoopProbe") };
            EditorUtility.SetDirty(catalog);
            var scene = EditorSceneManager.OpenScene("Assets/Shinzui/Tests/OrganicReverb/OrganicReverbProbe.unity");
            var oldProbe = UnityEngine.Object.FindFirstObjectByType<OrganicReverbProbe>();
            UnityEngine.Object.DestroyImmediate(oldProbe.Emitter.gameObject);
            UnityEngine.Object.DestroyImmediate(oldProbe.gameObject);
            new GameObject("Spatial Audio Lifecycle Tests").AddComponent<SpatialAudioLifecycleProbe>().Catalog = catalog;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[SpatialAudioProbe] Created " + ScenePath);
        }
        static SpatialAudioCatalog.Entry Entry(string id, string path)
        {
            var info = EventManager.EventFromPath(path);
            if (!info) throw new InvalidOperationException("Missing FMOD probe event: " + path);
            return new SpatialAudioCatalog.Entry { Id = id, Event = new EventReference { Guid = info.Guid, Path = info.Path },
                AuthoringNotes = id == "pulse" ? "6-second event includes authored silence for a 3-second reflection tail." : "1-second loop with 500ms release envelope." };
        }
        public static void RunEditor()
        { EditorSceneManager.OpenScene(ScenePath); SessionState.SetBool(Running, true); EditorApplication.EnterPlaymode(); }
        static void Poll()
        {
            if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || !SpatialAudioLifecycleProbe.Completed) return;
            SessionState.SetBool(Running, false);
            int result = SpatialAudioLifecycleProbe.Passed ? 0 : 1;
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(result);
        }
        public static void Build()
        {
            const string path = "Builds/SpatialAudioProbe/SpatialAudioProbe.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            var previous = addressables ? addressables.BuildAddressablesWithPlayerBuild : default;
            BuildReport report;
            try
            {
                if (addressables) addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = path,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            }
            finally { if (addressables) addressables.BuildAddressablesWithPlayerBuild = previous; }
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Spatial audio build failed: " + report.summary.result);
            var licenses = Path.Combine(Path.GetDirectoryName(path), "Licenses"); Directory.CreateDirectory(licenses);
            foreach (var file in new[] { "LICENSE.md", "THIRDPARTY.md" })
                File.Copy("Assets/Plugins/SteamAudio/" + file, Path.Combine(licenses, "SteamAudio-" + file), true);
            Debug.Log("[SpatialAudioProbe] Build succeeded.");
        }
    }
}
