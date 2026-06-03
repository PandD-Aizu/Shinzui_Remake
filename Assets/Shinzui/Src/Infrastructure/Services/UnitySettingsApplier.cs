using System;
using Shinzui.Application.Interfaces;
using Shinzui.Domain.Settings;
using UnityEngine;

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
                    // サービスが利用できない場合は従来通りBusに直接設定
                    SetFmodBusVolume(MasterBusPath, audio.SystemVolume);
                    SetFmodBusVolume(BgmBusPath, audio.BgmVolume);
                    SetFmodBusVolume(SeBusPath, audio.SeVolume);
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

        public void ApplyGraphics(GraphicsSettings graphics)
        {
#if UNITY_SWITCH && !UNITY_EDITOR
            // Nintendo Switch実機では解像度やレンダリング品質の変更はシステムが自動処理するためスキップ
            // 画面の明るさ(セーフエリア/表示領域調整)など一部項目のみ反映します
            ApplySwitchGraphics(graphics);
            return;
#endif

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

            // 4. アンチエイリアス
            // 0: OFF, 2: 2x, 4: 4x, 8: 8x
            QualitySettings.antiAliasing = graphics.AntiAliasingType switch
            {
                0 => 0,
                1 => 2, // FXAA相当のマルチサンプル代替
                2 => 4, // SMAA
                3 => 8, // TAA/MSAA最大
                _ => 0
            };

            // 5. 影品質
            // QualitySettingsのShadow設定などを適用
            QualitySettings.shadows = graphics.ShadowQuality > 0 ? ShadowQuality.All : ShadowQuality.Disable;
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
#if FMOD_UNITY
                var bus = FMODUnity.RuntimeManager.GetBus(busPath);
                if (bus.isValid())
                {
                    bus.setVolume(volume);
                }
#endif
            }
            catch (Exception)
            {
                // エディタ未初期化エラーなどを無視
            }
        }

        private void ApplySwitchGraphics(GraphicsSettings graphics)
        {
            // Switch実機向けの制限付きグラフィックス適用
        }
    }
}
