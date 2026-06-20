using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering.InverseScanLine
{
    public class InverseScanlinePass : ScriptableRenderPass
    {
        private class PassData
        {
            public TextureHandle Src;
            public TextureHandle Dst;
            public Material Material;
            public float GlitchPeriod;
            public float GlitchSize;
            public float GlitchSpeed;
            public float GlitchProbability;
            public Vector2 BlockScale;
        }

        private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        private static readonly int GlitchPeriod = Shader.PropertyToID("_GlitchPeriod");
        private static readonly int GlitchSize = Shader.PropertyToID("_GlitchSize");
        private static readonly int GlitchSpeed = Shader.PropertyToID("_GlitchSpeed");
        private static readonly int GlitchProbability = Shader.PropertyToID("_GlitchProbability");
        private static readonly int _BlockScale = Shader.PropertyToID("_BlockScale");
        
        private string PassName => "InverseScanline";
        private Material _material;

        public InverseScanlinePass(Shader shader, RenderPassEvent evt)
        {
            renderPassEvent = evt;
            if (shader != null)
                _material = CoreUtils.CreateEngineMaterial(shader);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Volumeを取得
            var stack = VolumeManager.instance.stack;
            var volume = stack.GetComponent<InverseScanlineVolume>();
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
                data.GlitchPeriod = volume.glitchPeriod.value;
                data.GlitchSize = volume.glitchSize.value;
                data.GlitchSpeed = volume.glitchSpeed.value;
                data.GlitchProbability = volume.glitchProbability.value;
                data.BlockScale = volume.blockScale.value;
                
                // 入出力のリソースを設定
                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
                
                // グローバルの状態の変更を許可
                builder.AllowGlobalStateModification(true);
                
                // レンダリング関数を定義
                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    data.Material.SetFloat(GlitchPeriod, data.GlitchPeriod);
                    data.Material.SetFloat(GlitchSize, data.GlitchSize);
                    data.Material.SetFloat(GlitchSpeed, data.GlitchSpeed);
                    data.Material.SetFloat(GlitchProbability, data.GlitchProbability);
                    data.Material.SetVector(_BlockScale, data.BlockScale);
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