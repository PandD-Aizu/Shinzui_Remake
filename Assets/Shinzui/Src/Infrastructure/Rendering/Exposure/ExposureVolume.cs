using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.Infrastructure.Rendering.Exposure
{
    public enum ExposureMode
    {
        Fixed,
        Automatic,
        AutomaticHistogram
    }

    public enum EyeAdaptationMode
    {
        Instant,
        Progressive
    }

    [Serializable]
    public sealed class ExposureModeParameter : VolumeParameter<ExposureMode>
    {
        public ExposureModeParameter(ExposureMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    [Serializable]
    public sealed class EyeAdaptationModeParameter : VolumeParameter<EyeAdaptationMode>
    {
        public EyeAdaptationModeParameter(EyeAdaptationMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    [VolumeComponentMenu("Shinzui Custom/Exposure")]
    [VolumeRequiresRendererFeatures(typeof(ExposureRendererFeature))]
    public class ExposureVolume : VolumeComponent, IPostProcessComponent
    {
        public ExposureModeParameter mode = new ExposureModeParameter(ExposureMode.AutomaticHistogram);
        
        public Vector2Parameter filtering = new Vector2Parameter(new Vector2(90.0f, 99.0f));
        
        public FloatParameter minLuminance = new FloatParameter(-3.0f); // EV100の最小値
        public FloatParameter maxLuminance = new FloatParameter(-1.6f);  // EV100の最大値
        
        public FloatParameter exposureCompensation = new FloatParameter(1.0f); // EV補正値
        
        public EyeAdaptationModeParameter eyeAdaptation = new EyeAdaptationModeParameter(EyeAdaptationMode.Progressive);
        public MinFloatParameter speedUp = new MinFloatParameter(5.0f, 0.0f);
        public MinFloatParameter speedDown = new MinFloatParameter(5.0f, 0.0f);
        
        public FloatParameter portalExposureCompensation = new FloatParameter(1.0f);

        public bool IsActive() => active;
    }
}