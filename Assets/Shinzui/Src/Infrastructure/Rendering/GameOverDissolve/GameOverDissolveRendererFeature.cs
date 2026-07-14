using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering.GameOverDissolve
{
    [System.Serializable]
    public class GameOverDissolveSettings
    {
        public Shader shader;
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public class GameOverDissolveRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private GameOverDissolveSettings settings = new();

        private const string ShaderName = "Custom/GameOverDissolve";
        private GameOverDissolvePass _pass;

        private GameOverDissolvePass CreatePass(GameOverDissolveSettings settings)
        {
            if (settings.shader == null)
            {
                settings.shader = Shader.Find(ShaderName);
            }

            return new GameOverDissolvePass(settings.shader, settings.injectionPoint);
        }

        public override void Create()
        {
            _pass = CreatePass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.isSceneViewCamera)
            {
                return;
            }

            var volume = VolumeManager.instance.stack.GetComponent<GameOverDissolveVolume>();
            if ((volume == null || !volume.IsActive()) && !GameOverDissolveRuntimeState.IsActive)
            {
                return;
            }

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
