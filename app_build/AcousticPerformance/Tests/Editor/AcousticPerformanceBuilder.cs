using System;
using System.IO;
using FMODUnity;
using Shinzui.Infrastructure.SpatialAudio;
using Shinzui.Infrastructure.TunnelAcoustics;
using SteamAudio;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shinzui.AcousticPerformance.Tests.Editor
{
    [InitializeOnLoad]
    public static class AcousticPerformanceBuilder
    {
        const string Root="Assets/Shinzui/Tests/AcousticPerformance";
        const string ScenePath=Root+"/AcousticPerformanceProbe.unity";
        const string Running="AcousticPerformance.CliRunning";
        static AcousticPerformanceBuilder(){EditorApplication.update+=Poll;}
        [MenuItem("Tools/Shinzui/Audio Quality/Standard (12 voices)")]
        public static void ApplyStandard()=>ApplyQuality(false);
        [MenuItem("Tools/Shinzui/Audio Quality/Economy (8 voices)")]
        public static void ApplyEconomy()=>ApplyQuality(true);
        static void ApplyQuality(bool economy)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Audio quality changes require a stopped runtime; native buffers are allocated at startup.");
            var settings=SteamAudioSettings.Singleton;
            settings.realTimeRays=economy?4096:8192;settings.realTimeBounces=economy?32:64;
            settings.realTimeDuration=economy?1.5f:3f;settings.simulationUpdateInterval=economy?.15f:.1f;
            settings.realTimeMaxSources=16;settings.maxOcclusionSamples=32;
            EditorUtility.SetDirty(settings);
            var config=AssetDatabase.LoadAssetAtPath<TunnelAudioConfiguration>("Assets/Shinzui/Audio/Resources/SpatialAudio/TunnelAudioConfiguration.asset");
            config.MaxVoices=economy?8:12;EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
            Debug.Log("[AcousticPerformance] Saved "+(economy?"Economy":"Standard")+" audio quality for the next launch.");
        }
        public static void Create()
        {
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();EventManager.RefreshBanks();
            var config=AssetDatabase.LoadAssetAtPath<TunnelAudioConfiguration>("Assets/Shinzui/Audio/Resources/SpatialAudio/TunnelAudioConfiguration.asset");
            var catalog=AssetDatabase.LoadAssetAtPath<SpatialAudioCatalog>("Assets/Shinzui/Tests/Propagation/PropagationEvents.asset");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var listener=new GameObject("AcousticPerformance listener");listener.tag="MainCamera";
            listener.AddComponent<Camera>();listener.AddComponent<StudioListener>();listener.AddComponent<SteamAudioListener>();
            var probe=new GameObject("AcousticPerformance comparisons").AddComponent<AcousticPerformanceProbe>();probe.Catalog=catalog;probe.Listener=listener.transform;probe.Configuration=config;
            probe.TunnelTemplate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/Prefabs/TunnelBaseModel.prefab");
            probe.CorridorTemplate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/3DModels/Tunnel_Path_Long.fbx");
            new GameObject("Light").AddComponent<Light>().type=LightType.Directional;
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Debug.Log("[AcousticPerformance] Scene and production tuning saved.");
        }
        public static void RunEditor(){EditorSceneManager.OpenScene(ScenePath);SessionState.SetBool(Running,true);EditorApplication.EnterPlaymode();}
        static void Poll()
        {
            if(!SessionState.GetBool(Running,false)||!EditorApplication.isPlaying||!AcousticPerformanceProbe.Completed)return;
            SessionState.SetBool(Running,false);int code=AcousticPerformanceProbe.Passed?0:1;EditorApplication.ExitPlaymode();EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
        }
        public static void Build()
        {
            const string path="Builds/AcousticPerformanceProbe/AcousticPerformanceProbe.exe";Directory.CreateDirectory(Path.GetDirectoryName(path));
            var settings=AddressableAssetSettingsDefaultObject.Settings;var previous=settings?settings.BuildAddressablesWithPlayerBuild:default;
            BuildReport report;
            try
            {
                if(settings)settings.BuildAddressablesWithPlayerBuild=AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            }
            finally{if(settings)settings.BuildAddressablesWithPlayerBuild=previous;}
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("AcousticPerformance build failed: "+report.summary.result);
            var licenses=Path.Combine(Path.GetDirectoryName(path),"Licenses");Directory.CreateDirectory(licenses);
            foreach(var name in new[]{"LICENSE.md","THIRDPARTY.md"})File.Copy("Assets/Plugins/SteamAudio/"+name,Path.Combine(licenses,"SteamAudio-"+name),true);
            Debug.Log("[AcousticPerformance] Build succeeded.");
        }
    }
}
