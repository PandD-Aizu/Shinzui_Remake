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

#if UNITY_EDITOR
        /* エディター検証用の値はプレイヤービルドへ含めない */
        public static float PreviewIntensity { get; set; }
        public static int LastExecutionFrame { get; private set; } = -1;
        public static int LastDrawFrame { get; private set; } = -1;
        public static string LastCameraName { get; private set; }
        public static float LastIntensity { get; private set; }
#endif

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
            float intensity = Shader.GetGlobalFloat("_HorrorNoiseIntensity");
#if UNITY_EDITOR
            LastExecutionFrame = Time.frameCount;
            LastCameraName = camera.name;
            intensity = Mathf.Max(intensity, PreviewIntensity);
            LastIntensity = intensity;
#endif
            if (intensity <= 0) return;
            /* 他のカメラの更新から値を隔離して描画コマンドへ渡す */
            ctx.propertyBlock.SetFloat("_HorrorNoiseIntensity", intensity);
            CustomPassUtils.Copy(ctx, ctx.cameraColorBuffer, input);
            ctx.propertyBlock.SetTexture("_HorrorInput", input.rt);
            ctx.propertyBlock.SetVector("_HorrorInputScale", input.rtHandleProperties.rtHandleScale);
            CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ClearFlag.None);
            CoreUtils.DrawFullScreen(ctx.cmd, material, ctx.propertyBlock, shaderPassId: 0);
#if UNITY_EDITOR
            LastDrawFrame = Time.frameCount;
#endif
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
            if (prefab == null)
            {
                Debug.LogError("HDRP HorrorDetection missing Resources/HdrpHorrorDetection.prefab; run HDRP Migration/8 Tune Gameplay Rendering");
                return;
            }
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = name;
            UnityEngine.Object.DontDestroyOnLoad(instance);
            Debug.Log("HDRP HorrorDetection installed: Resources/HdrpHorrorDetection.prefab, AfterPostProcess");
        }
    }
}
