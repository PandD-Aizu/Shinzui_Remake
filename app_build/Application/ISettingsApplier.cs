using Shinzui.Domain.Settings;

namespace Shinzui.Application.Settings
{
    /// <summary>
    /// 各設定項目が変更された際に、ゲームエンジンや各種ミドルウェアへ即時反映するためのインターフェース
    /// </summary>
    public interface ISettingsApplier
    {
        /// <summary>
        /// 音量設定を反映します（FMOD等への反映）
        /// </summary>
        void ApplyAudio(AudioSettings audio);

        /// <summary>
        /// 画面・グラフィックス品質設定を反映
        /// </summary>
        void ApplyGraphics(GraphicsSettings graphics);

        /// <summary>
        /// カメラ関連の設定（感度・反転）を反映
        /// </summary>
        void ApplyCamera(CameraSettings camera);

        /// <summary>
        /// 操作関連の設定を反映
        /// </summary>
        void ApplyControls(ControlsSettings controls);

        /// <summary>
        /// ゲームプレイ設定を反映
        /// </summary>
        void ApplyGameplay(GameplaySettings gameplay);

        /// <summary>
        /// 言語設定を反映
        /// </summary>
        void ApplyLanguage(LanguageSettings language);

        /// <summary>
        /// アクセシビリティ設定を反映
        /// </summary>
        void ApplyAccessibility(AccessibilitySettings accessibility);

        /// <summary>
        /// すべての設定をゲームに反映
        /// </summary>
        void ApplyAll(GameSettings settings);
    }
}
