using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Shinzui.Infrastructure.Services;
using Shinzui.View;
using Shinzui.View.Flashlight;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>Opt-in player evidence capture; inactive during ordinary gameplay and never writes user settings</summary>
public sealed class GraphicsPlayerValidation : MonoBehaviour
{
    private readonly List<object> _samples = new();
    private readonly FrameTiming[] _timing = new FrameTiming[1];
    private RenderTexture _target;
    private Camera _camera;
    private string _label;
    private string _folder;
    private int _seed;
    private object _rayCounts;
    private Shinzui.Domain.Settings.GraphicsSettings _settings;
    private bool _portalsDisabled;

    /// <summary>Activate only when the local development player is explicitly launched for graphics validation</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Debug.isDebugBuild || !Environment.GetCommandLineArgs().Contains("--graphics-validation")) return;
        var root = new GameObject("Graphics player validation");
        DontDestroyOnLoad(root);
        root.AddComponent<GraphicsPlayerValidation>();
    }

    /// <summary>Read an optional command-line argument without changing persistent preferences</summary>
    /// <param name="key">Exact option token</param>
    /// <param name="fallback">Default value</param>
    /// <returns>Following argument or fallback</returns>
    private static string Argument(string key, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }

    /// <summary>Load through the production addressable path and record a warmed, repeatable frozen-scene render workload</summary>
    private IEnumerator Start()
    {
        _label = Argument("--graphics-label", "player-hdrp");
        _folder = Argument("--graphics-output", Path.Combine(UnityEngine.Application.dataPath, "../GraphicsEvidence"));
        _seed = int.Parse(Argument("--graphics-seed", "2777"));
        Directory.CreateDirectory(_folder);
        yield return null;
        Shinzui.DI.GenerateTunnel.GenerateTunnelLifetimeScope.RuntimeSeedOverride = _seed;
        var load = Addressables.LoadSceneAsync("Assets/Shinzui/Scenes/StageTemp.unity");
        yield return load;
        if (load.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            File.WriteAllText(Path.Combine(_folder, _label + "-failure.txt"), load.OperationException?.ToString() ?? "Scene load failed");
            UnityEngine.Application.Quit(2);
            yield break;
        }
        Time.timeScale = 0;
        while (Camera.main == null || TunnelGateView.ActiveGates.Count == 0) yield return null;
        var graphics = new Shinzui.Domain.Settings.GraphicsSettings();
        graphics.SetQualityPreset(Shinzui.Domain.Settings.GraphicsQualityPreset.Ultra);
        graphics.EnableRayTracing = !Environment.GetCommandLineArgs().Contains("--graphics-raster");
        graphics.GiAndReflectionQuality = int.Parse(Argument("--graphics-lighting-quality", "3"));
        graphics.VolumeLightQuality = int.Parse(Argument("--graphics-fog-quality", "3"));
        graphics.ShadowQuality = int.Parse(Argument("--graphics-shadow-quality", "3"));
        graphics.PortalResolutionScale = float.Parse(Argument("--graphics-portal-scale", "0.75"), System.Globalization.CultureInfo.InvariantCulture);
        if (graphics.GiAndReflectionQuality != 3 || graphics.VolumeLightQuality != 3 || graphics.ShadowQuality != 3 ||
            !Mathf.Approximately(graphics.PortalResolutionScale, .75f))
            graphics.QualityPreset = Shinzui.Domain.Settings.GraphicsQualityPreset.Custom;
        graphics.EnableDepthOfField = false; // Frozen simulation cannot update gameplay autofocus.
        graphics.EnableVSync = false;
        graphics.FrameRateLimit = 0;
        graphics.ScreenMode = 1;
        graphics.Validate();
        _settings = graphics.Clone();
        new UnitySettingsApplier().ApplyGraphics(graphics);
        _portalsDisabled = Environment.GetCommandLineArgs().Contains("--graphics-no-portals");
        if (_portalsDisabled)
            foreach (var gate in TunnelGateView.ActiveGates.ToArray()) gate.enabled = false;
        _camera = Camera.main;
        var brain = _camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        if (brain != null) brain.enabled = false;
        _camera.transform.SetPositionAndRotation(new Vector3(0, 2.2228f, .248f), Quaternion.identity);
        _target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        _target.Create();
        _camera.targetTexture = _target;
        _camera.aspect = 16f / 9;
        double started = Time.realtimeSinceStartupAsDouble;
        int frames = 0;
        while (Time.realtimeSinceStartupAsDouble - started < 40 || _samples.Count < 300)
        {
            FindAnyObjectByType<FlashlightView>()?.SetLightActive(true);
            yield return null;
            frames++;
            FrameTimingManager.CaptureFrameTimings();
            uint count = FrameTimingManager.GetLatestTimings(1, _timing);
            if (frames > 120 && Time.realtimeSinceStartupAsDouble - started > 10)
                _samples.Add(new { frame = Time.frameCount, wallMs = Time.unscaledDeltaTime * 1000,
                    cpuMs = count > 0 ? _timing[0].cpuFrameTime : 0, gpuMs = count > 0 ? _timing[0].gpuFrameTime : 0 });
        }
        SaveEvidence();
        // Count actual GPU rays after performance sampling so instrumentation does not skew those samples.
        if (Environment.GetCommandLineArgs().Contains("--graphics-count-rays") && RenderPipelineManager.currentPipeline is HDRenderPipeline && SetRayCounting(true))
        {
            for (int i = 0; i < 30; i++) yield return null;
            // HDRP 17.5's aggregate reduction dispatches the wrong clear kernel. Sum the actual
            // per-pixel GPU ray counters instead; never use its corrupted GetRaysPerFrame totals.
            var texture = Resources.FindObjectsOfTypeAll<RenderTexture>().FirstOrDefault(t =>
                t.name.StartsWith("RayCountTextureDebug") && t.width == 1920 && t.height == 1080);
            bool completed = texture == null;
            if (texture == null) _rayCounts = new { available = false, reason = "No ray counter texture allocated" };
            else AsyncGPUReadback.Request(texture, 0, request =>
            {
                var values = new Dictionary<string, ulong>();
                if (!request.hasError)
                    for (int layer = 0; layer < request.layerCount; layer++)
                    {
                        ulong sum = 0;
                        var pixels = request.GetData<ushort>(layer);
                        for (int pixel = 0; pixel < pixels.Length; pixel++) sum += pixels[pixel];
                        values[((RayCountValues)layer).ToString()] = sum;
                    }
                _rayCounts = new { available = !request.hasError, method = "Raw R16_UInt GPU texture readback, CPU sum by ray type", values };
                completed = true;
            });
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!completed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (!completed) _rayCounts = new { available = false, reason = "GPU readback timed out" };
            SetRayCounting(false);
            for (int i = 0; i < 30; i++) yield return null;
            SaveEvidence();
        }
        UnityEngine.Application.Quit();
    }

    /// <summary>Record actual device, render configuration, pipeline RT state, volume overrides and frame samples</summary>
    private void SaveEvidence()
    {
        var old = RenderTexture.active;
        RenderTexture.active = _target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        RenderTexture.active = old;
        File.WriteAllBytes(Path.Combine(_folder, _label + ".png"), image.EncodeToPNG());
        Destroy(image);
        var camera = HDCamera.GetOrCreate(_camera);
        var gi = camera.volumeStack.GetComponent<GlobalIllumination>();
        var ssr = camera.volumeStack.GetComponent<ScreenSpaceReflection>();
        var report = new
        {
            gpu = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString(), hardwareRT = SystemInfo.supportsRayTracing,
            pipelineRT = (RenderPipelineManager.currentPipeline as HDRenderPipeline)?.rayTracingSupported,
            rayTracingFrameEnabled = camera.frameSettings.IsEnabled(FrameSettingsField.RayTracing),
            giEnabled = gi.enable.value, giTracing = gi.tracing.value.ToString(), reflectionEnabled = ssr.enabled.value, reflectionTracing = ssr.tracing.value.ToString(),
            giFullResolution = gi.fullResolution, reflectionFullResolution = ssr.fullResolution,
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, seed = _seed, width = 1920, height = 1080, renderScale = 1,
            editor = false, developmentBuild = Debug.isDebugBuild, frozenGameplay = true, depthOfField = false,
            settings = _settings, portalsDisabled = _portalsDisabled,
            activeCameras = Camera.allCameras.Select(c => new { c.name, c.pixelWidth, c.pixelHeight }).ToArray(),
            gates = TunnelGateView.ActiveGates.Count, capability = HdrpGraphicsRuntime.CapabilityReport(), rayCounts = _rayCounts,
            rayCounterScope = "Separate diagnostic interval after performance samples; main 1920x1080 counter texture", samples = _samples
        };
        File.WriteAllText(Path.Combine(_folder, _label + ".json"), JsonConvert.SerializeObject(report, Formatting.Indented));
        Debug.Log("GRAPHICS_PLAYER_CAPTURE_COMPLETE " + _label);
    }

    /// <summary>Enable HDRP's own diagnostic GPU ray counter without substituting capability flags for traced work</summary>
    /// <param name="enabled">Diagnostic counter state</param>
    /// <returns>Whether the installed development runtime exposes its counter</returns>
    private static bool SetRayCounting(bool enabled)
    {
        try
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.FlattenHierarchy;
            var type = typeof(HDRenderPipeline).Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDDebugDisplaySettings");
            var instance = type.GetProperty("Instance", flags).GetValue(null);
            var settings = type.GetProperty("displayStats", flags).GetValue(instance);
            var stats = settings.GetType().GetProperty("debugDisplayStats", flags).GetValue(settings);
            stats.GetType().GetField("countRays", flags).SetValue(stats, enabled);
            return true;
        }
        catch (Exception error) { Debug.LogWarning("Ray counter unavailable: " + error.Message); return false; }
    }
}
