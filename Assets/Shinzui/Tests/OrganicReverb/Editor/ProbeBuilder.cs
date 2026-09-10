using System;
using System.IO;
using System.Linq;
using FMODUnity;
using Shinzui.AudioProbe.Infrastructure;
using SteamAudio;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Shinzui.AudioProbe.Editor
{
    [InitializeOnLoad]
    public static class ProbeBuilder
    {
        const string Root = "Assets/Shinzui/Tests/OrganicReverb";
        const string ScenePath = Root + "/OrganicReverbProbe.unity";
        const string RunningKey = "OrganicReverbProbe.CliRunning";

        static ProbeBuilder() { EditorApplication.update += Poll; }

        [MenuItem("Shinzui/Audio/Create Organic Reverb Probe")]
        public static void Create()
        {
            Directory.CreateDirectory(Root + "/Data");
            AssetDatabase.Refresh();
            var settings = SteamAudioSettings.Singleton;
            settings.audioEngine = AudioEngineType.FMODStudio;
            settings.realTimeRays = 8192;
            settings.realTimeBounces = 16;
            settings.realTimeDuration = 3;
            settings.realTimeMaxSources = 1;
            settings.realTimeCPUCoresPercentage = 5;
            settings.simulationUpdateInterval = .1f;
            EditorUtility.SetDirty(settings);
            var platform = FMODUnity.Settings.Instance.DefaultPlatform;
            if (!platform.Plugins.Contains("phonon_fmod")) platform.Plugins.Add("phonon_fmod");
            EditorUtility.SetDirty(platform);
            EditorUtility.SetDirty(FMODUnity.Settings.Instance);
            AssetDatabase.SaveAssets();
            EventManager.RefreshBanks();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var material = AssetDatabase.LoadAssetAtPath<SteamAudioMaterial>(Root + "/Data/Concrete.asset");
            if (!material)
            {
                material = ScriptableObject.CreateInstance<SteamAudioMaterial>();
                material.lowFreqAbsorption = .05f; material.midFreqAbsorption = .08f; material.highFreqAbsorption = .12f;
                material.lowFreqTransmission = 0; material.midFreqTransmission = 0; material.highFreqTransmission = 0;
                material.scattering = .2f;
                AssetDatabase.CreateAsset(material, Root + "/Data/Concrete.asset");
            }
            var visual = new UnityEngine.Material(Shader.Find("Universal Render Pipeline/Lit"));
            visual.color = new Color(.32f, .36f, .4f);
            string visualPath = Root + "/Data/ConcreteVisual.mat";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(visualPath))
            { UnityEngine.Object.DestroyImmediate(visual); visual = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(visualPath); }
            else AssetDatabase.CreateAsset(visual, visualPath);

            Box("Room floor", new Vector3(0,-.15f,0), new Vector3(6,.3f,8), material, visual);
            Box("Room ceiling", new Vector3(0,3.15f,0), new Vector3(6,.3f,8), material, visual);
            Box("Room left", new Vector3(-3,1.5f,0), new Vector3(.3f,3,8), material, visual);
            Box("Room right", new Vector3(3,1.5f,0), new Vector3(.3f,3,8), material, visual);
            Box("Room front", new Vector3(0,1.5f,4), new Vector3(6,3,.3f), material, visual);
            Box("Room back", new Vector3(0,1.5f,-4), new Vector3(6,3,.3f), material, visual);
            Box("Occlusion partition", new Vector3(2.25f,1.5f,0), new Vector3(1.5f,3,.2f), material, visual);
            Box("Corridor floor", new Vector3(40,-.15f,0), new Vector3(4,.3f,30), material, visual);
            Box("Corridor ceiling", new Vector3(40,3.15f,0), new Vector3(4,.3f,30), material, visual);
            Box("Corridor left", new Vector3(38,1.5f,0), new Vector3(.3f,3,30), material, visual);
            Box("Corridor right", new Vector3(42,1.5f,0), new Vector3(.3f,3,30), material, visual);
            Box("Outdoor ground", new Vector3(80,-.15f,0), new Vector3(20,.3f,20), material, visual);

            var listener = new GameObject("Probe Listener");
            listener.tag = "MainCamera";
            listener.transform.position = new Vector3(0,1.5f,-1.5f);
            listener.AddComponent<Camera>().backgroundColor = new Color(.15f,.18f,.22f);
            listener.AddComponent<StudioListener>();
            listener.AddComponent<SteamAudioListener>();
            var source = new GameObject("Probe Source");
            source.transform.position = new Vector3(0,1.5f,1.5f);
            var emitter = source.AddComponent<StudioEventEmitter>();
            var eventInfo = EventManager.EventFromPath("event:/OrganicReverbProbe");
            if (!eventInfo) throw new InvalidOperationException("Probe event is missing from the FMOD Bank cache.");
            emitter.EventReference = new EventReference { Guid = eventInfo.Guid, Path = eventInfo.Path };
            if (emitter.EventReference.IsNull) throw new InvalidOperationException("Probe bank/event was not imported.");
            var acoustics = source.AddComponent<SteamAudioSource>();
            acoustics.reflections = true;
            acoustics.reflectionsType = ReflectionsType.Realtime;
            acoustics.occlusion = true;
            acoustics.occlusionInput = OcclusionInput.SimulationDefined;
            var probe = new GameObject("Organic Reverb Experiment").AddComponent<OrganicReverbProbe>();
            probe.Listener = listener.transform; probe.Emitter = emitter; probe.AcousticSource = acoustics;
            new GameObject("Probe Light").AddComponent<Light>().type = LightType.Directional;
            var data = AssetDatabase.LoadAssetAtPath<SerializedData>(Root + "/Data/ProbeGeometry.asset");
            if (!data) { data = ScriptableObject.CreateInstance<SerializedData>(); AssetDatabase.CreateAsset(data, Root + "/Data/ProbeGeometry.asset"); }
            var mesh = new GameObject("Acoustic Geometry").AddComponent<SteamAudioStaticMesh>();
            mesh.asset = data; mesh.sceneNameWhenExported = "OrganicReverbProbe";
            EditorSceneManager.SaveScene(scene, ScenePath);
            SteamAudioManager.ExportScene(scene, false);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            if (data.data == null || data.data.Length == 0) throw new InvalidOperationException("Acoustic geometry export is empty.");
            Debug.Log("[OrganicReverbProbe] Scene created; geometry bytes=" + data.data.Length);
        }

        static void Box(string name, Vector3 position, Vector3 scale, SteamAudioMaterial material, UnityEngine.Material visual)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name; obj.transform.position = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = visual;
            obj.AddComponent<SteamAudioGeometry>().material = material;
        }

        public static void RunEditor()
        {
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(RunningKey, true);
            EditorApplication.EnterPlaymode();
        }

        static void Poll()
        {
            if (SessionState.GetBool(RunningKey, false) && EditorApplication.isPlaying && OrganicReverbProbe.Completed)
            {
                SessionState.SetBool(RunningKey, false);
                int exit = OrganicReverbProbe.Passed ? 0 : 1;
                EditorApplication.ExitPlaymode();
                EditorApplication.delayCall += () => EditorApplication.Exit(exit);
            }
        }

        public static void Build()
        {
            string path = "Builds/OrganicReverbProbe/OrganicReverbProbe.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // This scene has no Addressables. Do not build the game's unrelated content for the probe.
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            var previous = addressables ? addressables.BuildAddressablesWithPlayerBuild : default;
            BuildReport report;
            try
            {
                if (addressables) addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { ScenePath }, locationPathName = path,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
            }
            finally
            {
                if (addressables) addressables.BuildAddressablesWithPlayerBuild = previous;
            }
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Probe build failed: " + report.summary.result);
            var licenseDirectory = Path.Combine(Path.GetDirectoryName(path), "Licenses");
            Directory.CreateDirectory(licenseDirectory);
            foreach (var file in new[] { "LICENSE.md", "THIRDPARTY.md" })
                File.Copy("Assets/Plugins/SteamAudio/" + file, Path.Combine(licenseDirectory, "SteamAudio-" + file), true);
            Debug.Log("[OrganicReverbProbe] Build succeeded: " + Path.GetFullPath(path));
        }
    }
}
