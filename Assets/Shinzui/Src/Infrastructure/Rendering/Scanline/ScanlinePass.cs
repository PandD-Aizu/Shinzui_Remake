using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering.Scanline
{
    public class ScanlinePass : ScriptableRenderPass
    {
        private class PassData
        {
            public TextureHandle Src;
            public TextureHandle Dst;
            public Material Material;
            public float Speed;
            public float BarSize;
            public float Strength;
            public float Frequency;
            public float Curvature;
            public float FineLines;
            public float FineLinesScale;
            public float Vignette;
            public float Flicker;
        }

        private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        private static readonly int Speed = Shader.PropertyToID("_Speed");
        private static readonly int BarSize = Shader.PropertyToID("_BarSize");
        private static readonly int Strength = Shader.PropertyToID("_Strength");
        private static readonly int Frequency = Shader.PropertyToID("_Frequency");
        private static readonly int Curvature = Shader.PropertyToID("_Curvature");
        private static readonly int FineLines = Shader.PropertyToID("_FineLines");
        private static readonly int FineLinesScale = Shader.PropertyToID("_FineLinesScale");
        private static readonly int Vignette = Shader.PropertyToID("_Vignette");
        private static readonly int Flicker = Shader.PropertyToID("_Flicker");
        
        private string PassName => "Scanline";
        private Material _material;

        public ScanlinePass(Shader shader, RenderPassEvent evt)
        {
            renderPassEvent = evt;
            if (shader != null)
                _material = CoreUtils.CreateEngineMaterial(shader);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Volumeを取得
            var stack = VolumeManager.instance.stack;
            var volume = stack.GetComponent<ScanlineVolume>();
            if (volume == null || !volume.IsActive() || _material == null)
                return;
            
            // 必要な情報を ContextContainer から取得
            var resources = frameData.Get<UniversalResourceData>();
            var src = resources.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(src);
            var dst = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var data))
            {
                // ラスターパス
                data.Material = _material;
                data.Src = src;
                data.Dst = dst;
                data.Speed = volume.speed.value;
                data.BarSize = volume.barSize.value;
                data.Strength = volume.strength.value;
                data.Frequency = volume.frequency.value;
                data.Curvature = volume.curvature.value;
                data.FineLines = volume.fineLines.value;
                data.FineLinesScale = volume.fineLinesScale.value;
                data.Vignette = volume.vignette.value;
                data.Flicker = volume.flicker.value;
                
                // 入出力のリソースを設定
                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
                
                // グローバルの状態の変更を許可
                builder.AllowGlobalStateModification(true);
                
                // レンダリング関数を定義
                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    data.Material.SetFloat(Speed, data.Speed);
                    data.Material.SetFloat(BarSize, data.BarSize);
                    data.Material.SetFloat(Strength, data.Strength);
                    data.Material.SetFloat(Frequency, data.Frequency);
                    data.Material.SetFloat(Curvature, data.Curvature);
                    data.Material.SetFloat(FineLines, data.FineLines);
                    data.Material.SetFloat(FineLinesScale, data.FineLinesScale);
                    data.Material.SetFloat(Vignette, data.Vignette);
                    data.Material.SetFloat(Flicker, data.Flicker);
                    ctx.cmd.SetGlobalTexture(MainTexture, data.Src);
                    CoreUtils.DrawFullScreen(ctx.cmd, data.Material);
                });
                
                // パスの完了後にカメラのカラーターゲットを更新
                resources.cameraColor = dst;
            }
        }

        public virtual void Dispose()
        {
            if (_material != null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }
        }
    }
}
