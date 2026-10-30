using System;
using UnityEngine;
using UnityEngine.Rendering;

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
        /// <summary>
        /// 旧プロファイルの列挙値を読み込む
        /// </summary>
        /// <param name="value">保存済みの値</param>
        /// <param name="overrideState">上書き状態</param>
        public ExposureModeParameter(ExposureMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    [Serializable]
    public sealed class EyeAdaptationModeParameter : VolumeParameter<EyeAdaptationMode>
    {
        /// <summary>
        /// 旧プロファイルの列挙値を読み込む
        /// </summary>
        /// <param name="value">保存済みの値</param>
        /// <param name="overrideState">上書き状態</param>
        public EyeAdaptationModeParameter(EyeAdaptationMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    /// <summary>
    /// 旧露出パラメータをHDRPへ移行するための読み込み専用データ
    /// </summary>
    [VolumeComponentMenu("Shinzui Migration/Legacy Exposure Data")]
    public class ExposureVolume : VolumeComponent
    {
        public ExposureModeParameter mode = new ExposureModeParameter(ExposureMode.AutomaticHistogram);
        
        public Vector2Parameter filtering = new Vector2Parameter(new Vector2(90.0f, 99.0f));
        
        public FloatParameter minLuminance = new FloatParameter(-3.0f); // EV100の最小値
        public FloatParameter maxLuminance = new FloatParameter(-1.6f); // EV100の最大値
        
        public FloatParameter exposureCompensation = new FloatParameter(1.0f); // 自動露出のEV補正または固定露出の倍率
        
        public EyeAdaptationModeParameter eyeAdaptation = new EyeAdaptationModeParameter(EyeAdaptationMode.Progressive);
        public MinFloatParameter speedUp = new MinFloatParameter(5.0f, 0.0f);
        public MinFloatParameter speedDown = new MinFloatParameter(5.0f, 0.0f);
        
        // 旧描画パスでは未使用の値を移行確認用に保持する
        public FloatParameter portalExposureCompensation = new FloatParameter(1.0f);

    }
}