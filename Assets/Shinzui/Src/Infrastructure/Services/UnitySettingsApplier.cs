using System;
using System.Collections.Generic;
using Shinzui.Application.Interfaces;
using Shinzui.Domain.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using CameraSettings = Shinzui.Domain.Settings.CameraSettings;
using GraphicsSettings = Shinzui.Domain.Settings.GraphicsSettings;

namespace Shinzui.Infrastructure.Services
{
    /// <summary>
    /// 設定内容をUnityエンジンおよびFMODオーディオ等の各サブシステムに適用するインフラ層の実装クラス
    /// </summary>
    public partial class UnitySettingsApplier : ISettingsApplier
    {
        // FMODバスのパス定義
        private const string MasterBusPath = "bus:/";
        private const string VoiceBusPath = "bus:/Voice";
        private const string BgmBusPath = "bus:/BGM";
        private const string SeBusPath = "bus:/SE";
        private const string SystemBusPath = "bus:/System";

        private readonly IFMODVCAService _fmodVcaService;
        private static bool _enableDlss;
        private static uint _dlssQuality = 2;
        private static bool _enableFsr2;
        private static uint _fsr2Quality;

        /// <summary>HDRPの実機対応判定から使用するアップスケーラーを取得する</summary>
        /// <returns>DLSS優先の対応方式</returns>
        public UpscalerType GetSupportedUpscaler() => DetectUpscaler();

        /// <summary>現在の描画設定とGPUの対応状態を確認する</summary>
        /// <returns>利用可能な方式、未初期化や非対応ではNone</returns>
        private static UpscalerType DetectUpscaler()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is not HDRenderPipelineAsset asset)
                return UpscalerType.None;

            var settings = asset.currentPlatformRenderPipelineSettings.dynamicResolutionSettings;
            if (!settings.enabled) return UpscalerType.None;

            // ベンダー名ではなく、プラグインと描画APIを含む実際の対応状況で選ぶ
            if (HDDynamicResolutionPlatformCapabilities.DLSSDetected && settings.advancedUpscalerNames.Contains("DLSS"))
                return UpscalerType.Dlss;
            if (HDDynamicResolutionPlatformCapabilities.FSR2Detected && settings.advancedUpscalerNames.Contains("FSR2"))
                return UpscalerType.Fsr2;
            return UpscalerType.None;
        }

        /// <summary>
        /// 再生開始時と終了時にアップスケーリング設定と描画イベントを解除する
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetUpscaling()
        {
            // 再生終了時の解除処理を重複なく登録する
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif

            // ランタイム描画設定を破棄する
            RenderPipelineManager.beginContextRendering -= ApplyUpscalingToCameras;
            ResetGraphicsOverrides();

            _enableDlss = false;
            _dlssQuality = 2;
            _enableFsr2 = false;
            _fsr2Quality = 0;
        }

#if UNITY_EDITOR
        /// <summary>再生終了時に描画イベントと静的な設定を解除する</summary>
        /// <param name="state">エディタの再生状態</param>
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            // 編集モードへ戻る前にランタイム描画設定を破棄する
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                ResetUpscaling();
        }
#endif

        /// <summary>
        /// シーン遷移後のカメラを含めて描画前にアップスケーリング設定を適用する
        /// </summary>
        /// <param name="context">描画コンテキスト</param>
        /// <param name="cameras">今回描画するカメラ</param>
        private static void ApplyUpscalingToCameras(ScriptableRenderContext context, List<Camera> cameras)
        {
            // 編集モードではカメラやシーンを変更しない
            if (!UnityEngine.Application.isPlaying) return;

            // 非対応環境ではネイティブ解像度で描画する
            // パイプライン切り替え後の対応状態を再判定する
            RefreshGraphicsOverrides();

            UpscalerType upscaler = DetectUpscaler();
            bool dlssEnabled = upscaler == UpscalerType.Dlss && _enableDlss;
            bool fsr2Enabled = upscaler == UpscalerType.Fsr2 && _enableFsr2;
            foreach (var camera in cameras)
            {
                if (camera.cameraType != CameraType.Game || camera.targetTexture != null ||
                    !camera.TryGetComponent<HDAdditionalCameraData>(out var data)) continue;

                // カメラ固有の描画項目を上書きする
                ApplyCameraGraphics(data);

                data.allowDeepLearningSuperSampling = dlssEnabled;
                data.allowFidelityFX2SuperResolution = fsr2Enabled;
                data.allowDynamicResolution = dlssEnabled || fsr2Enabled;
                data.deepLearningSuperSamplingUseCustomQualitySettings = true;
                data.deepLearningSuperSamplingQuality = _dlssQuality;
                data.deepLearningSuperSamplingUseCustomAttributes = true;
                data.deepLearningSuperSamplingUseOptimalSettings = true;
                data.fidelityFX2SuperResolutionUseCustomQualitySettings = true;
                data.fidelityFX2SuperResolutionQuality = _fsr2Quality;
                data.fidelityFX2SuperResolutionUseCustomAttributes = true;
                data.fidelityFX2SuperResolutionUseOptimalSettings = true;
            }
        }

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

        /// <summary>画面設定とHDRP描画品質を反映する</summary>
        /// <param name="graphics">適用する描画設定</param>
        public void ApplyGraphics(GraphicsSettings graphics)
        {
#if UNITY_SWITCH && !UNITY_EDITOR
            // Nintendo Switch実機では解像度やレンダリング品質の変更はシステムが自動処理するためスキップ
            // 画面の明るさなど一部項目のみ反映する
            ApplySwitchGraphics(graphics);
            return;
#endif

            // 品質アセットを先に切り替え個別項目の上書きを保つ
            ApplyQualityLevel(graphics.Preset);

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

            // 3. テクスチャ解像度 (0:高, 1:中, 2:低, 3:超低)
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(3 - graphics.TextureQuality, 0, 3);

            // 異方性フィルタリングとメッシュ詳細度を反映する
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            int anisotropy = 2 << Mathf.Clamp(graphics.TextureFilteringQuality, 0, 3);
            Texture.SetGlobalAnisotropicFilteringLimits(anisotropy, anisotropy);
            QualitySettings.lodBias = graphics.MeshQuality switch { 0 => 0.5f, 1 => 1f, _ => 2f };

            // 描画設定を保持しランタイムのVolumeとライトを更新する
            if (!UnityEngine.Application.isPlaying) return;

            _graphics = new GameSettings { Graphics = graphics }.Clone().Graphics;
            _graphicsDirty = true;
            RefreshGraphicsOverrides();

            // カメラ生成やシーン遷移後にも保存済みのアップスケーリング設定を反映する
            _enableDlss = graphics.EnableDlss;
            _dlssQuality = (uint)(2 - Mathf.Clamp(graphics.DlssQuality, 0, 2));
            _enableFsr2 = graphics.EnableFsr2;
            _fsr2Quality = (uint)Mathf.Clamp(graphics.Fsr2Quality, 0, 2);
            RenderPipelineManager.beginContextRendering -= ApplyUpscalingToCameras;
            RenderPipelineManager.beginContextRendering += ApplyUpscalingToCameras;
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
