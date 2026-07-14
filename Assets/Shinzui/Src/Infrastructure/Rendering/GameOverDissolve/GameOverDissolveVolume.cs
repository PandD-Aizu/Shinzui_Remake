using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering.GameOverDissolve
{
    [VolumeComponentMenu("Shinzui Custom/Game Over Dissolve")]
    [VolumeRequiresRendererFeatures(typeof(GameOverDissolveRendererFeature))]
    public class GameOverDissolveVolume : VolumeComponent, IPostProcessComponent
    {
        public bool IsActive() => active && progress.value > 0.001f;

        [Header("Dissolve")]
        public ClampedFloatParameter progress = new(0.0f, 0.0f, 1.0f);
        public ClampedFloatParameter edgeWidth = new(0.11f, 0.01f, 0.35f);
        public ClampedFloatParameter noiseStrength = new(0.24f, 0.0f, 0.65f);
        public ClampedFloatParameter cellIntensity = new(1.0f, 0.0f, 2.0f);

        [Header("Color")]
        public ColorParameter coverColor = new(new Color(0.0f, 0.0f, 0.01f, 1.0f));
        public ColorParameter membraneColor = new(new Color(0.12f, 0.55f, 0.72f, 1.0f));
        public ColorParameter hotEdgeColor = new(new Color(1.05f, 1.22f, 1.25f, 1.0f));
    }
}
