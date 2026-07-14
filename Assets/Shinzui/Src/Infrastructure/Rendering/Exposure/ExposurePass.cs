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
            public int Width;
            public int Height;
            public int Mode;
            public int AdaptationMode;
            public float FixedExposure;
            public float MinLuminance;
            public float MaxLuminance;
            public float LowPercentile;
            public float HighPercentile;
            public float ExposureCompensation;
            public float AdaptationSpeedUp;
            public float AdaptationSpeedDown;
            public float DeltaTime;
        }

        private class PassData
        {
            public TextureHandle Src;
            public TextureHandle Dst;
            public Material Material;
            public BufferHandle ExposureResultBuffer;
            public int Mode;
            public float FixedExposureMultiplier;
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

            // 現在レンダリングしているカメラ情報を取得
            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;
            
            // ポータル用カメラであるか判定
            bool isPortalCamera = camera != null && camera.name.Contains("Portal");

            // 必要な情報を ContextContainer から取得
            var resources = frameData.Get<UniversalResourceData>();
            var src = resources.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(src);
            var dst = renderGraph.CreateTexture(desc);

            // 永続露出バッファをRenderGraphにインポート
            BufferHandle exposureResultBuffer = renderGraph.ImportBuffer(_exposureBuffer);

            // ポータル用カメラではない場合のみ、露出バッファの更新処理（Compute Pass）を行う
            if (!isPortalCamera)
            {
                // 一時ヒストグラムバッファの作成 (256ビン, uint)
                var histogramDesc = new BufferDesc(256, sizeof(uint), GraphicsBuffer.Target.Structured);
                BufferHandle histogramBuffer = renderGraph.CreateBuffer(histogramDesc);

                int width = desc.width;
                int height = desc.height;

                // Compute Pass: 露出計算
                using (var builder = renderGraph.AddComputePass<ComputePassData>("Compute Exposure", out var data))
                {
                    data.HistogramBuffer = histogramBuffer;
                    data.ExposureResultBuffer = exposureResultBuffer;
                    data.SourceTex = src;
                    data.Width = width;
                    data.Height = height;
                    data.Mode = (int)volume.mode.value;
                    data.AdaptationMode = (int)volume.eyeAdaptation.value;
                    data.FixedExposure = volume.exposureCompensation.value;
                    data.MinLuminance = volume.minLuminance.value;
                    data.MaxLuminance = volume.maxLuminance.value;
                    data.LowPercentile = volume.filtering.value.x / 100.0f;
                    data.HighPercentile = volume.filtering.value.y / 100.0f;
                    data.ExposureCompensation = volume.exposureCompensation.value;
                    data.AdaptationSpeedUp = volume.speedUp.value;
                    data.AdaptationSpeedDown = volume.speedDown.value;
                    data.DeltaTime = Time.deltaTime;

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
                        
                        int threadGroupsX = Mathf.CeilToInt((float)passData.Width / 16.0f);
                        int threadGroupsY = Mathf.CeilToInt((float)passData.Height / 16.0f);
                        ctx.cmd.DispatchCompute(_computeShader, _kernelGenerate, threadGroupsX, threadGroupsY, 1);

                        // 露出値の算出と目の順応適用
                        ctx.cmd.SetComputeBufferParam(_computeShader, _kernelCompute, "_HistogramBuffer", passData.HistogramBuffer);
                        ctx.cmd.SetComputeBufferParam(_computeShader, _kernelCompute, "_ExposureResultBuffer", passData.ExposureResultBuffer);
                        
                        ctx.cmd.SetComputeIntParam(_computeShader, "_Mode", passData.Mode);
                        ctx.cmd.SetComputeIntParam(_computeShader, "_AdaptationMode", passData.AdaptationMode);
                        ctx.cmd.SetComputeFloatParam(_computeShader, "_FixedExposure", passData.FixedExposure);
                        ctx.cmd.SetComputeFloatParam(_computeShader, "_MinLogLuminance", passData.MinLuminance);
                        ctx.cmd.SetComputeFloatParam(_computeShader, "_MaxLogLuminance", passData.MaxLuminance);
                        ctx.cmd.SetComputeFloatParam(_computeShader, "_LowPercentile", passData.LowPercentile);
                        ctx.cmd.SetComputeFloatParam(_computeShader, "_HighPercentile", passData.HighPercentile);
                        ctx.cmd.SetComputeFloatParam(_computeShader, "_ExposureCompensation", passData.ExposureCompensation);
                        ctx.cmd.SetComputeFloatParam(_computeShader, "_AdaptationSpeedUp", passData.AdaptationSpeedUp);
                        ctx.cmd.SetComputeFloatParam(_computeShader, "_AdaptationSpeedDown", passData.AdaptationSpeedDown);
                        ctx.cmd.SetComputeFloatParam(_computeShader, "_DeltaTime", passData.DeltaTime);

                        ctx.cmd.DispatchCompute(_computeShader, _kernelCompute, 1, 1, 1);
                    });
                }
            }

            // Raster Pass: 露出適用のBlit
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var data))
            {
                data.Material = _material;
                data.Src = src;
                data.Dst = dst;
                data.ExposureResultBuffer = exposureResultBuffer;
                data.Mode = isPortalCamera ? (int)ExposureMode.Fixed : (int)volume.mode.value;
                data.FixedExposureMultiplier = isPortalCamera
                    ? volume.portalExposureCompensation.value
                    : volume.exposureCompensation.value;

                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
                builder.UseBuffer(exposureResultBuffer, AccessFlags.Read);
                
                builder.AllowGlobalStateModification(true);
                
                builder.SetRenderFunc((PassData passData, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalBuffer("_HDRPExposureBufferGlobal", passData.ExposureResultBuffer);
                    ctx.cmd.SetGlobalInt("_Mode", passData.Mode);
                    ctx.cmd.SetGlobalFloat("_FixedExposureMultiplier", passData.FixedExposureMultiplier);
                    
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
