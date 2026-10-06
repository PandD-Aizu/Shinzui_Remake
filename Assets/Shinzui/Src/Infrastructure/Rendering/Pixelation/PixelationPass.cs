using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering
{
    public class PixelationPass : ScriptableRenderPass
    {
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int OutputSize = Shader.PropertyToID("_PixelationOutputSize");
        private static readonly int SourceSize = Shader.PropertyToID("_PixelationSourceSize");
        private static readonly int GridSize = Shader.PropertyToID("_PixelationGridSize");
        private static readonly int PixelSize = Shader.PropertyToID("_PixelationPixelSize");

        private string PassName => "Pixelation";
        private Material _material;

        /// <summary>
        /// ピクセル化用のマテリアルと入力カラーの中間テクスチャを準備
        /// </summary>
        /// <param name="shader">ピクセル化シェーダー</param>
        /// <param name="evt">エフェクトの実行タイミング</param>
        public PixelationPass(Shader shader, RenderPassEvent evt)
        {
            renderPassEvent = evt;
            requiresIntermediateTexture = true;
            if (shader != null)
                _material = CoreUtils.CreateEngineMaterial(shader);
        }

        /// <summary>
        /// RenderGraphを使用してピクセル化エフェクトを適用するパスを記録
        /// </summary>
        /// <param name="renderGraph">描画パスを記録するRenderGraph</param>
        /// <param name="frameData">現在のカメラと描画リソース</param>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Volumeを取得
            var stack = VolumeManager.instance.stack;
            var volume = stack.GetComponent<PixelationVolume>();
            if (volume == null || !volume.IsActive() || _material == null)
                return;
            
            // 必要な情報をContextContainerから取得
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
                return;

            var src = resources.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(src);
            desc.name = PassName;
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples = MSAASamples.None;
            desc.bindTextureMS = false;
            desc.clearBuffer = false;

            // ULTRAでは高解像度の全サンプルを平均してから最終解像度へ展開
            if (volume.highQuality.value)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                resources.cameraColor = RecordHighQuality(renderGraph, src, desc, cameraData.camera, volume.intensity.value);
                return;
            }

            // 既存の品質設定では中心サンプルとハイライト保護を維持
            var dst = renderGraph.CreateTexture(desc);
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(Intensity, volume.intensity.value);
            var parameters = new RenderGraphUtils.BlitMaterialParameters(src, dst, _material, 0)
            {
                propertyBlock = properties
            };
            renderGraph.AddBlitPass(parameters, PassName);
            resources.cameraColor = dst;
        }

        /// <summary>
        /// 高解像度入力を面積平均し、一定サイズのピクセルへ展開
        /// </summary>
        /// <param name="renderGraph">描画パスを記録するRenderGraph</param>
        /// <param name="source">ポストプロセス後のカラー</param>
        /// <param name="descriptor">入力カラーから取得したテクスチャ設定</param>
        /// <param name="camera">出力解像度を取得するカメラ</param>
        /// <param name="intensity">ピクセル化の強度</param>
        /// <returns>最終解像度のピクセル化済みカラー</returns>
        private TextureHandle RecordHighQuality(RenderGraph renderGraph, TextureHandle source, TextureDesc descriptor, Camera camera, float intensity)
        {
            // レンダースケールに依存しない整数サイズのピクセル格子を作成
            int width = Mathf.Max(1, camera.pixelWidth);
            int height = Mathf.Max(1, camera.pixelHeight);
            int pixelSize = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1.0f, 64.0f, intensity)));
            int columns = Mathf.CeilToInt((float)width / pixelSize);
            int rows = Mathf.CeilToInt((float)height / pixelSize);
            var properties = new MaterialPropertyBlock();
            properties.SetVector(OutputSize, new Vector4(width, height, 1.0f / width, 1.0f / height));
            properties.SetVector(SourceSize, new Vector4(descriptor.width, descriptor.height, 1.0f / descriptor.width, 1.0f / descriptor.height));
            properties.SetVector(GridSize, new Vector4(columns, rows, 1.0f / columns, 1.0f / rows));
            properties.SetFloat(PixelSize, pixelSize);

            // 各ブロックが覆う入力テクセルを、端の部分被覆も含めて平均
            descriptor.sizeMode = TextureSizeMode.Explicit;
            descriptor.useDynamicScale = false;
            descriptor.useDynamicScaleExplicit = false;
            descriptor.filterMode = FilterMode.Point;
            descriptor.width = columns;
            descriptor.height = rows;
            descriptor.name = "Pixelation Block Average";
            var blocks = renderGraph.CreateTexture(descriptor);
            var averageParameters = new RenderGraphUtils.BlitMaterialParameters(source, blocks, _material, 1)
            {
                propertyBlock = properties
            };
            renderGraph.AddBlitPass(averageParameters, descriptor.name);

            // 平均したブロックを点サンプリングで展開し、後段の補間による輪郭のぼけを防止
            descriptor.width = width;
            descriptor.height = height;
            descriptor.name = "Pixelation Output";
            var destination = renderGraph.CreateTexture(descriptor);
            var expandParameters = new RenderGraphUtils.BlitMaterialParameters(blocks, destination, _material, 2)
            {
                propertyBlock = properties
            };
            renderGraph.AddBlitPass(expandParameters, descriptor.name);
            return destination;
        }

        /// <summary>
        /// ピクセル化用のマテリアルを破棄
        /// </summary>
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
