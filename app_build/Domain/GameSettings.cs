using System;

namespace Shinzui.Domain.Settings
{
    /// <summary>
    /// すべての設定カテゴリーを統合するルートドメインモデル
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        public ControlsSettings Controls { get; set; } = new();
        public CameraSettings Camera { get; set; } = new();
        public GameplaySettings Gameplay { get; set; } = new();
        public GraphicsSettings Graphics { get; set; } = new();
        public AudioSettings Audio { get; set; } = new();
        public LanguageSettings Language { get; set; } = new();
        public AccessibilitySettings Accessibility { get; set; } = new();

        /// <summary>
        /// 全設定を初期値（デフォルト）にリセット
        /// </summary>
        public void ResetToDefault()
        {
            Controls = new ControlsSettings();
            Camera = new CameraSettings();
            Gameplay = new GameplaySettings();
            Graphics = new GraphicsSettings();
            Audio = new AudioSettings();
            Language = new LanguageSettings();
            Accessibility = new AccessibilitySettings();
        }

        /// <summary>
        /// 設定オブジェクトのディープコピーを作成
        /// </summary>
        public GameSettings Clone()
        {
            return new GameSettings
            {
                Controls = new ControlsSettings
                {
                    EnableVibration = Controls.EnableVibration,
                    ControllerLayoutType = Controls.ControllerLayoutType,
                    LockCursorToWindow = Controls.LockCursorToWindow,
                    InvertMouseClick = Controls.InvertMouseClick,
                    InvertMouseWheel = Controls.InvertMouseWheel
                },
                Camera = new CameraSettings
                {
                    NormalCameraInversion = Camera.NormalCameraInversion,
                    AimCameraInversion = Camera.AimCameraInversion,
                    MenuOperationInversion = Camera.MenuOperationInversion,
                    ControllerNormalSpeed = Camera.ControllerNormalSpeed,
                    ControllerAimSpeed = Camera.ControllerAimSpeed,
                    ControllerNormalRotationSpeed = Camera.ControllerNormalRotationSpeed,
                    ControllerAimRotationSpeed = Camera.ControllerAimRotationSpeed,
                    MouseNormalSensitivity = Camera.MouseNormalSensitivity,
                    MouseAimSensitivity = Camera.MouseAimSensitivity,
                    MouseMenuSensitivity = Camera.MouseMenuSensitivity
                },
                Gameplay = new GameplaySettings
                {
                    AimAssistStrength = Gameplay.AimAssistStrength,
                    EnableDamageExpression = Gameplay.EnableDamageExpression,
                    ShowTutorial = Gameplay.ShowTutorial,
                    ShowHUD = Gameplay.ShowHUD,
                    ShowReticle = Gameplay.ShowReticle,
                    ReticleColorIndex = Gameplay.ReticleColorIndex
                },
                Graphics = new GraphicsSettings
                {
                    ShowPerformanceMetrics = Graphics.ShowPerformanceMetrics,
                    DisplayAreaRatio = Graphics.DisplayAreaRatio,
                    BrightnessValue = Graphics.BrightnessValue,
                    EnableHDR = Graphics.EnableHDR,
                    ColorSpaceType = Graphics.ColorSpaceType,
                    ScreenMode = Graphics.ScreenMode,
                    Resolution = Graphics.Resolution,
                    RefreshRate = Graphics.RefreshRate,
                    FrameRateLimit = Graphics.FrameRateLimit,
                    EnableVSync = Graphics.EnableVSync,
                    FsrMode = Graphics.FsrMode,
                    RenderingPath = Graphics.RenderingPath,
                    ImageQualityScale = Graphics.ImageQualityScale,
                    EnableFidelityFxCas = Graphics.EnableFidelityFxCas,
                    AntiAliasingType = Graphics.AntiAliasingType,
                    EnableVrs = Graphics.EnableVrs,
                    TextureQuality = Graphics.TextureQuality,
                    TextureFilteringQuality = Graphics.TextureFilteringQuality,
                    MeshQuality = Graphics.MeshQuality,
                    EnableRayTracing = Graphics.EnableRayTracing,
                    GiAndReflectionQuality = Graphics.GiAndReflectionQuality,
                    ReflectionIntensity = Graphics.ReflectionIntensity,
                    EnableAo = Graphics.EnableAo,
                    EnableSsr = Graphics.EnableSsr,
                    VolumeLightQuality = Graphics.VolumeLightQuality,
                    EnableSss = Graphics.EnableSss,
                    ShadowQuality = Graphics.ShadowQuality,
                    EnableContactShadow = Graphics.EnableContactShadow,
                    EnableShadowCache = Graphics.EnableShadowCache,
                    EnableBloom = Graphics.EnableBloom,
                    EnableLensFlare = Graphics.EnableLensFlare,
                    EnableFilmGrain = Graphics.EnableFilmGrain,
                    EnableDepthOfField = Graphics.EnableDepthOfField,
                    EnableLensDistortion = Graphics.EnableLensDistortion
                },
                Audio = new AudioSettings
                {
                    VoiceVolume = Audio.VoiceVolume,
                    BgmVolume = Audio.BgmVolume,
                    SeVolume = Audio.SeVolume,
                    SystemVolume = Audio.SystemVolume,
                    EnableDynamicRangeControl = Audio.EnableDynamicRangeControl,
                    SpeakerType = Audio.SpeakerType,
                    EnableVirtualSurround = Audio.EnableVirtualSurround
                },
                Language = new LanguageSettings
                {
                    VoiceLanguage = Language.VoiceLanguage,
                    DisplayLanguage = Language.DisplayLanguage,
                    ShowSubtitles = Language.ShowSubtitles
                },
                Accessibility = new AccessibilitySettings
                {
                    TextSizeIndex = Accessibility.TextSizeIndex,
                    TextColorIndex = Accessibility.TextColorIndex,
                    ShowTextBackground = Accessibility.ShowTextBackground,
                    TextBackgroundOpacity = Accessibility.TextBackgroundOpacity,
                    ShowSpeakerName = Accessibility.ShowSpeakerName,
                    ShowSoundSubtitles = Accessibility.ShowSoundSubtitles,
                    ShowCenterDot = Accessibility.ShowCenterDot
                }
            };
        }
    }
}
