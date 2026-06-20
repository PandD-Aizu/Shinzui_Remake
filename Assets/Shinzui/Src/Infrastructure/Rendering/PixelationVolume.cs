using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering
{
    [VolumeComponentMenu("Shinzui Custom/Pixelation")]
    [VolumeRequiresRendererFeatures(typeof(PixelationRendererFeature))]
    public class PixelationVolume : VolumeComponent, IPostProcessComponent
    {
        public bool IsActive() => active;

        public ClampedFloatParameter intensity = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);
    }
}