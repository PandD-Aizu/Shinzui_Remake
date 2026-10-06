using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Gfx = Shinzui.Domain.Settings.GraphicsSettings;

namespace Shinzui.Infrastructure.Services
{
    /// <summary>Applies confirmed preferences to HDRP without modifying authored volume assets</summary>
    public sealed class HdrpGraphicsRuntime : MonoBehaviour
    {
        private static HdrpGraphicsRuntime _instance;
        private static Gfx _settings;
        private Volume _volume;
        private VolumeProfile _profile;
        private bool _configuredRt;
        public static float PortalScale => _settings?.PortalResolutionScale ?? .75f;
        public static bool DepthOfFieldEnabled => _settings?.EnableDepthOfField ?? true;
        public static bool MotionBlurEnabled => _settings?.EnableMotionBlur ?? false;
        public static int ShadowQuality => _settings?.ShadowQuality ?? 3;
        public static bool RayTracingConfigured => _instance != null && _instance._configuredRt;

        /// <summary>Describe hardware, pipeline and configured effects separately</summary>
        /// <returns>A truthful capability and active configuration description</returns>
        public static string CapabilityReport()
        {
            bool hardware = SystemInfo.supportsRayTracing && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12;
            bool pipeline = RenderPipelineManager.currentPipeline is HDRenderPipeline hd && hd.rayTracingSupported;
            bool lighting = _settings != null && _settings.GiAndReflectionQuality > 0;
            bool reflections = lighting && _settings.EnableSsr;
            string active = RayTracingConfigured
                ? "RT indirect lighting configured / reflections " + (reflections ? "RT" : "off")
                : "Raster lighting / indirect lighting " + (lighting ? "screen-space" : "off") +
                  " / reflections " + (reflections ? "screen-space" : "off");
            return $"{SystemInfo.graphicsDeviceName} / {SystemInfo.graphicsDeviceType}\nHardware RT: {(hardware ? "supported" : "unavailable")} / HDRP RT resources: {(pipeline ? "ready" : "unavailable")}\n{active}. Portal views use raster lighting.";
        }

        /// <summary>Install persistent runtime overrides for the confirmed settings snapshot</summary>
        /// <param name="settings">Confirmed or temporarily previewed display candidate</param>
        public static void Apply(Gfx settings)
        {
            _settings = settings.Clone();
            if (_instance == null)
            {
                var host = new GameObject("Graphics preferences (runtime)");
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<HdrpGraphicsRuntime>();
                _instance._volume = host.AddComponent<Volume>();
                _instance._volume.isGlobal = true;
                _instance._volume.priority = 10000;
                _instance._profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _instance._volume.sharedProfile = _instance._profile;
                var horror = host.AddComponent<CustomPassVolume>();
                horror.isGlobal = true;
                horror.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
                horror.customPasses.Add(new Rendering.HdrpHorrorPass { name = "Detection and death feedback" });
                RenderPipelineManager.beginCameraRendering += _instance.ConfigureCamera;
            }
            _instance.ConfigureVolumes();
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include))
                _instance.ConfigureCamera(default, camera);
        }

        /// <summary>Resolve hardware capability again after a pipeline is recreated</summary>
        private void Update()
        {
            if (_configuredRt != CanUseRayTracing()) ConfigureVolumes();
        }

        /// <summary>Require every pipeline and hardware prerequisite for enabling traced effects</summary>
        /// <returns>Whether the requested ray tracing path can be configured</returns>
        private static bool CanUseRayTracing() => _settings != null && _settings.EnableRayTracing && _settings.GiAndReflectionQuality > 0
            && SystemInfo.supportsRayTracing && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12
            && RenderPipelineManager.currentPipeline is HDRenderPipeline hd && hd.rayTracingSupported;

        /// <summary>Get or add an override without changing the profile's other artistic parameters</summary>
        /// <typeparam name="T">HDRP volume component</typeparam>
        /// <returns>The runtime-owned component</returns>
        private T Effect<T>() where T : VolumeComponent => _profile.TryGet<T>(out var value) ? value : _profile.Add<T>();

        /// <summary>Apply individual preferences instead of forcing Ultra values over custom choices</summary>
        private void ConfigureVolumes()
        {
            var g = _settings;
            _configuredRt = CanUseRayTracing();
            int quality = Mathf.Clamp(g.GiAndReflectionQuality - 1, 0, 2);
            var gi = Effect<GlobalIllumination>();
            gi.enable.Override(g.GiAndReflectionQuality > 0);
            gi.tracing.Override(_configuredRt ? RayCastingMode.RayTracing : RayCastingMode.RayMarching);
            gi.mode.Override(RayTracingMode.Performance);
            gi.quality.Override(quality);
            gi.fullResolutionSS.Override(g.GiAndReflectionQuality >= 3);
            var reflection = Effect<ScreenSpaceReflection>();
            reflection.enabled.Override(g.EnableSsr && g.GiAndReflectionQuality > 0);
            reflection.tracing.Override(_configuredRt ? RayCastingMode.RayTracing : RayCastingMode.RayMarching);
            reflection.mode.Override(RayTracingMode.Performance);
            reflection.quality.Override(quality);
            var ao = Effect<ScreenSpaceAmbientOcclusion>();
            ao.intensity.Override(g.EnableAo ? .65f : 0f);
            ao.rayTracing.Override(false);
            var contact = Effect<ContactShadows>();
            contact.enable.Override(g.EnableContactShadow && g.ShadowQuality > 0);
            contact.length.Override(.25f);
            Effect<IndirectLightingController>().reflectionLightingMultiplier.Override(g.ReflectionIntensity);
            var fog = Effect<Fog>();
            fog.enabled.Override(g.VolumeLightQuality > 0);
            fog.enableVolumetricFog.Override(g.VolumeLightQuality > 0);
            fog.quality.Override(Mathf.Clamp(g.VolumeLightQuality - 1, 0, 2));
            Effect<Bloom>().intensity.Override(g.EnableBloom ? .15f : 0f);
            Effect<FilmGrain>().intensity.Override(g.EnableFilmGrain ? .12f : 0f);
            Effect<LensDistortion>().intensity.Override(g.EnableLensDistortion ? -.08f : 0f);
            Effect<MotionBlur>().intensity.Override(g.EnableMotionBlur ? .12f : 0f);
            Effect<DepthOfField>().focusMode.Override(g.EnableDepthOfField ? DepthOfFieldMode.UsePhysicalCamera : DepthOfFieldMode.Off);
            Effect<ColorAdjustments>().postExposure.Override((g.BrightnessValue - .5f) * 3f);
            var shadows = Effect<HDShadowSettings>();
            shadows.maxShadowDistance.Override(g.ShadowQuality switch { 0 => 0, 1 => 18, 2 => 35, _ => 60 });
        }

        /// <summary>Apply frame settings to newly spawned cameras and cameras loaded in later scenes</summary>
        /// <param name="context">Active render context</param>
        /// <param name="camera">Camera about to render</param>
        private void ConfigureCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game || camera.name.Contains("Portal Camera")) return;
            var data = camera.GetComponent<HDAdditionalCameraData>();
            if (data == null) data = camera.gameObject.AddComponent<HDAdditionalCameraData>();
            data.customRenderingSettings = true;
            data.antialiasing = _settings.AntiAliasingType switch
            {
                1 => HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing,
                2 => HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                3 => HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing,
                _ => HDAdditionalCameraData.AntialiasingMode.None
            };
            camera.allowHDR = true;
            camera.allowMSAA = false;
            SetFrame(data, FrameSettingsField.CustomPass, true);
            SetFrame(data, FrameSettingsField.Postprocess, true);
            SetFrame(data, FrameSettingsField.Refraction, true);
            SetFrame(data, FrameSettingsField.RayTracing, _configuredRt);
            SetFrame(data, FrameSettingsField.SSGI, _settings.GiAndReflectionQuality > 0);
            SetFrame(data, FrameSettingsField.SSR, _settings.EnableSsr);
            SetFrame(data, FrameSettingsField.SSAO, _settings.EnableAo);
            SetFrame(data, FrameSettingsField.ShadowMaps, _settings.ShadowQuality > 0);
            SetFrame(data, FrameSettingsField.ContactShadows, _settings.EnableContactShadow);
            SetFrame(data, FrameSettingsField.Volumetrics, _settings.VolumeLightQuality > 0);
        }

        /// <summary>Set an explicit frame override</summary>
        /// <param name="data">Camera settings</param>
        /// <param name="field">Frame feature</param>
        /// <param name="enabled">Desired state</param>
        private static void SetFrame(HDAdditionalCameraData data, FrameSettingsField field, bool enabled)
        {
            data.renderingPathCustomFrameSettings.SetEnabled(field, enabled);
            data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
        }

        /// <summary>Release callbacks and transient profile when play ends</summary>
        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= ConfigureCamera;
            if (_profile != null) Destroy(_profile);
            if (_instance == this) { _instance = null; _settings = null; }
        }
    }
}
