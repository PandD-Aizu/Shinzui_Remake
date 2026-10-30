using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.Infrastructure.Rendering
{
    /// <summary>読み取り用コピーを使って恐怖ノイズを最終画面へ合成する</summary>
    [Serializable]
    public sealed class HdrpHorrorDetectionPass : CustomPass
    {
        public Material material;
        private RTHandle input;

        /// <summary>入力用のHDRバッファを確保する</summary>
        /// <param name="renderContext">描画コンテキスト</param>
        /// <param name="cmd">コマンドバッファ</param>
        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            input = RTHandles.Alloc(Vector2.one, slices: TextureXR.slices, dimension: TextureXR.dimension,
                colorFormat: GraphicsFormat.R16G16B16A16_SFloat, useDynamicScale: true, name: "HorrorDetectionInput");
        }

        /// <summary>主画面だけへノイズを適用する</summary>
        /// <param name="ctx">HDRPの描画状態</param>
        protected override void Execute(CustomPassContext ctx)
        {
            /* ポータル画像に重ねて適用せず最終画面で一度だけ演出する */
            Camera camera = ctx.hdCamera.camera;
            if (material == null || camera.cameraType != CameraType.Game || camera.targetTexture != null) return;
            if (Shader.GetGlobalFloat("_HorrorNoiseIntensity") <= 0) return;
            CustomPassUtils.Copy(ctx, ctx.cameraColorBuffer, input);
            ctx.propertyBlock.SetTexture("_HorrorInput", input.rt);
            ctx.propertyBlock.SetVector("_HorrorInputScale", input.rtHandleProperties.rtHandleScale);
            CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ClearFlag.None);
            CoreUtils.DrawFullScreen(ctx.cmd, material, ctx.propertyBlock, shaderPassId: 0);
        }

        /// <summary>再構築前に入力バッファを解放する</summary>
        protected override void Cleanup()
        {
            input?.Release();
            input = null;
        }

        /// <summary>シーンの加算読込でも演出用Volumeを一つだけ用意する</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            /* Prefabの参照が材質とシェーダーをビルドへ含める */
            const string name = "HDRP Horror Detection";
            if (GameObject.Find(name) != null) return;
            var prefab = Resources.Load<GameObject>("HdrpHorrorDetection");
            if (prefab == null) return;
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = name;
            UnityEngine.Object.DontDestroyOnLoad(instance);
        }
    }
}
