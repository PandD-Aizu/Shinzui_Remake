using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering.Exposure
{
    [System.Serializable]
    public class Settings
    {
        public Shader shader;
        public ComputeShader computeShader;
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
    }
    
    public class ExposureRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Settings settings = new Settings();
        
        private string _shaderName = "Custom/Exposure";
        private ExposurePass _pass;
        private GraphicsBuffer _exposureBuffer;

        /// <summary>
        /// バッファの初期化
        /// </summary>
        private void InitBuffer()
        {
            if (_exposureBuffer == null)
            {
                _exposureBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, sizeof(float));
                // 初期値 1.0f (露出倍率1倍) で初期化
                float[] initialData = new float[] { 1.0f, 1.0f };
                _exposureBuffer.SetData(initialData);
            }
        }

        /// <summary>
        /// バッファの解放
        /// </summary>
        private void ReleaseBuffer()
        {
            if (_exposureBuffer != null)
            {
                _exposureBuffer.Release();
                _exposureBuffer = null;
            }
        }

        /// <summary>
        /// パスの作成
        /// </summary>
        private ExposurePass CreatePass(Settings settings)
        {
            if (settings.shader == null)
                settings.shader = Shader.Find(_shaderName);
            
            return new ExposurePass(settings.shader, settings.computeShader, settings.injectionPoint);
        }

        /// <summary>
        /// レンダラー機能の初期化
        /// </summary>
        public override void Create()
        {
            _pass = CreatePass(settings);
        }

        /// <summary>
        /// レンダリングパイプラインにパスを追加
        /// </summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // シーンビューカメラではエフェクトを適用しない
            if (renderingData.cameraData.isSceneViewCamera)
                return;
            
            // Volumeを取得
            var volume = VolumeManager.instance.stack.GetComponent<ExposureVolume>();
            if (volume == null || !volume.IsActive())
                return;

            InitBuffer();
            _pass.Setup(_exposureBuffer);
            
            // パスをレンダラーに追加
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            ReleaseBuffer();
            if (_pass != null)
            {
                _pass.Dispose();
                _pass = null;
            }
        }
        
        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (_pass != null)
            {
                _pass.Dispose();
                _pass = null;
            }
            ReleaseBuffer();

            Create();
        }
        #endif
    }
}