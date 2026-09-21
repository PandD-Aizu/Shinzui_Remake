using System;
using System.IO;
using Shinzui.Infrastructure.CustomSpatialAudio;
using UnityEditor;
using UnityEngine;

namespace Shinzui.Editor
{
    public static class CustomSpatialAudioAssets
    {
        [MenuItem("Shinzui/Audio/Custom Spatial Audio/Ensure game configuration")]
        public static void EnsureConfiguration()
        {
            // Legacy Studio events still require their authored plug-in, loaded before any Bank.
            var platform = FMODUnity.Settings.Instance.DefaultPlatform;
            if (!platform.Plugins.Contains("phonon_fmod"))
            {
                platform.Plugins.Add("phonon_fmod");
                EditorUtility.SetDirty(platform);
                EditorUtility.SetDirty(FMODUnity.Settings.Instance);
                AssetDatabase.SaveAssets();
            }
            const string path = "Assets/Shinzui/Audio/Resources/SpatialAudio/CustomSpatialAudioConfiguration.asset";
            if (AssetDatabase.LoadAssetAtPath<CustomSpatialAudioConfiguration>(path)) return;
            var config = ScriptableObject.CreateInstance<CustomSpatialAudioConfiguration>();
            config.Sounds = new[] { new CustomSpatialAudioConfiguration.DrySound {
                Id = "footstep.prototype", Path = "CustomSpatialAudio/Audio/FootstepPrototype.wav" } };
            if (!File.Exists(Path.Combine(UnityEngine.Application.streamingAssetsPath, config.Sounds[0].Path)))
                throw new InvalidOperationException("The packaged dry footstep is missing.");
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[CustomSpatialAudio] Game configuration created.");
        }
    }
}
