using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering.Scanline
{
    [VolumeComponentMenu("Shinzui Custom/Scanline")]
    [VolumeRequiresRendererFeatures(typeof(ScanlineRendererFeature))]
    public class ScanlineVolume : VolumeComponent, IPostProcessComponent
    {
        public bool IsActive() => active;

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
    }
}
