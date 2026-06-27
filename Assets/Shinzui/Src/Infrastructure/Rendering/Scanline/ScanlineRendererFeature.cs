using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering.Scanline
{
    [System.Serializable]
    public class Settings
    {
        public Shader shader;
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
    }
    
    public class ScanlineRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Settings settings = new Settings();

        private string _shaderName = "Custom/Scanline";
        private ScanlinePass _pass;

        /// <summary>
        /// パスの作成
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        private ScanlinePass CreatePass(Settings settings)
        {
            if (settings.shader == null)
                settings.shader = Shader.Find(_shaderName);
            
            return new ScanlinePass(settings.shader, settings.injectionPoint);
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
        /// <param name="renderer"></param>
        /// <param name="renderingData"></param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // シーンビューカメラではエフェクトを適用しない
            if (renderingData.cameraData.isSceneViewCamera)
                return;

            // Volumeを取得
            var volume = VolumeManager.instance.stack.GetComponent<ScanlineVolume>();
            if (volume == null || !volume.IsActive())
                return;
            
            // パスをレンダラーに追加
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
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

            Create();
        }
        #endif
    }
}
