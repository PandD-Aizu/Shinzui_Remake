using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering.Exposure
{
    public class ExposurePass : ScriptableRenderPass
    {
        private class ComputePassData
        {
            public BufferHandle HistogramBuffer;
            public BufferHandle ExposureResultBuffer;
            public TextureHandle SourceTex;
        }

        private class PassData
        {
            public TextureHandle Src;
            public TextureHandle Dst;
            public Material Material;
            public BufferHandle ExposureResultBuffer;
        }

        private string PassName => "Exposure";
        private Material _material;
        private ComputeShader _computeShader;
        private GraphicsBuffer _exposureBuffer;

        private int _kernelClear;
        private int _kernelGenerate;
        private int _kernelCompute;

        public ExposurePass(Shader shader, ComputeShader computeShader, RenderPassEvent evt)
        {
            renderPassEvent = evt;
            if (shader != null)
                _material = CoreUtils.CreateEngineMaterial(shader);
            
            _computeShader = computeShader;
            if (_computeShader != null)
            {
                _kernelClear = _computeShader.FindKernel("KClearHistogram");
                _kernelGenerate = _computeShader.FindKernel("KGenerateHistogram");
                _kernelCompute = _computeShader.FindKernel("KComputeExposure");
            }
        }

        public void Setup(GraphicsBuffer exposureBuffer)
        {
            _exposureBuffer = exposureBuffer;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Volumeを取得
            var stack = VolumeManager.instance.stack;
            var volume = stack.GetComponent<ExposureVolume>();
            if (volume == null || !volume.IsActive() || _material == null || _computeShader == null || _exposureBuffer == null)
                return;
            
            // 必要な情報を ContextContainer から取得
            var resources = frameData.Get<UniversalResourceData>();
            var src = resources.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(src);
            var dst = renderGraph.CreateTexture(desc);

            // 一時ヒストグラムバッファの作成 (256ビン, uint)
            var histogramDesc = new BufferDesc(256 * sizeof(uint), sizeof(uint), GraphicsBuffer.Target.Structured);
            BufferHandle histogramBuffer = renderGraph.CreateBuffer(histogramDesc);

            // 永続露出バッファをRenderGraphにインポート
            BufferHandle exposureResultBuffer = renderGraph.ImportBuffer(_exposureBuffer);

            int width = desc.width;
            int height = desc.height;

            // 1. Compute Pass: 露出計算
            using (var builder = renderGraph.AddComputePass<ComputePassData>("Compute Exposure", out var data))
            {
                data.HistogramBuffer = histogramBuffer;
                data.ExposureResultBuffer = exposureResultBuffer;
                data.SourceTex = src;

                builder.UseBuffer(data.HistogramBuffer, AccessFlags.Write);
                builder.UseBuffer(data.ExposureResultBuffer, AccessFlags.ReadWrite);
                builder.UseTexture(data.SourceTex, AccessFlags.Read);

                builder.SetRenderFunc((ComputePassData passData, ComputeGraphContext ctx) =>
                {
                    // ヒストグラムバッファの初期化
                    ctx.cmd.SetComputeBufferParam(_computeShader, _kernelClear, "_HistogramBuffer", passData.HistogramBuffer);
                    ctx.cmd.DispatchCompute(_computeShader, _kernelClear, 1, 1, 1);

                    // ヒストグラムの生成
                    ctx.cmd.SetComputeTextureParam(_computeShader, _kernelGenerate, "_SourceTex", passData.SourceTex);
                    ctx.cmd.SetComputeBufferParam(_computeShader, _kernelGenerate, "_HistogramBuffer", passData.HistogramBuffer);
                    
                    int threadGroupsX = Mathf.CeilToInt((float)width / 16.0f);
                    int threadGroupsY = Mathf.CeilToInt((float)height / 16.0f);
                    ctx.cmd.DispatchCompute(_computeShader, _kernelGenerate, threadGroupsX, threadGroupsY, 1);

                    // 露出値の算出と目の順応適用
                    ctx.cmd.SetComputeBufferParam(_computeShader, _kernelCompute, "_HistogramBuffer", passData.HistogramBuffer);
                    ctx.cmd.SetComputeBufferParam(_computeShader, _kernelCompute, "_ExposureResultBuffer", passData.ExposureResultBuffer);
                    
                    ctx.cmd.SetComputeIntParam(_computeShader, "_Mode", (int)volume.mode.value);
                    ctx.cmd.SetComputeIntParam(_computeShader, "_AdaptationMode", (int)volume.eyeAdaptation.value);
                    ctx.cmd.SetComputeFloatParam(_computeShader, "_FixedExposure", volume.exposureCompensation.value);
                    ctx.cmd.SetComputeFloatParam(_computeShader, "_MinLogLuminance", volume.minLuminance.value);
                    ctx.cmd.SetComputeFloatParam(_computeShader, "_MaxLogLuminance", volume.maxLuminance.value);
                    ctx.cmd.SetComputeFloatParam(_computeShader, "_LowPercentile", volume.filtering.value.x / 100.0f);
                    ctx.cmd.SetComputeFloatParam(_computeShader, "_HighPercentile", volume.filtering.value.y / 100.0f);
                    ctx.cmd.SetComputeFloatParam(_computeShader, "_ExposureCompensation", volume.exposureCompensation.value);
                    ctx.cmd.SetComputeFloatParam(_computeShader, "_AdaptationSpeedUp", volume.speedUp.value);
                    ctx.cmd.SetComputeFloatParam(_computeShader, "_AdaptationSpeedDown", volume.speedDown.value);
                    ctx.cmd.SetComputeFloatParam(_computeShader, "_DeltaTime", Time.deltaTime);

                    ctx.cmd.DispatchCompute(_computeShader, _kernelCompute, 1, 1, 1);
                });
            }

            // 2. Raster Pass: 露出適用のBlit
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var data))
            {
                data.Material = _material;
                data.Src = src;
                data.Dst = dst;
                data.ExposureResultBuffer = exposureResultBuffer;

                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
                builder.UseBuffer(exposureResultBuffer, AccessFlags.Read);
                
                builder.AllowGlobalStateModification(true);
                
                builder.SetRenderFunc((PassData passData, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalBuffer("_HDRPExposureBufferGlobal", passData.ExposureResultBuffer);
                    ctx.cmd.SetGlobalInt("_Mode", (int)volume.mode.value);
                    ctx.cmd.SetGlobalFloat("_FixedExposureMultiplier", volume.exposureCompensation.value);
                    
                    Blitter.BlitTexture(ctx.cmd, passData.Src, new Vector4(1, 1, 0, 0), passData.Material, 0);
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