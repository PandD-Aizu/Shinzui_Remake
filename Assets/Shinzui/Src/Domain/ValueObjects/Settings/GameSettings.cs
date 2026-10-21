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
        /// 全設定を初期値にリセット
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
        /// 設定オブジェクトのディープコピーを作成する
        /// </summary>
        /// <returns>変更を独立して保持する設定のコピー</returns>
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
                Graphics = Graphics.Clone(),
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
