using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Video;

namespace Shinzui.Infrastructure.Rendering.InverseScanLine
{
    [VolumeComponentMenu("Shinzui Custom/InverseScanline")]
    [VolumeRequiresRendererFeatures(typeof(InverseScanlineRendererFeature))]
    public class InverseScanlineVolume : VolumeComponent, IPostProcessComponent
    {
        public bool IsActive() => active;

        public ClampedFloatParameter glitchPeriod = new (15.0f, 0.0f, 60.0f);
        public ClampedFloatParameter glitchSize = new (0.001f, 0.0f, 0.1f);
        public ClampedFloatParameter glitchSpeed = new (0.5f, 0.05f, 10.0f);
        public ClampedFloatParameter glitchProbability = new (0.1f, 0.0f, 1.0f);
        public Vector2Parameter blockScale = new (new Vector2(50.0f, 300.0f));
    }
}