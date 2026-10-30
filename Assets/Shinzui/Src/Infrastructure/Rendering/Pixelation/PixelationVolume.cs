using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.Infrastructure.Rendering
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(HDRenderPipelineAsset))]
    [VolumeComponentMenu("Shinzui Custom/Pixelation")]
    public class PixelationVolume : CustomPostProcessVolumeComponent, IPostProcessComponent
    {

        public ClampedFloatParameter intensity = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

        private Material _material;

        /// <summary>
        /// 色調補正後の画面に演出を適用する位置
        /// </summary>
        public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

        /// <summary>
        /// 描画用マテリアルと演出の有効状態を確認する
        /// </summary>
        /// <returns>描画が必要な場合はtrue</returns>
        public bool IsActive() => _material != null && active && intensity.value > 0f;

        /// <summary>
        /// フルスクリーン描画用マテリアルを生成する
        /// </summary>
        public override void Setup()
        {
            // ビルドにも含めた専用シェーダーからマテリアルを生成する
            Shader shader = Shader.Find("Custom/Pixelation");
            if (shader != null) _material = CoreUtils.CreateEngineMaterial(shader);
        }

        /// <summary>
        /// 入力画像へ演出パラメータを適用して出力先へ描画する
        /// </summary>
        /// <param name="cmd">描画コマンド</param>
        /// <param name="camera">描画対象カメラ</param>
        /// <param name="source">入力画像</param>
        /// <param name="destination">出力画像</param>
        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            // 補間済みパラメータと入力画像をシェーダーへ渡す
            _material.SetTexture("_InputTexture", source);
            _material.SetFloat("_Intensity", intensity.value);

            HDUtils.DrawFullScreen(cmd, _material, destination);
        }

        /// <summary>
        /// パイプラインの終了時に描画用マテリアルを解放する
        /// </summary>
        public override void Cleanup()
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }
    }
}
