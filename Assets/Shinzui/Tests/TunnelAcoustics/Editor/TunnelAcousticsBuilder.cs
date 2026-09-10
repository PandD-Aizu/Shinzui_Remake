using System;
using System.Collections.Generic;
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
using Vector3 = UnityEngine.Vector3;

namespace Shinzui.TunnelAcoustics.Tests.Editor
{
    [InitializeOnLoad]
    public static class TunnelAcousticsBuilder
    {
        const string DataRoot="Assets/Shinzui/Audio/Resources/SpatialAudio";
        const string TestRoot="Assets/Shinzui/Tests/TunnelAcoustics";
        const string ScenePath=TestRoot+"/TunnelAcousticsProbe.unity";
        const string Running="TunnelAcoustics.CliRunning";
        static TunnelAcousticsBuilder(){EditorApplication.update+=Poll;}
        public static void Create()
        {
            Directory.CreateDirectory(DataRoot);Directory.CreateDirectory(TestRoot);AssetDatabase.Refresh();
            SteamAudioSettings.Singleton.realTimeMaxSources=16;EditorUtility.SetDirty(SteamAudioSettings.Singleton);
            EventManager.RefreshBanks();
            var library=Asset<AcousticMeshLibrary>("AcousticMeshLibrary");
            var exported=new List<AcousticMeshLibrary.Entry>();
            foreach(var path in new[]{"Assets/Shinzui/3DModels/tunnel_base_model.fbx","Assets/Shinzui/3DModels/tunnelBase_tmp.fbx","Assets/Shinzui/3DModels/Tunnel_Path_Long.fbx"})
                foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                    if(obj is Mesh mesh) exported.Add(ExportMesh(mesh));
            library.Entries=exported.ToArray();EditorUtility.SetDirty(library);
            var events=Asset<SpatialAudioCatalog>("GeneratedTunnelEvents");
            var info=EventManager.EventFromPath("event:/FootstepPrototypeSpatial");
            if(!info)throw new InvalidOperationException("Build the GeneratedTunnelAudio FMOD bank first.");
            events.Entries=new[]{new SpatialAudioCatalog.Entry { Id="footstep.prototype",Event=new EventReference {Guid=info.Guid,Path=info.Path},AuthoringNotes="Synthetic placeholder for footstep timing; replace with final sound-design asset. No leading silence." }};
            EditorUtility.SetDirty(events);
            var config=Asset<TunnelAudioConfiguration>("TunnelAudioConfiguration");
            config.MeshLibrary=library;config.Events=events;config.MaxVoices=12;EditorUtility.SetDirty(config);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var listener=new GameObject("Acoustic Test Listener");listener.tag="MainCamera";
            listener.AddComponent<Camera>();listener.AddComponent<StudioListener>();listener.AddComponent<SteamAudioListener>();
            var probe=new GameObject("Tunnel Acoustics Tests").AddComponent<TunnelAcousticsProbe>();
            probe.Configuration=config;probe.Listener=listener.transform;
            probe.TunnelTemplate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/Prefabs/TunnelBaseModel.prefab");
            probe.CorridorTemplate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shinzui/3DModels/Tunnel_Path_Long.fbx");
            new GameObject("Test Light").AddComponent<Light>().type=LightType.Directional;
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            File.WriteAllText("Logs/TunnelAcoustics/mesh-export.json",JsonUtility.ToJson(new ExportReport {meshes=exported.Count,originalVertices=Sum(exported,true),acousticVertices=Sum(exported,false)},true));
            Debug.Log("[TunnelAcoustics] Exported "+exported.Count+" meshes and created test scene.");
        }
        [Serializable]sealed class ExportReport{public int meshes,originalVertices,acousticVertices;}
        static int Sum(List<AcousticMeshLibrary.Entry> entries,bool original){int n=0;foreach(var e in entries)n+=original?e.OriginalVertexCount:e.Vertices.Length;return n;}
        static T Asset<T>(string name) where T:ScriptableObject
        {var p=DataRoot+"/"+name+".asset";var a=AssetDatabase.LoadAssetAtPath<T>(p);if(!a){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,p);}return a;}
        static AcousticMeshLibrary.Entry ExportMesh(Mesh mesh)
        {
            var input=mesh.vertices;var triangles=mesh.triangles;
            var points=new List<Vector3>();var map=new int[input.Length];var welded=new Dictionary<(int,int,int),int>();
            for(int i=0;i<input.Length;i++)
            {
                var p=input[i];var key=((int)Math.Round(p.x*100000),(int)Math.Round(p.y*100000),(int)Math.Round(p.z*100000));
                if(!welded.TryGetValue(key,out int index)){index=points.Count;points.Add(p);welded.Add(key,index);}map[i]=index;
            }
            var indices=new List<int>();
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=map[triangles[i]],b=map[triangles[i+1]],c=map[triangles[i+2]];
                if(a==b||b==c||c==a||Vector3.Cross(points[b]-points[a],points[c]-points[a]).sqrMagnitude<1e-12f)continue;
                indices.Add(a);indices.Add(b);indices.Add(c);
            }
            return new AcousticMeshLibrary.Entry {Mesh=mesh,Vertices=points.ToArray(),Triangles=indices.ToArray(),OriginalVertexCount=input.Length};
        }
        public static void RunEditor(){EditorSceneManager.OpenScene(ScenePath);SessionState.SetBool(Running,true);EditorApplication.EnterPlaymode();}
        static void Poll()
        {
            if(!SessionState.GetBool(Running,false)||!EditorApplication.isPlaying||!TunnelAcousticsProbe.Completed)return;
            SessionState.SetBool(Running,false);int result=TunnelAcousticsProbe.Passed?0:1;
            EditorApplication.ExitPlaymode();EditorApplication.delayCall+=()=>EditorApplication.Exit(result);
        }
        public static void Build()
        {
            const string path="Builds/TunnelAcousticsProbe/TunnelAcousticsProbe.exe";Directory.CreateDirectory(Path.GetDirectoryName(path));
            var addressables=AddressableAssetSettingsDefaultObject.Settings;var previous=addressables?addressables.BuildAddressablesWithPlayerBuild:default;
            BuildReport report;
            try
            {
                if(addressables)addressables.BuildAddressablesWithPlayerBuild=AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            }
            finally{if(addressables)addressables.BuildAddressablesWithPlayerBuild=previous;}
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Acoustic Player build failed: "+report.summary.result);
            var licenses=Path.Combine(Path.GetDirectoryName(path),"Licenses");Directory.CreateDirectory(licenses);
            foreach(var file in new[]{"LICENSE.md","THIRDPARTY.md"})File.Copy("Assets/Plugins/SteamAudio/"+file,Path.Combine(licenses,"SteamAudio-"+file),true);
            Debug.Log("[TunnelAcoustics] Build succeeded.");
        }
    }
}
