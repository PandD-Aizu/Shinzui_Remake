using System.Collections.Generic;
using Shinzui.Domain.Settings;

namespace Shinzui.Application.Interfaces
{
    /// <summary>
    /// 各設定項目が変更された際に、ゲームエンジンや各種ミドルウェアへ即時反映するためのインターフェース
    /// </summary>
    public interface ISettingsApplier
    {
        /// <summary>現在の描画環境でレイトレーシングを使用できるか取得する</summary>
        /// <returns>使用可能なら真</returns>
        bool IsRayTracingSupported();

        /// <summary>重複を除いた昇順の画面解像度を取得する</summary>
        /// <returns>幅x高さ形式の解像度一覧</returns>
        IReadOnlyList<string> GetAvailableResolutions();

        /// <summary>実行環境で使用できるアップスケーラーを取得する</summary>
        /// <returns>DLSSを優先し、非対応の場合はFSR 2、いずれも非対応ならNone</returns>
        UpscalerType GetSupportedUpscaler();

        /// <summary>
        /// 音量設定を反映する
        /// </summary>
        void ApplyAudio(AudioSettings audio);

        /// <summary>
        /// 画面・グラフィックス品質設定を反映する
        /// </summary>
        void ApplyGraphics(GraphicsSettings graphics);

        /// <summary>
        /// カメラ関連の設定（感度・反転）を反映する
        /// </summary>
        void ApplyCamera(CameraSettings camera);

        /// <summary>
        /// 操作関連の設定を反映する
        /// </summary>
        void ApplyControls(ControlsSettings controls);

        /// <summary>
        /// ゲームプレイ設定を反映する
        /// </summary>
        void ApplyGameplay(GameplaySettings gameplay);

        /// <summary>
        /// 言語設定を反映する
        /// </summary>
        void ApplyLanguage(LanguageSettings language);

        /// <summary>
        /// アクセシビリティ設定を反映する
        /// </summary>
        void ApplyAccessibility(AccessibilitySettings accessibility);

        /// <summary>
        /// すべての設定をゲームに反映する
        /// </summary>
        void ApplyAll(GameSettings settings);
    }
}
