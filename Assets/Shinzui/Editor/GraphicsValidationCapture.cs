using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Shinzui.View;
using Shinzui.View.Flashlight;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Repeatable still-image validation, explicitly separate from playable-build performance measurement</summary>
[InitializeOnLoad]
public static class GraphicsValidationCapture
{
    private static Camera _camera;
    private static RenderTexture _target;
    private static double _started;
    private static int _lastFrame;
    private static int _renderedFrames;
    private static readonly List<object> Samples = new();
    private static readonly FrameTiming[] Timing = new FrameTiming[1];
    private static readonly Dictionary<Material, Material> PreviewMaterials = new();
    private static readonly HashSet<Light> PreviewLights = new();
    private static Volume _previewVolume;

    /// <summary>Recover the pending capture after the play-mode domain reload</summary>
    static GraphicsValidationCapture()
    {
        EditorApplication.update += Tick;
        RenderPipelineManager.beginCameraRendering += ApplyFeedbackProbe;
    }

    /// <summary>Exercise gameplay feedback in explicitly named visual probes without saving scene state</summary>
    /// <param name="context">Render context</param>
    /// <param name="camera">Camera about to render</param>
    private static void ApplyFeedbackProbe(ScriptableRenderContext context, Camera camera)
    {
        string label = SessionState.GetString("Shinzui.GraphicsCapture", "");
        if (!EditorApplication.isPlaying || camera != Camera.main) return;
        if (label.Contains("-noise-"))
        {
            Shader.SetGlobalFloat("_HorrorNoiseIntensity", .65f);
            Shader.SetGlobalFloat("_HorrorDistortionIntensity", .04f);
            Shader.SetGlobalFloat("_HorrorChromaticAberrationIntensity", .02f);
            Shader.SetGlobalFloat("_HorrorScanlineIntensity", .3f);
        }
        if (label.Contains("-death-"))
            Shinzui.Infrastructure.Rendering.GameOverDissolve.GameOverDissolveRuntimeState.Set(
                .45f, .11f, .24f, 1, new Color(0, 0, .01f), new Color(.12f, .55f, .72f), new Color(1.05f, 1.22f, 1.25f));
    }

    /// <summary>Start a controlled fixed-view capture on the next play session</summary>
    /// <param name="label">Evidence filename prefix</param>
    public static void Start(string label)
    {
        SessionState.SetString("Shinzui.GraphicsCapture", label);
        EditorApplication.isPlaying = true;
    }

    /// <summary>Freeze simulation from the first available editor update, then sample rendered frames</summary>
    private static void Tick()
    {
        string label = SessionState.GetString("Shinzui.GraphicsCapture", "");
        if (string.IsNullOrEmpty(label) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (label.StartsWith("title-")) { CaptureTitle(label); return; }
        Time.timeScale = 0;
        if (Camera.main == null || TunnelGateView.ActiveGates.Count == 0) return;
        if (label.StartsWith("prototype-gameplay")) PrepareHdrpPreview();
        if (_camera == null)
        {
            if (GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset && !label.StartsWith("prototype-gameplay"))
                Shinzui.Infrastructure.Services.HdrpGraphicsRuntime.Apply(new Shinzui.Domain.Settings.GraphicsSettings { EnableDepthOfField = false, EnableRayTracing = !label.Contains("raster") });
            _camera = Camera.main;
            if (label.Contains("nowarp"))
                foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                    foreach (var material in renderer.materials)
                        if (material.shader.name == "Shinzui/BlackHoleLens") material.SetFloat("_PullStrength", 0);
            if (label.Contains("nolens"))
                foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                    if (System.Array.Exists(renderer.sharedMaterials, m => m != null && m.shader.name == "Shinzui/BlackHoleLens")) renderer.enabled = false;
            var brain = _camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            _camera.transform.SetPositionAndRotation(new Vector3(0, 2.2228f, .248f), Quaternion.identity);
            _target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            _target.Create();
            _camera.targetTexture = _target;
            _camera.aspect = 16f / 9f;
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = -1;
            _started = Time.realtimeSinceStartupAsDouble;
            _renderedFrames = 0;
            Samples.Clear();
        }
        Object.FindFirstObjectByType<FlashlightView>()?.SetLightActive(true);
        if (Time.frameCount == _lastFrame) return;
        _lastFrame = Time.frameCount;
        _renderedFrames++;
        FrameTimingManager.CaptureFrameTimings();
        uint count = FrameTimingManager.GetLatestTimings(1, Timing);
        double elapsed = Time.realtimeSinceStartupAsDouble - _started;
        if (elapsed > 5 && _renderedFrames > 60) Samples.Add(new { frame = _lastFrame, wallMs = Time.unscaledDeltaTime * 1000,
            cpuMs = count > 0 ? Timing[0].cpuFrameTime : 0, gpuMs = count > 0 ? Timing[0].gpuFrameTime : 0 });
        if (label.StartsWith("inspect-")) return;
        if (elapsed < 20 || Samples.Count < 120) return;
        string folder = "Artifacts/GraphicsValidation";
        Directory.CreateDirectory(folder);
        var old = RenderTexture.active;
        RenderTexture.active = _target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        RenderTexture.active = old;
        File.WriteAllBytes(Path.Combine(folder, label + ".png"), image.EncodeToPNG());
        Object.DestroyImmediate(image);
        float scale = GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp ? urp.renderScale : 1;
        var data = new { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, gpu = SystemInfo.graphicsDeviceName,
            api = SystemInfo.graphicsDeviceType.ToString(), hardwareRT = SystemInfo.supportsRayTracing, width = 1920, height = 1080,
            editor = true, frozenGameplay = true, renderScale = scale, flashlightOn = true, seed = 2777,
            cameraPosition = _camera.transform.position.ToString("F4"), cameraRotation = _camera.transform.eulerAngles.ToString("F4"),
            pipeline = GraphicsSettings.currentRenderPipeline.GetType().Name, gates = TunnelGateView.ActiveGates.Count,
            capability = Shinzui.Infrastructure.Services.HdrpGraphicsRuntime.CapabilityReport(), samples = Samples };
        File.WriteAllText(Path.Combine(folder, label + ".json"), JsonConvert.SerializeObject(data, Formatting.Indented));
        SessionState.EraseString("Shinzui.GraphicsCapture");
        Debug.Log("GRAPHICS_CONTROLLED_CAPTURE_COMPLETE " + label);
        _camera.targetTexture = null;
        Object.DestroyImmediate(_target);
        _camera = null;
        EditorApplication.isPlaying = false;
    }

    /// <summary>Capture the actual options hierarchy, with overlay canvases rendered through a temporary camera target</summary>
    /// <param name="label">Evidence filename</param>
    private static void CaptureTitle(string label)
    {
        var title = Object.FindAnyObjectByType<Shinzui.Src.Title.ButtonController>();
        if (title == null) return;
        if (_camera == null)
        {
            title.OpenOptions();
            title.AlignGraphicOption();
            _camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            _target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            _target.Create();
            _camera.targetTexture = _target;
            _camera.aspect = 16f / 9;
            _camera.cullingMask |= 1 << 5;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                if (canvas.isRootCanvas)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = _camera;
                    canvas.planeDistance = 1;
                }
            Canvas.ForceUpdateCanvases();
            _started = Time.realtimeSinceStartupAsDouble;
            return;
        }
        if (Time.realtimeSinceStartupAsDouble - _started < 5) return;
        var old = RenderTexture.active;
        RenderTexture.active = _target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        RenderTexture.active = old;
        Directory.CreateDirectory("Artifacts/GraphicsValidation");
        File.WriteAllBytes("Artifacts/GraphicsValidation/" + label + ".png", image.EncodeToPNG());
        Object.DestroyImmediate(image);
        SessionState.EraseString("Shinzui.GraphicsCapture");
        _camera.targetTexture = null;
        Object.DestroyImmediate(_target);
        _camera = null;
        Debug.Log("GRAPHICS_TITLE_CAPTURE_COMPLETE " + label);
        EditorApplication.isPlaying = false;
    }

    /// <summary>Preview the actual generated gameplay geometry without converting any material or prefab assets</summary>
    private static void PrepareHdrpPreview()
    {
        if (_previewVolume == null)
        {
            _previewVolume = new GameObject("Temporary HDRP gameplay proof").AddComponent<Volume>();
            _previewVolume.isGlobal = true;
            _previewVolume.priority = 100;
            var exposure = _previewVolume.profile.Add<UnityEngine.Rendering.HighDefinition.Exposure>(true);
            exposure.mode.Override(UnityEngine.Rendering.HighDefinition.ExposureMode.Fixed);
            exposure.fixedExposure.Override(8);
            var tone = _previewVolume.profile.Add<UnityEngine.Rendering.HighDefinition.Tonemapping>(true);
            tone.mode.Override(UnityEngine.Rendering.HighDefinition.TonemappingMode.ACES);
            // A frozen simulation cannot update the gameplay autofocus; disable it for this asset viability view.
            var settings = new Shinzui.Domain.Settings.GraphicsSettings { EnableDepthOfField = false };
            Shinzui.Infrastructure.Services.HdrpGraphicsRuntime.Apply(settings);
        }
        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || (!source.shader.name.StartsWith("Universal Render Pipeline/") && !source.shader.name.StartsWith("Standard"))) continue;
                if (!PreviewMaterials.TryGetValue(source, out var material))
                {
                    material = new Material(Shader.Find("HDRP/Lit")) { name = source.name + " (temporary HDRP proof)" };
                    string baseMap = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                    if (source.HasProperty(baseMap))
                    {
                        material.SetTexture("_BaseColorMap", source.GetTexture(baseMap));
                        material.SetTextureScale("_BaseColorMap", source.GetTextureScale(baseMap));
                    }
                    material.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.gray);
                    if (source.HasProperty("_BumpMap")) material.SetTexture("_NormalMap", source.GetTexture("_BumpMap"));
                    material.SetFloat("_Smoothness", .3f);
                    UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(material);
                    PreviewMaterials[source] = material;
                }
                materials[i] = material;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (light.name.Contains("Portal") || !PreviewLights.Add(light)) continue;
            float intensity = light.intensity;
            if (!light.TryGetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>(out var data))
                data = light.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
            light.intensity = intensity * 100;
        }
        var flashlight = Object.FindFirstObjectByType<FlashlightView>();
        if (flashlight != null)
        {
            var type = typeof(FlashlightView);
            var field = type.GetField("_baseIntensity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            float current = (float)field.GetValue(flashlight);
            if (current > 0 && current < 20) field.SetValue(flashlight, current * 100);
        }
    }
}
