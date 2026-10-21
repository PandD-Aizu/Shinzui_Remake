using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using State = Shinzui.Infrastructure.Rendering.GameOverDissolve.GameOverDissolveRuntimeState;

namespace Shinzui.Infrastructure.Rendering
{
    /// <summary>Preserves detection and death feedback after HDRP's normal post processing</summary>
    [System.Serializable]
    public sealed class HdrpHorrorPass : CustomPass
    {
        private Material _noise;
        private Material _death;
        private RTHandle _scratch;

        /// <summary>Allocate a separate source buffer so effects never sample their render target</summary>
        /// <param name="context">HDRP render context</param>
        /// <param name="cmd">Setup commands</param>
        protected override void Setup(ScriptableRenderContext context, CommandBuffer cmd)
        {
            _noise = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Shinzui/HdrpHorrorDetectionNoise"));
            _death = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Shinzui/HdrpGameOverDissolve"));
            _scratch = RTHandles.Alloc(Vector2.one, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
                dimension: TextureXR.dimension, slices: TextureXR.slices, useDynamicScale: true, name: "Horror effect source");
        }

        /// <summary>Apply authored gameplay feedback only to the player's final view</summary>
        /// <param name="ctx">Current camera targets</param>
        protected override void Execute(CustomPassContext ctx)
        {
            if (ctx.hdCamera.camera != Camera.main) return;
            if (Shader.GetGlobalFloat("_HorrorNoiseIntensity") > .001f && _noise != null)
            {
                HDUtils.BlitCameraTexture(ctx.cmd, ctx.cameraColorBuffer, _scratch);
                _noise.SetFloat("_HorrorNoiseIntensity", Shader.GetGlobalFloat("_HorrorNoiseIntensity"));
                _noise.SetFloat("_HorrorDistortionIntensity", Shader.GetGlobalFloat("_HorrorDistortionIntensity"));
                _noise.SetFloat("_HorrorChromaticAberrationIntensity", Shader.GetGlobalFloat("_HorrorChromaticAberrationIntensity"));
                _noise.SetFloat("_HorrorScanlineIntensity", Shader.GetGlobalFloat("_HorrorScanlineIntensity"));
                HDUtils.BlitCameraTexture(ctx.cmd, _scratch, ctx.cameraColorBuffer, _noise, 0);
            }
            if (!State.IsActive || _death == null) return;
            HDUtils.BlitCameraTexture(ctx.cmd, ctx.cameraColorBuffer, _scratch);
            _death.SetFloat("_Progress", State.Progress);
            _death.SetFloat("_EdgeWidth", State.EdgeWidth);
            _death.SetFloat("_NoiseStrength", State.NoiseStrength);
            _death.SetFloat("_CellIntensity", State.CellIntensity);
            _death.SetColor("_CoverColor", State.CoverColor);
            _death.SetColor("_MembraneColor", State.MembraneColor);
            _death.SetColor("_HotEdgeColor", State.HotEdgeColor);
            HDUtils.BlitCameraTexture(ctx.cmd, _scratch, ctx.cameraColorBuffer, _death, 0);
        }

        /// <summary>Release camera-sized scratch storage and engine materials</summary>
        protected override void Cleanup()
        {
            _scratch?.Release();
            CoreUtils.Destroy(_noise);
            CoreUtils.Destroy(_death);
        }
    }
}
