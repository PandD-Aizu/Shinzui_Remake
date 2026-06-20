using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering
{
    public class PixelationPass : ScriptableRenderPass
    {
        private class PassData
        {
            public TextureHandle Src;
            public TextureHandle Dst;
            public Material Material;
            public float Weight;
        }
        
        private static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");

        private string PassName => "Pixelation";
        private Material _material;

        public PixelationPass(Shader shader, RenderPassEvent evt)
        {
            renderPassEvent = evt;
            if (shader != null)
                _material = CoreUtils.CreateEngineMaterial(shader);
        }

        /// <summary>
        /// RenderGraphを使用してピクセル化エフェクトを適用するパスを記録
        /// </summary>
        /// <param name="renderGraph"></param>
        /// <param name="frameData"></param>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Volumeを取得
            var stack = VolumeManager.instance.stack;
            var volume = stack.GetComponent<PixelationVolume>();
            if (volume == null || !volume.IsActive() || _material == null)
                return;
            
            // 必要な情報をContextContainerから取得
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
                data.Weight = volume.intensity.value;
                
                // 入力と出力のリソースを設定
                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
                
                // グローバル状態の変更を許可
                builder.AllowGlobalStateModification(true);
                
                // レンダリング関数を定義
                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    data.Material.SetFloat(Intensity, data.Weight);
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