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

namespace Shinzui.Propagation.Tests.Editor
{
    [InitializeOnLoad]
    public static class PropagationBuilder
    {
        const string Root="Assets/Shinzui/Tests/Propagation";
        const string ScenePath=Root+"/PropagationProbe.unity";
        const string Running="Propagation.CliRunning";
        static PropagationBuilder(){EditorApplication.update+=Poll;}
        public static void Create()
        {
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();EventManager.RefreshBanks();
            SteamAudioSettings.Singleton.maxOcclusionSamples=32;
            SteamAudioSettings.Singleton.realTimeBounces=64;
            SteamAudioSettings.Singleton.realTimeMaxSources=16;
            EditorUtility.SetDirty(SteamAudioSettings.Singleton);
            var config=AssetDatabase.LoadAssetAtPath<TunnelAudioConfiguration>("Assets/Shinzui/Audio/Resources/SpatialAudio/TunnelAudioConfiguration.asset");
            if(!config)throw new InvalidOperationException("Phase-3 acoustic configuration is missing.");
            config.Propagation=new SpatialPropagationSettings();EditorUtility.SetDirty(config);
            var catalog=AssetDatabase.LoadAssetAtPath<SpatialAudioCatalog>(Root+"/PropagationEvents.asset");
            if(!catalog){catalog=ScriptableObject.CreateInstance<SpatialAudioCatalog>();AssetDatabase.CreateAsset(catalog,Root+"/PropagationEvents.asset");}
            var tone=EventManager.EventFromPath("event:/PropagationTone");var pulse=EventManager.EventFromPath("event:/OrganicReverbProbe");
            if(!tone||!pulse)throw new InvalidOperationException("Build the PropagationProbe and OrganicReverbProbe banks first.");
            catalog.Entries=new[]{
                new SpatialAudioCatalog.Entry {Id="tone",Event=new EventReference {Guid=tone.Guid,Path=tone.Path}},
                new SpatialAudioCatalog.Entry {Id="pulse",Event=new EventReference {Guid=pulse.Guid,Path=pulse.Path}}};
            EditorUtility.SetDirty(catalog);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var listener=new GameObject("Propagation listener");listener.tag="MainCamera";
            listener.AddComponent<Camera>();listener.AddComponent<StudioListener>();listener.AddComponent<SteamAudioListener>();
            var probe=new GameObject("Propagation comparisons").AddComponent<PropagationProbe>();probe.Catalog=catalog;probe.Listener=listener.transform;
            new GameObject("Light").AddComponent<Light>().type=LightType.Directional;
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Debug.Log("[Propagation] Scene and production tuning saved.");
        }
        public static void RunEditor(){EditorSceneManager.OpenScene(ScenePath);SessionState.SetBool(Running,true);EditorApplication.EnterPlaymode();}
        static void Poll()
        {
            if(!SessionState.GetBool(Running,false)||!EditorApplication.isPlaying||!PropagationProbe.Completed)return;
            SessionState.SetBool(Running,false);int code=PropagationProbe.Passed?0:1;EditorApplication.ExitPlaymode();EditorApplication.delayCall+=()=>EditorApplication.Exit(code);
        }
        public static void Build()
        {
            const string path="Builds/PropagationProbe/PropagationProbe.exe";Directory.CreateDirectory(Path.GetDirectoryName(path));
            var settings=AddressableAssetSettingsDefaultObject.Settings;var previous=settings?settings.BuildAddressablesWithPlayerBuild:default;
            BuildReport report;
            try
            {
                if(settings)settings.BuildAddressablesWithPlayerBuild=AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            }
            finally{if(settings)settings.BuildAddressablesWithPlayerBuild=previous;}
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Propagation build failed: "+report.summary.result);
            var licenses=Path.Combine(Path.GetDirectoryName(path),"Licenses");Directory.CreateDirectory(licenses);
            foreach(var name in new[]{"LICENSE.md","THIRDPARTY.md"})File.Copy("Assets/Plugins/SteamAudio/"+name,Path.Combine(licenses,"SteamAudio-"+name),true);
            Debug.Log("[Propagation] Build succeeded.");
        }
    }
}
