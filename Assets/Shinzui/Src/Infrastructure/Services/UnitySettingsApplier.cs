using System;
using Shinzui.Application.Interfaces;
using Shinzui.Domain.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GraphicsSettings = Shinzui.Domain.Settings.GraphicsSettings;

namespace Shinzui.Infrastructure.Services
{
    /// <summary>
    /// 設定内容をUnityエンジンおよびFMODオーディオ等の各サブシステムに適用するインフラ層の実装クラス
    /// </summary>
    public class UnitySettingsApplier : ISettingsApplier
    {
        // FMODバスのパス定義
        private const string MasterBusPath = "bus:/";
        private const string VoiceBusPath = "bus:/Voice";
        private const string BgmBusPath = "bus:/BGM";
        private const string SeBusPath = "bus:/SE";
        private const string SystemBusPath = "bus:/System";

        private readonly IFMODVCAService _fmodVcaService;

        private static GraphicsQualityPreset _cameraQualityPreset;
        private static int _cameraAntiAliasingType;
        private static bool _allowHdrOutput;

        public UnitySettingsApplier(IFMODVCAService fmodVcaService = null)
        {
            _fmodVcaService = fmodVcaService;
        }

        public void ApplyAudio(Domain.Settings.AudioSettings audio)
        {
            try
            {
                // FMOD VCA サービス経由で音量を設定
                if (_fmodVcaService != null)
                {
                    _fmodVcaService.SetMasterVolume(audio.SystemVolume);
                    _fmodVcaService.SetBGMVolume(audio.BgmVolume);
                    _fmodVcaService.SetSEVolume(audio.SeVolume);
                }
                else
                {
                    // Use the same authored VCAs as SoundSystemLifetimeScope. WorldSE is a
                    // sibling of SE, so changing bus:/SE alone misses generated world sounds.
                    SetFmodVcaVolume("vca:/Master", audio.SystemVolume);
                    SetFmodVcaVolume("vca:/BGM", audio.BgmVolume);
                    SetFmodVcaVolume("vca:/SE", audio.SeVolume);
                }

                // Voice は VCA サービスに含まれないため、従来通り Bus に適用
                SetFmodBusVolume(VoiceBusPath, audio.VoiceVolume);

                // DRC(ダイナミックレンジコントロール)の切り替え
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Settings] Failed to apply audio: {ex.Message}");
            }
        }

        /// <summary>
        /// 描画プリセットと個別設定をエンジンおよびURPカメラへ適用する
        /// </summary>
        /// <param name="graphics">適用するグラフィックス設定</param>
        public void ApplyGraphics(GraphicsSettings graphics)
        {
#if UNITY_SWITCH && !UNITY_EDITOR
            // Nintendo Switch実機では解像度やレンダリング品質の変更はシステムが自動処理するためスキップ
            // 画面の明るさ(セーフエリア/表示領域調整)など一部項目のみ反映します
            ApplySwitchGraphics(graphics);
            return;
#endif

            // インデックスを固定せず品質名から対応するレンダーパイプラインを選択
            ApplyQualityPreset(graphics.QualityPreset);
            bool isUltra = graphics.QualityPreset == GraphicsQualityPreset.Ultra;

            // 画面解像度とウィンドウモードの適用
            string[] resParts = graphics.Resolution.Split('x');
            if (resParts.Length == 2 && int.TryParse(resParts[0], out int width) && int.TryParse(resParts[1], out int height))
            {
                FullScreenMode mode = graphics.ScreenMode switch
                {
                    0 => FullScreenMode.ExclusiveFullScreen, // フルスクリーン
                    1 => FullScreenMode.Windowed,            // ウィンドウ
                    2 => FullScreenMode.FullScreenWindow,    // 仮想フルスクリーン (ボーダーレスウィンドウ)
                    _ => FullScreenMode.FullScreenWindow
                };
                
                var refreshRate = new RefreshRate
                {
                    numerator = (uint)Math.Max(graphics.RefreshRate, 1),
                    denominator = 1u
                };

                Screen.SetResolution(width, height, mode, refreshRate);
            }

            // 2. 垂直同期 (VSync) とフレームレート制限
            QualitySettings.vSyncCount = graphics.EnableVSync ? 1 : 0;
            UnityEngine.Application.targetFrameRate = graphics.FrameRateLimit > 0 ? graphics.FrameRateLimit : -1;

            // ULTRAでは旧保存データの個別設定に関係なく元解像度のテクスチャを使用
            QualitySettings.globalTextureMipmapLimit = isUltra ? 0 : Mathf.Clamp(3 - graphics.TextureQuality, 0, 3);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            int anisotropy = isUltra ? 16 : 1 << Mathf.Clamp(graphics.TextureFilteringQuality + 1, 1, 4);
            Texture.SetGlobalAnisotropicFilteringLimits(anisotropy, anisotropy);
            QualitySettings.lodBias = isUltra ? 4f : graphics.MeshQuality switch { 0 => 0.7f, 1 => 1f, _ => 2f };
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.realtimeReflectionProbes = isUltra || graphics.GiAndReflectionQuality >= 3;

            // FXAA/SMAA/TAAはURPのポスト処理として設定しMSAAとの競合を避ける
            QualitySettings.antiAliasing = 0;
            _cameraQualityPreset = graphics.QualityPreset;
            _cameraAntiAliasingType = isUltra ? 3 : graphics.AntiAliasingType;
            _allowHdrOutput = graphics.EnableHDR;
            RenderPipelineManager.beginCameraRendering -= ApplyCameraRenderingSettings;
            RenderPipelineManager.beginCameraRendering += ApplyCameraRenderingSettings;
            foreach (var renderingCamera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                ApplyCameraRenderingSettings(default, renderingCamera);
            }

            // 通常品質の影の切り替えを維持しULTRAでは常に影を有効化
            QualitySettings.shadows = isUltra || graphics.ShadowQuality > 0
                ? UnityEngine.ShadowQuality.All : UnityEngine.ShadowQuality.Disable;
        }

        /// <summary>
        /// 保存用の品質番号をUnityの品質名へ変換して切り替える
        /// </summary>
        /// <param name="preset">適用する描画品質</param>
        private static void ApplyQualityPreset(GraphicsQualityPreset preset)
        {
            string qualityName = preset == GraphicsQualityPreset.Medium ? "Midium" : preset.ToString();
            string[] names = QualitySettings.names;
            for (int index = 0; index < names.Length; index++)
            {
                // 既存プロジェクトのMidium表記と修正後のMedium表記を受け入れる
                if (!string.Equals(names[index], qualityName, StringComparison.OrdinalIgnoreCase)
                    && !(preset == GraphicsQualityPreset.Medium && names[index] == "Medium"))
                {
                    continue;
                }

                if (QualitySettings.GetQualityLevel() != index)
                {
                    QualitySettings.SetQualityLevel(index, true);
                }
                return;
            }
        }

        /// <summary>
        /// シーン遷移後のゲームカメラにもHDR描画とアンチエイリアスを反映する
        /// </summary>
        /// <param name="context">描画コンテキスト</param>
        /// <param name="renderingCamera">描画対象のカメラ</param>
        private static void ApplyCameraRenderingSettings(ScriptableRenderContext context, Camera renderingCamera)
        {
            // ポータルなどポスト処理を無効にした中間描画の設定を維持
            if (renderingCamera.cameraType != CameraType.Game
                || !renderingCamera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData)
                || cameraData.renderType != CameraRenderType.Base
                || (renderingCamera.targetTexture != null && !cameraData.renderPostProcessing))
            {
                return;
            }

            // HDR内部描画はディスプレイのHDR出力設定と独立して有効化
            renderingCamera.allowHDR = true;
            renderingCamera.allowMSAA = false;
            renderingCamera.allowDynamicResolution = false;
            cameraData.allowHDROutput = _allowHdrOutput;
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = _cameraAntiAliasingType switch
            {
                1 => AntialiasingMode.FastApproximateAntialiasing,
                2 => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                3 => AntialiasingMode.TemporalAntiAliasing,
                _ => AntialiasingMode.None
            };

            // カメラスタックはURPのTAA対象外なのでSMAAへ切り替える
            if (cameraData.antialiasing == AntialiasingMode.TemporalAntiAliasing && cameraData.cameraStack?.Count > 0)
            {
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.taaSettings.quality = _cameraQualityPreset == GraphicsQualityPreset.Ultra
                ? TemporalAAQuality.VeryHigh : TemporalAAQuality.High;
            cameraData.taaSettings.contrastAdaptiveSharpening = 0f;
        }

        /// <summary>
        /// ドメインリロードを省略した再生開始時にもカメラ描画の購読を初期化する
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCameraRenderingSettings()
        {
            RenderPipelineManager.beginCameraRendering -= ApplyCameraRenderingSettings;
        }

        public void ApplyCamera(CameraSettings camera)
        {
            // カメラワークの挙動を制御するマネージャーへパラメータを通知
            // 実際のCinemachine Axis設定などは入力監視処理でこの設定クラスを参照
        }

        public void ApplyControls(ControlsSettings controls)
        {
            // マウスカーソルのウィンドウ固定制御
#if UNITY_STANDALONE && !UNITY_EDITOR
            if (controls.LockCursorToWindow)
            {
                Cursor.lockState = CursorLockMode.Confined;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
            }
#endif
        }

        public void ApplyGameplay(GameplaySettings gameplay)
        {
            // 照準アシスト強度、チュートリアル、HUD表示切替などをUIマネージャーや武器スクリプトに反映
        }

        public void ApplyLanguage(LanguageSettings language)
        {
            // ローカライズアセット (Unity Localization packageなど) の選択言語を更新
        }

        public void ApplyAccessibility(AccessibilitySettings accessibility)
        {
            // 字幕表示システムにパラメータを反映
        }

        public void ApplyAll(GameSettings settings)
        {
            ApplyAudio(settings.Audio);
            ApplyGraphics(settings.Graphics);
            ApplyCamera(settings.Camera);
            ApplyControls(settings.Controls);
            ApplyGameplay(settings.Gameplay);
            ApplyLanguage(settings.Language);
            ApplyAccessibility(settings.Accessibility);
        }

        private void SetFmodBusVolume(string busPath, float volume)
        {
            // FMOD Unity インテグレーションが読み込まれている場合にボリュームを適用
            try
            {
                if (FMODUnity.RuntimeManager.StudioSystem.getBus(busPath, out var bus) == FMOD.RESULT.OK && bus.isValid())
                {
                    bus.setVolume(Mathf.Clamp01(volume));
                }
            }
            catch (Exception)
            {
                // エディタ未初期化エラーなどを無視
            }
        }

        private static void SetFmodVcaVolume(string path, float volume)
        {
            if (FMODUnity.RuntimeManager.StudioSystem.getVCA(path, out var vca) == FMOD.RESULT.OK && vca.isValid())
                vca.setVolume(Mathf.Clamp01(volume));
        }

        private void ApplySwitchGraphics(GraphicsSettings graphics)
        {
            // Switch実機向けの制限付きグラフィックス適用
        }
    }
}
