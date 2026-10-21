using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering
{
    [VolumeComponentMenu("Shinzui Custom/Pixelation")]
    [VolumeRequiresRendererFeatures(typeof(PixelationRendererFeature))]
    public class PixelationVolume : VolumeComponent, IPostProcessComponent
    {
        /// <summary>
        /// ピクセル化エフェクトの有効状態を取得
        /// </summary>
        /// <returns>Volumeが有効ならtrue</returns>
        public bool IsActive() => active;

        public ClampedFloatParameter intensity = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

        // 高解像度の描画結果をブロック全域で平均し、出力解像度を基準にピクセル化
        public BoolParameter highQuality = new BoolParameter(false);
    }
}
