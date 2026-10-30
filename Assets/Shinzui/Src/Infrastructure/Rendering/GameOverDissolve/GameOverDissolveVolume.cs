using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.Infrastructure.Rendering.GameOverDissolve
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(HDRenderPipelineAsset))]
    [VolumeComponentMenu("Shinzui Custom/Game Over Dissolve")]
    public class GameOverDissolveVolume : CustomPostProcessVolumeComponent, IPostProcessComponent
    {

        [Header("Dissolve")]
        public ClampedFloatParameter progress = new(0.0f, 0.0f, 1.0f);
        public ClampedFloatParameter edgeWidth = new(0.11f, 0.01f, 0.35f);
        public ClampedFloatParameter noiseStrength = new(0.24f, 0.0f, 0.65f);
        public ClampedFloatParameter cellIntensity = new(1.0f, 0.0f, 2.0f);

        [Header("Color")]
        public ColorParameter coverColor = new(new Color(0.0f, 0.0f, 0.01f, 1.0f));
        public ColorParameter membraneColor = new(new Color(0.12f, 0.55f, 0.72f, 1.0f));
        public ColorParameter hotEdgeColor = new(new Color(1.05f, 1.22f, 1.25f, 1.0f));

        private Material _material;

        /// <summary>
        /// 色調補正後の画面に演出を適用する位置
        /// </summary>
        public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

        /// <summary>
        /// 描画用マテリアルと演出の有効状態を確認する
        /// </summary>
        /// <returns>描画が必要な場合はtrue</returns>
        public bool IsActive() => _material != null && active && (progress.value > 0.001f || GameOverDissolveRuntimeState.IsActive);

        /// <summary>
        /// フルスクリーン描画用マテリアルを生成する
        /// </summary>
        public override void Setup()
        {
            // ビルドにも含めた専用シェーダーからマテリアルを生成する
            Shader shader = Shader.Find("Custom/GameOverDissolve");
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
            bool volumeActive = progress.value > 0.001f;
            _material.SetFloat("_Progress", volumeActive ? progress.value : GameOverDissolveRuntimeState.Progress);
            _material.SetFloat("_EdgeWidth", volumeActive ? edgeWidth.value : GameOverDissolveRuntimeState.EdgeWidth);
            _material.SetFloat("_NoiseStrength", volumeActive ? noiseStrength.value : GameOverDissolveRuntimeState.NoiseStrength);
            _material.SetFloat("_CellIntensity", volumeActive ? cellIntensity.value : GameOverDissolveRuntimeState.CellIntensity);
            _material.SetColor("_CoverColor", volumeActive ? coverColor.value : GameOverDissolveRuntimeState.CoverColor);
            _material.SetColor("_MembraneColor", volumeActive ? membraneColor.value : GameOverDissolveRuntimeState.MembraneColor);
            _material.SetColor("_HotEdgeColor", volumeActive ? hotEdgeColor.value : GameOverDissolveRuntimeState.HotEdgeColor);

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
