using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.Infrastructure.Rendering.Scanline
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(HDRenderPipelineAsset))]
    [VolumeComponentMenu("Shinzui Custom/Scanline")]
    public class ScanlineVolume : CustomPostProcessVolumeComponent, IPostProcessComponent
    {

        [Header("Scanline Settings")]
        public ClampedFloatParameter speed = new (0.5f, 0.0f, 10.0f);
        public ClampedFloatParameter barSize = new (0.1f, 0.0f, 1.0f);
        public ClampedFloatParameter strength = new (1.0f, 0.0f, 10.0f);
        public ClampedFloatParameter frequency = new (30.0f, 0.0f, 100.0f);

        [Header("CRT Aesthetics (High Quality)")]
        public ClampedFloatParameter curvature = new (0.03f, 0.0f, 0.3f);     // 画面の湾曲度合い
        public ClampedFloatParameter fineLines = new (0.25f, 0.0f, 1.0f);     // 微細な走査線パターンの濃さ
        public ClampedFloatParameter fineLinesScale = new (480.0f, 50.0f, 2000.0f); // 走査線の細かさ（スケール）
        public ClampedFloatParameter vignette = new (0.3f, 0.0f, 1.0f);       // 画面端ビネット（暗さ）の強度
        public ClampedFloatParameter flicker = new (0.05f, 0.0f, 0.5f);       // アナログ画面特有の輝度チラつき

        private Material _material;

        /// <summary>
        /// 色調補正後の画面に演出を適用する位置
        /// </summary>
        public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

        /// <summary>
        /// 描画用マテリアルと演出の有効状態を確認する
        /// </summary>
        /// <returns>描画が必要な場合はtrue</returns>
        public bool IsActive() => _material != null && active
            && (strength.value > 0f || curvature.value > 0f || fineLines.value > 0f
                || vignette.value > 0f || flicker.value > 0f);

        /// <summary>
        /// フルスクリーン描画用マテリアルを生成する
        /// </summary>
        public override void Setup()
        {
            // ビルドにも含めた専用シェーダーからマテリアルを生成する
            Shader shader = Shader.Find("Custom/Scanline");
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
            _material.SetFloat("_Speed", speed.value);
            _material.SetFloat("_BarSize", barSize.value);
            _material.SetFloat("_Strength", strength.value);
            _material.SetFloat("_Frequency", frequency.value);
            _material.SetFloat("_Curvature", curvature.value);
            _material.SetFloat("_FineLines", fineLines.value);
            _material.SetFloat("_FineLinesScale", fineLinesScale.value);
            _material.SetFloat("_Vignette", vignette.value);
            _material.SetFloat("_Flicker", flicker.value);

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
