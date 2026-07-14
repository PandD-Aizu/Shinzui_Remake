using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering.GameOverDissolve
{
    public class GameOverDissolvePass : ScriptableRenderPass
    {
        private class PassData
        {
            public TextureHandle Src;
            public TextureHandle Dst;
            public Material Material;
            public float Progress;
            public float EdgeWidth;
            public float NoiseStrength;
            public float CellIntensity;
            public Color CoverColor;
            public Color MembraneColor;
            public Color HotEdgeColor;
        }

        private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        private static readonly int Progress = Shader.PropertyToID("_Progress");
        private static readonly int EdgeWidth = Shader.PropertyToID("_EdgeWidth");
        private static readonly int NoiseStrength = Shader.PropertyToID("_NoiseStrength");
        private static readonly int CellIntensity = Shader.PropertyToID("_CellIntensity");
        private static readonly int CoverColor = Shader.PropertyToID("_CoverColor");
        private static readonly int MembraneColor = Shader.PropertyToID("_MembraneColor");
        private static readonly int HotEdgeColor = Shader.PropertyToID("_HotEdgeColor");

        private const string PassName = "Game Over Dissolve";
        private Material _material;

        public GameOverDissolvePass(Shader shader, RenderPassEvent evt)
        {
            renderPassEvent = evt;
            if (shader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(shader);
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var volume = VolumeManager.instance.stack.GetComponent<GameOverDissolveVolume>();
            bool volumeActive = volume != null && volume.IsActive();
            bool runtimeActive = GameOverDissolveRuntimeState.IsActive;

            if ((!volumeActive && !runtimeActive) || _material == null)
            {
                return;
            }

            var resources = frameData.Get<UniversalResourceData>();
            var src = resources.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(src);
            var dst = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var data))
            {
                data.Material = _material;
                data.Src = src;
                data.Dst = dst;
                data.Progress = volumeActive ? volume.progress.value : GameOverDissolveRuntimeState.Progress;
                data.EdgeWidth = volumeActive ? volume.edgeWidth.value : GameOverDissolveRuntimeState.EdgeWidth;
                data.NoiseStrength = volumeActive ? volume.noiseStrength.value : GameOverDissolveRuntimeState.NoiseStrength;
                data.CellIntensity = volumeActive ? volume.cellIntensity.value : GameOverDissolveRuntimeState.CellIntensity;
                data.CoverColor = volumeActive ? volume.coverColor.value : GameOverDissolveRuntimeState.CoverColor;
                data.MembraneColor = volumeActive ? volume.membraneColor.value : GameOverDissolveRuntimeState.MembraneColor;
                data.HotEdgeColor = volumeActive ? volume.hotEdgeColor.value : GameOverDissolveRuntimeState.HotEdgeColor;

                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData passData, RasterGraphContext ctx) =>
                {
                    passData.Material.SetFloat(Progress, passData.Progress);
                    passData.Material.SetFloat(EdgeWidth, passData.EdgeWidth);
                    passData.Material.SetFloat(NoiseStrength, passData.NoiseStrength);
                    passData.Material.SetFloat(CellIntensity, passData.CellIntensity);
                    passData.Material.SetColor(CoverColor, passData.CoverColor);
                    passData.Material.SetColor(MembraneColor, passData.MembraneColor);
                    passData.Material.SetColor(HotEdgeColor, passData.HotEdgeColor);
                    ctx.cmd.SetGlobalTexture(MainTexture, passData.Src);
                    CoreUtils.DrawFullScreen(ctx.cmd, passData.Material);
                });

                resources.cameraColor = dst;
            }
        }

        public void Dispose()
        {
            if (_material != null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }
        }
    }
}
