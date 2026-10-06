using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.View
{
    /// <summary>HDRP portal cameras render in normal depth order, never inside a rendering callback</summary>
    public partial class TunnelGateView
    {
        private Camera[] _hdrpCameras;
        private MaterialPropertyBlock _portalProperties;
        private Rect _hdrpCrop = new Rect(0, 0, 1, 1);
        private static readonly int PortalUvTransformId = Shader.PropertyToID("_PortalUvTransform");
        private static readonly bool CropPortalTargets = !System.Array.Exists(System.Environment.GetCommandLineArgs(), a => a == "--graphics-uncropped-portals");
        public static float HdrpPortalScale => Infrastructure.Services.HdrpGraphicsRuntime.PortalScale;
        private static bool UsesHdrp => GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset;

        /// <summary>Prepare all portal cameras before the pipeline collects its render list</summary>
        private void LateUpdate()
        {
            if (!UsesHdrp || targetGate == null) return;
            InitializePortalResources();
            _mainCamera = Camera.main;
            if (_mainCamera == null || _portalCamera == null) return;
            if (_hdrpCameras == null)
            {
                _hdrpCameras = new Camera[MaxRecursionDepth];
                _hdrpCameras[0] = _portalCamera;
                for (int i = 1; i < _hdrpCameras.Length; i++)
                {
                    var host = new GameObject($"[HDRP Portal Camera {i + 1}]");
                    host.transform.SetParent(transform, false);
                    _hdrpCameras[i] = host.AddComponent<Camera>();
                }

                HdrpPortalPass.EnsureVolume();
            }
            bool visible = IsVisibleFromMainCamera(_mainCamera);
            foreach (var portalCamera in _hdrpCameras) portalCamera.enabled = visible;
            DisableHdrpLightCopies();
            if (!visible) { ReleaseHdrpLightSlots(); return; }
            _lastVisibleTime = Time.unscaledTime;
            _hdrpCrop = CropPortalTargets ? GetHdrpPortalCrop() : new Rect(0, 0, 1, 1);
            int width = Mathf.Max(1, Mathf.RoundToInt(_mainCamera.pixelWidth * HdrpPortalScale * _hdrpCrop.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(_mainCamera.pixelHeight * HdrpPortalScale * _hdrpCrop.height));
            if (_portalRT == null || _portalRT.width != width || _portalRT.height != height)
            {
                foreach (var camera in _hdrpCameras) camera.targetTexture = null;
                ReleasePortalTextures();
                _portalRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf) { name = $"HDRP Portal {name}" };
                _portalRT.Create();
                _portalRenderer.sharedMaterial.SetTexture("_MainTex", _portalRT);
            }
            EnsureRecursionRenderTextures(MaxRecursionDepth - 1, Mathf.Max(1, width / 2), Mathf.Max(1, height / 2));
            RefreshCandidateLights();
            _mainCamera.cullingMask &= ~PortalLightMask;
            for (int i = 0; i < _hdrpCameras.Length; i++)
            {
                var portalCamera = _hdrpCameras[i];
                CopyCameraSettings(_mainCamera, portalCamera);
                PrepareHdrpLightCopies(portalCamera, i);
                portalCamera.targetTexture = i == 0 ? _portalRT : _recursionRTs[i - 1];
                portalCamera.depth = _mainCamera.depth - 100 - i;
                portalCamera.enabled = true;
                portalCamera.allowMSAA = false;
                portalCamera.allowDynamicResolution = false;
                portalCamera.useOcclusionCulling = false;
                MatchPortalCameraTransformForDepth(portalCamera.transform, transform, targetGate.transform, i + 1);
                if (useDynamicNearClip) SetObliqueNearClipPlane(portalCamera, targetGate.transform);
                // HDRP's depth-based lighting reads Camera.nearClipPlane as well as the projection.
                // Keep its depth constants consistent with the portal clip plane at the view center.
                var projection = portalCamera.projectionMatrix;
                float effectiveNear = projection.m23 / (projection.m22 - 1);
                if (effectiveNear > 0 && effectiveNear < portalCamera.farClipPlane)
                    portalCamera.nearClipPlane = effectiveNear;
                portalCamera.projectionMatrix = projection;
                portalCamera.projectionMatrix = CropProjection(_hdrpCrop) * portalCamera.projectionMatrix;
                var data = portalCamera.GetComponent<HDAdditionalCameraData>();
                if (data == null) data = portalCamera.gameObject.AddComponent<HDAdditionalCameraData>();
                data.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
                data.backgroundColorHDR = Color.black;
                data.volumeLayerMask = ~0;
                data.volumeAnchorOverride = _mainCamera.transform;
                data.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
                data.customRenderingSettings = true;
                SetPortalFrame(data, FrameSettingsField.CustomPass, true);
                SetPortalFrame(data, FrameSettingsField.Postprocess, false);
                SetPortalFrame(data, FrameSettingsField.Refraction, true);
                // Store unexposed radiance; composition applies the receiving camera's pre-exposure once.
                SetPortalFrame(data, FrameSettingsField.ExposureControl, false);
                SetPortalFrame(data, FrameSettingsField.RayTracing, Infrastructure.Services.HdrpGraphicsRuntime.RayTracingConfigured);
                SetPortalFrame(data, FrameSettingsField.SSR, false);
                SetPortalFrame(data, FrameSettingsField.SSGI, Infrastructure.Services.HdrpGraphicsRuntime.RayTracingConfigured);
                SetPortalFrame(data, FrameSettingsField.MotionVectors, true);
            }
        }

        /// <summary>Shade only the screen rectangle visible through this portal, preserving its requested pixel density</summary>
        /// <returns>Conservative viewport crop, with a full view while crossing the near plane</returns>
        private Rect GetHdrpPortalCrop()
        {
            var bounds = _portalRenderer.bounds;
            var lower = Vector2.one;
            var upper = Vector2.zero;
            for (int corner = 0; corner < 8; corner++)
            {
                var world = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                var point = _mainCamera.WorldToViewportPoint(world);
                if (point.z <= _mainCamera.nearClipPlane + .1f) return new Rect(0, 0, 1, 1);
                lower = Vector2.Min(lower, new Vector2(point.x, point.y));
                upper = Vector2.Max(upper, new Vector2(point.x, point.y));
            }
            // Quantize outward with a guard band to reduce target reallocations during movement.
            float width = Mathf.Max(1, _mainCamera.pixelWidth * HdrpPortalScale);
            float height = Mathf.Max(1, _mainCamera.pixelHeight * HdrpPortalScale);
            float left = Mathf.Clamp01(Mathf.Floor((lower.x * width - 8) / 32) * 32 / width);
            float right = Mathf.Clamp01(Mathf.Ceil((upper.x * width + 8) / 32) * 32 / width);
            float bottom = Mathf.Clamp01(Mathf.Floor((lower.y * height - 8) / 32) * 32 / height);
            float top = Mathf.Clamp01(Mathf.Ceil((upper.y * height + 8) / 32) * 32 / height);
            return right > left && top > bottom ? Rect.MinMaxRect(left, bottom, right, top) : new Rect(0, 0, 1, 1);
        }

        /// <summary>Expand a viewport rectangle to the render target without changing world-space rays</summary>
        /// <param name="crop">Visible rectangle in the uncropped viewport</param>
        /// <returns>Clip-space projection adjustment</returns>
        private static Matrix4x4 CropProjection(Rect crop)
        {
            var matrix = Matrix4x4.identity;
            matrix.m00 = 1 / crop.width;
            matrix.m03 = (1 - 2 * crop.x - crop.width) / crop.width;
            matrix.m11 = 1 / crop.height;
            matrix.m13 = (1 - 2 * crop.y - crop.height) / crop.height;
            return matrix;
        }

        /// <summary>Override one portal frame feature explicitly</summary>
        /// <param name="data">Portal camera settings</param>
        /// <param name="field">Frame feature</param>
        /// <param name="enabled">Feature state</param>
        private static void SetPortalFrame(HDAdditionalCameraData data, FrameSettingsField field, bool enabled)
        {
            data.renderingPathCustomFrameSettings.SetEnabled(field, enabled);
            data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
        }

        /// <summary>Release scheduled recursion cameras together with the gate</summary>
        private void ReleaseHdrpCameras()
        {
            ReleaseHdrpLightCopies();
            if (_hdrpCameras != null)
                foreach (var camera in _hdrpCameras) if (camera != null) camera.targetTexture = null;
            if (_hdrpCameras != null)
                for (int i = 1; i < _hdrpCameras.Length; i++)
                    if (_hdrpCameras[i] != null) Destroy(_hdrpCameras[i].gameObject);
            _hdrpCameras = null;
        }

        /// <summary>Draw only the portal's reserved user stencil bit and projected image</summary>
        /// <param name="command">HDRP custom pass command buffer</param>
        /// <param name="camera">Camera owning this render without global renderer mutations</param>
        internal void DrawHdrpPortal(CommandBuffer command, Camera camera)
        {
            if (_portalRenderer == null || !_portalRenderer.enabled || _portalMaskRenderer == null || !_portalMaskRenderer.enabled) return;
            Texture texture = _portalRT;
            Rect cameraCrop = new Rect(0, 0, 1, 1);
            foreach (var owner in ActiveGates)
            {
                if (owner._hdrpCameras == null) continue;
                int level = System.Array.IndexOf(owner._hdrpCameras, camera);
                if (level < 0) continue;
                cameraCrop = owner._hdrpCrop;
                if (this == owner.targetGate) return;
                if (this == owner) texture = level + 1 < owner._hdrpCameras.Length ? owner._recursionRTs[level] : Texture2D.blackTexture;
                break;
            }
            _portalProperties ??= new MaterialPropertyBlock();
            _portalProperties.SetTexture("_MainTex", texture != null ? texture : Texture2D.blackTexture);
            _portalProperties.SetFloat(PortalExposureMultiplierId, PortalExposureMultiplier);
            // HDRP's render-target projection and this shader use the same viewport orientation.
            _portalProperties.SetVector(PortalUvTransformId, new Vector4(
                cameraCrop.width / _hdrpCrop.width, cameraCrop.height / _hdrpCrop.height,
                (cameraCrop.x - _hdrpCrop.x) / _hdrpCrop.width,
                (cameraCrop.y - _hdrpCrop.y) / _hdrpCrop.height));
            command.DrawMesh(_portalMesh, _portalMaskRenderer.localToWorldMatrix, _portalMaskRenderer.sharedMaterial, 0, 0);
            command.DrawMesh(_portalMesh, _portalRenderer.localToWorldMatrix, _portalRenderer.sharedMaterial, 0, 0, _portalProperties);
        }
    }

    /// <summary>Compose portal surfaces after opaque shading using HDRP user stencil bit 64</summary>
    [System.Serializable]
    public sealed class HdrpPortalPass : CustomPass
    {
        private static CustomPassVolume _volume;
        private RTHandle _opaque;

        /// <summary>Own a camera-sized scene copy that includes portal composition for supernatural refraction</summary>
        /// <param name="context">Current render context</param>
        /// <param name="cmd">Setup commands</param>
        protected override void Setup(ScriptableRenderContext context, CommandBuffer cmd) =>
            _opaque = RTHandles.Alloc(Vector2.one,
                colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
                useDynamicScale: true, name: "Portal-aware refraction source");
        /// <summary>Create a global composition pass for runtime-generated portals</summary>
        public static void EnsureVolume()
        {
            if (_volume != null) return;
            var host = new GameObject("HDRP Portal Composition");
            Object.DontDestroyOnLoad(host);
            _volume = host.AddComponent<CustomPassVolume>();
            _volume.isGlobal = true;
            _volume.injectionPoint = CustomPassInjectionPoint.BeforeTransparent;
            _volume.customPasses.Add(new HdrpPortalPass { name = "Portal user-stencil composition" });
        }
        /// <summary>Compose visible portal masks and images into the active camera target</summary>
        /// <param name="ctx">HDRP pass targets and command buffer</param>
        protected override void Execute(CustomPassContext ctx)
        {
            CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ctx.cameraDepthBuffer);
            foreach (var gate in TunnelGateView.ActiveGates)
                if (gate != null && gate.isActiveAndEnabled) gate.DrawHdrpPortal(ctx.cmd, ctx.hdCamera.camera);
            HDUtils.BlitCameraTexture(ctx.cmd, ctx.cameraColorBuffer, _opaque);
            ctx.cmd.SetGlobalTexture("_ShinzuiOpaqueColor", _opaque);
            ctx.cmd.SetGlobalVector("_ShinzuiOpaqueScale", new Vector4(
                (float)ctx.hdCamera.actualWidth / _opaque.rt.width,
                (float)ctx.hdCamera.actualHeight / _opaque.rt.height, 0, 0));
        }

        /// <summary>Release the refraction source with the rendering pipeline</summary>
        protected override void Cleanup() => _opaque?.Release();
    }
}
