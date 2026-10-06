using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Build the production Title and its addressable gameplay content for local Windows validation</summary>
public static class GraphicsWindowsBuild
{
    /// <summary>Build addressables and a development player with real frame timing enabled</summary>
    public static void Build()
    {
        const string folder = "Artifacts/GraphicsValidation";
        Directory.CreateDirectory(folder);
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var previous = settings.BuildAddressablesWithPlayerBuild;
        try
        {
            AddressableAssetSettings.BuildPlayerContent(out var content);
            if (!string.IsNullOrEmpty(content.Error)) throw new InvalidOperationException(content.Error);
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Shinzui/Scenes/Title.unity" },
                target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/GraphicsValidation/Shinzui.exe",
                options = BuildOptions.Development
            });
            File.WriteAllText(folder + "/windows-build.json", JsonConvert.SerializeObject(new
            {
                result = report.summary.result.ToString(), errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings, seconds = report.summary.totalTime.TotalSeconds,
                bytes = report.summary.totalSize, path = report.summary.outputPath,
                steps = report.steps
            }, Formatting.Indented));
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            Debug.Log("GRAPHICS_WINDOWS_BUILD_COMPLETE " + report.summary.outputPath);
        }
        catch (Exception error)
        {
            File.WriteAllText(folder + "/windows-build-failure.txt", error.ToString());
            Debug.LogException(error);
        }
        finally { settings.BuildAddressablesWithPlayerBuild = previous; AssetDatabase.SaveAssets(); }
    }
}
