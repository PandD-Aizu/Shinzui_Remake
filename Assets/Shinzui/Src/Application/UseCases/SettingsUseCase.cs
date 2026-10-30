using System;
using System.Collections.Generic;
using Preset = Shinzui.Domain.Settings.GraphicsPreset;
using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Domain.Settings;

namespace Shinzui.Application.UseCases
{
    /// <summary>
    /// ゲーム設定のユースケース（ビジネスロジック）を制御するクラス
    /// DomainレイヤーのGameSettingsを完全に内部で保持・管理し、
    /// Presentationレイヤーへは個別のReactiveProperty（プリミティブ型）のみを公開する
    /// </summary>
    public class SettingsUseCase : IDisposable
    {
        private readonly ISettingsRepository _repository;
        private readonly ISettingsApplier _applier;
        private readonly CompositeDisposable _editDisposables = new();

        // 決定済みの正式な設定
        private GameSettings _activeSettings;

        // 編集中の設定
        private GameSettings _editingSettings;

        // --- Presentation層へ公開するリアクティブプロパティ ---
        // 音量関連
        public ReactiveProperty<float> MasterVolume { get; } = new();
        public ReactiveProperty<float> BgmVolume { get; } = new();
        public ReactiveProperty<float> SeVolume { get; } = new();
        public ReactiveProperty<float> VoiceVolume { get; } = new();

        // カメラ・マウス感度
        public ReactiveProperty<float> ControllerNormalSpeed { get; } = new();
        public ReactiveProperty<float> MouseNormalSensitivity { get; } = new();

        // 明るさ
        public ReactiveProperty<float> Brightness { get; } = new();
        public ReactiveProperty<bool> EnableDlss { get; } = new();
        public ReactiveProperty<int> DlssQuality { get; } = new();
        public ReactiveProperty<bool> EnableFsr2 { get; } = new();
        public ReactiveProperty<int> Fsr2Quality { get; } = new();

        /// <summary>現在のPC構成で表示するアップスケーラー</summary>
        public UpscalerType SupportedUpscaler => IsConsolePlatform() ? UpscalerType.None : _applier.GetSupportedUpscaler();

        /// <summary>表示中のアップスケーラーの有効状態</summary>
        public ReactiveProperty<bool> UpscalingEnabled => SupportedUpscaler == UpscalerType.Fsr2 ? EnableFsr2 : EnableDlss;

        /// <summary>表示中のアップスケーラーの品質</summary>
        public ReactiveProperty<int> UpscalingQuality => SupportedUpscaler == UpscalerType.Fsr2 ? Fsr2Quality : DlssQuality;

        private bool _syncingGraphics;

        /// <summary>選択中の描画品質プリセット</summary>
        public ReactiveProperty<GraphicsPreset> GraphicsPreset { get; } = new();

        /// <summary>選択中の画面解像度の添字</summary>
        public ReactiveProperty<int> ResolutionIndex { get; } = new();
        public ReactiveProperty<int> ScreenMode { get; } = new();
        public ReactiveProperty<int> FrameRateLimit { get; } = new();
        public ReactiveProperty<int> AntiAliasing { get; } = new();
        public ReactiveProperty<int> TextureQuality { get; } = new();
        public ReactiveProperty<int> TextureFiltering { get; } = new();
        public ReactiveProperty<int> MeshQuality { get; } = new();
        public ReactiveProperty<int> ShadowQuality { get; } = new();
        public ReactiveProperty<int> GiAndReflectionQuality { get; } = new();
        public ReactiveProperty<int> VolumeLightQuality { get; } = new();
        public ReactiveProperty<bool> EnableVSync { get; } = new();
        public ReactiveProperty<bool> EnableRayTracing { get; } = new();
        public ReactiveProperty<bool> EnableAo { get; } = new();
        public ReactiveProperty<bool> EnableSsr { get; } = new();
        public ReactiveProperty<bool> EnableContactShadow { get; } = new();
        public ReactiveProperty<bool> EnableSss { get; } = new();
        public ReactiveProperty<bool> EnableBloom { get; } = new();
        public ReactiveProperty<bool> EnableLensFlare { get; } = new();
        public ReactiveProperty<bool> EnableFilmGrain { get; } = new();
        public ReactiveProperty<bool> EnableDepthOfField { get; } = new();
        public ReactiveProperty<bool> EnableLensDistortion { get; } = new();

        /// <summary>実行環境で利用できる画面解像度</summary>
        public IReadOnlyList<string> AvailableResolutions { get; }

        /// <summary>現在の描画環境におけるレイトレーシング対応状態</summary>
        public bool IsRayTracingSupported => !IsConsolePlatform() && _applier.IsRayTracingSupported();

        // アクセシビリティ
        public ReactiveProperty<bool> ShowCenterDot { get; } = new();

        /// <summary>設定の保存先と適用先を受け取り初期化する</summary>
        /// <param name="repository">設定の保存先</param>
        /// <param name="applier">設定の適用先</param>
        public SettingsUseCase(ISettingsRepository repository, ISettingsApplier applier)
        {
            // 設定の依存先と解像度一覧を保持する
            _repository = repository;
            _applier = applier;
            AvailableResolutions = _applier.GetAvailableResolutions();
            
            // 起動時にロードしてエンジンに反映
            Initialize();
        }

        /// <summary>保存済み設定を読み込み適用する</summary>
        public void Initialize()
        {
            // 保存値を初回の描画状態へ反映する
            _activeSettings = _repository.Load();
            _applier.ApplyAll(_activeSettings);
        }

        /// <summary>
        /// オプション画面を開いた際に編集用セッションを開始する
        /// 内部でドメインモデルのクローンを生成しプロパティ群を同期する
        /// </summary>
        public void BeginEdit()
        {
            _editDisposables.Clear();
            _editingSettings = _activeSettings.Clone();

            // ドメインデータを公開プロパティへ転記
            SyncModelToProperties(_editingSettings);

            // プロパティ値変更時の自動プレビュー適用をバインド
            BindPropertiesToModel();
        }

        /// <summary>編集モデルを公開プロパティに同期する</summary>
        /// <param name="model">同期元モデル</param>
        private void SyncModelToProperties(GameSettings model)
        {
            // 音量を同期する
            MasterVolume.Value = model.Audio.SystemVolume;
            BgmVolume.Value = model.Audio.BgmVolume;
            SeVolume.Value = model.Audio.SeVolume;
            VoiceVolume.Value = model.Audio.VoiceVolume;

            // 入力感度を同期する
            ControllerNormalSpeed.Value = model.Camera.ControllerNormalSpeed;
            MouseNormalSensitivity.Value = model.Camera.MouseNormalSensitivity;

            // 画面の明るさとアップスケーラーを同期する
            Brightness.Value = model.Graphics.BrightnessValue;
            EnableDlss.Value = model.Graphics.EnableDlss;
            DlssQuality.Value = Math.Clamp(model.Graphics.DlssQuality, 0, 2);
            EnableFsr2.Value = model.Graphics.EnableFsr2;
            Fsr2Quality.Value = Math.Clamp(model.Graphics.Fsr2Quality, 0, 2);

            // 描画項目を通知の再入なしで同期する
            SyncGraphicsProperties(model.Graphics);

            ShowCenterDot.Value = model.Accessibility.ShowCenterDot;
        }

        /// <summary>描画項目を公開プロパティへ同期する</summary>
        /// <param name="graphics">同期元の設定</param>
        private void SyncGraphicsProperties(GraphicsSettings graphics)
        {
            // 一括転記中のプレビューとプリセット再判定を止める
            _syncingGraphics = true;
            GraphicsPreset.Value = graphics.Preset;
            ScreenMode.Value = graphics.ScreenMode;
            FrameRateLimit.Value = graphics.FrameRateLimit;
            AntiAliasing.Value = graphics.AntiAliasingType;
            TextureQuality.Value = graphics.TextureQuality;
            TextureFiltering.Value = graphics.TextureFilteringQuality;
            MeshQuality.Value = graphics.MeshQuality;
            ShadowQuality.Value = graphics.ShadowQuality;
            GiAndReflectionQuality.Value = graphics.GiAndReflectionQuality;
            VolumeLightQuality.Value = graphics.VolumeLightQuality;
            EnableVSync.Value = graphics.EnableVSync;
            EnableRayTracing.Value = graphics.EnableRayTracing;
            EnableAo.Value = graphics.EnableAo;
            EnableSsr.Value = graphics.EnableSsr;
            EnableContactShadow.Value = graphics.EnableContactShadow;
            EnableSss.Value = graphics.EnableSss;
            EnableBloom.Value = graphics.EnableBloom;
            EnableLensFlare.Value = graphics.EnableLensFlare;
            EnableFilmGrain.Value = graphics.EnableFilmGrain;
            EnableDepthOfField.Value = graphics.EnableDepthOfField;
            EnableLensDistortion.Value = graphics.EnableLensDistortion;

            // 保存済み解像度を優先し現在の画面解像度へフォールバックする
            int index = FindResolution(graphics.Resolution);
            if (index < 0) index = FindResolution($"{UnityEngine.Screen.width}x{UnityEngine.Screen.height}");
            if (index < 0) index = AvailableResolutions.Count - 1;
            ResolutionIndex.Value = index;
            if (index >= 0) graphics.Resolution = AvailableResolutions[index];
            _syncingGraphics = false;
        }

        /// <summary>解像度の添字を検索する</summary>
        /// <param name="resolution">幅x高さ形式の解像度</param>
        /// <returns>一致する添字または負の値</returns>
        private int FindResolution(string resolution)
        {
            // 一覧の文字列と照合する
            for (int i = 0; i < AvailableResolutions.Count; i++)
                if (AvailableResolutions[i] == resolution) return i;

            // 一覧にない解像度を区別する
            return -1;
        }

        /// <summary>指定した品質プリセットを編集設定に適用する</summary>
        /// <param name="preset">適用するプリセット</param>
        public void SelectGraphicsPreset(GraphicsPreset preset)
        {
            // 編集外とカスタムの選択を無視する
            if (_editingSettings == null || _syncingGraphics || preset < Preset.Low || preset > Preset.Ultra) return;

            // 全項目の転記後に一度だけプレビューする
            GraphicsPresets.Apply(_editingSettings.Graphics, preset);
            SyncGraphicsProperties(_editingSettings.Graphics);
            ApplyPreview();
        }

        /// <summary>個別の描画項目を編集モデルへ接続する</summary>
        /// <typeparam name="T">設定値の型</typeparam>
        /// <param name="property">公開プロパティ</param>
        /// <param name="write">モデルへの転記処理</param>
        /// <param name="presetControlled">プリセット判定対象か</param>
        private void BindGraphics<T>(ReactiveProperty<T> property, Action<T> write, bool presetControlled)
        {
            // 一括同期以外の変更だけをモデルへ反映する
            property.Skip(1).Subscribe(value =>
            {
                if (_syncingGraphics || _editingSettings == null) return;
                write(value);

                // 個別設定の組み合わせからプリセットを再判定する
                if (presetControlled)
                {
                    _editingSettings.Graphics.Preset = GraphicsPresets.Detect(_editingSettings.Graphics);
                    _syncingGraphics = true;
                    GraphicsPreset.Value = _editingSettings.Graphics.Preset;
                    _syncingGraphics = false;
                }

                // 更新した設定を描画へ反映する
                ApplyPreview();
            }).AddTo(_editDisposables);
        }

        /// <summary>編集通知と自動保存を接続する</summary>
        private void BindPropertiesToModel()
        {
            // 各プロパティ変更時、対応するドメイン設定の値を更新してプレビュー適用
            MasterVolume.Skip(1).Subscribe(val => { _editingSettings.Audio.SystemVolume = val; ApplyPreview(); }).AddTo(_editDisposables);
            BgmVolume.Skip(1).Subscribe(val => { _editingSettings.Audio.BgmVolume = val; ApplyPreview(); }).AddTo(_editDisposables);
            SeVolume.Skip(1).Subscribe(val => { _editingSettings.Audio.SeVolume = val; ApplyPreview(); }).AddTo(_editDisposables);
            VoiceVolume.Skip(1).Subscribe(val => { _editingSettings.Audio.VoiceVolume = val; ApplyPreview(); }).AddTo(_editDisposables);

            ControllerNormalSpeed.Skip(1).Subscribe(val => { _editingSettings.Camera.ControllerNormalSpeed = val; ApplyPreview(); }).AddTo(_editDisposables);
            MouseNormalSensitivity.Skip(1).Subscribe(val => { _editingSettings.Camera.MouseNormalSensitivity = val; ApplyPreview(); }).AddTo(_editDisposables);

            Brightness.Skip(1).Subscribe(val => { _editingSettings.Graphics.BrightnessValue = val; ApplyPreview(); }).AddTo(_editDisposables);
            EnableDlss.Skip(1).Subscribe(val => { _editingSettings.Graphics.EnableDlss = val; ApplyPreview(); }).AddTo(_editDisposables);
            DlssQuality.Skip(1).Subscribe(val => { _editingSettings.Graphics.DlssQuality = Math.Clamp(val, 0, 2); ApplyPreview(); }).AddTo(_editDisposables);
            EnableFsr2.Skip(1).Subscribe(val => { _editingSettings.Graphics.EnableFsr2 = val; ApplyPreview(); }).AddTo(_editDisposables);
            Fsr2Quality.Skip(1).Subscribe(val => { _editingSettings.Graphics.Fsr2Quality = Math.Clamp(val, 0, 2); ApplyPreview(); }).AddTo(_editDisposables);

            ShowCenterDot.Skip(1).Subscribe(val => { _editingSettings.Accessibility.ShowCenterDot = val; ApplyPreview(); }).AddTo(_editDisposables);

            // プリセット選択と個別描画項目を接続する
            GraphicsPreset.Skip(1).Subscribe(SelectGraphicsPreset).AddTo(_editDisposables);
            BindGraphics(ResolutionIndex, value =>
            {
                // 有効な解像度だけを保存する
                if (value >= 0 && value < AvailableResolutions.Count)
                    _editingSettings.Graphics.Resolution = AvailableResolutions[value];
            }, false);
            BindGraphics(ScreenMode, value => _editingSettings.Graphics.ScreenMode = Math.Clamp(value, 0, 2), false);
            BindGraphics(FrameRateLimit, value => _editingSettings.Graphics.FrameRateLimit = (value == 30 || value == 60 || value == 120 || value == 144 ? value : 0), false);
            BindGraphics(AntiAliasing, value => _editingSettings.Graphics.AntiAliasingType = Math.Clamp(value, 0, 3), true);
            BindGraphics(TextureQuality, value => _editingSettings.Graphics.TextureQuality = Math.Clamp(value, 0, 3), true);
            BindGraphics(TextureFiltering, value => _editingSettings.Graphics.TextureFilteringQuality = Math.Clamp(value, 0, 3), true);
            BindGraphics(MeshQuality, value => _editingSettings.Graphics.MeshQuality = Math.Clamp(value, 0, 2), true);
            BindGraphics(ShadowQuality, value => _editingSettings.Graphics.ShadowQuality = Math.Clamp(value, 0, 3), true);
            BindGraphics(GiAndReflectionQuality, value => _editingSettings.Graphics.GiAndReflectionQuality = Math.Clamp(value, 0, 3), true);
            BindGraphics(VolumeLightQuality, value => _editingSettings.Graphics.VolumeLightQuality = Math.Clamp(value, 0, 3), true);
            BindGraphics(EnableVSync, value => _editingSettings.Graphics.EnableVSync = value, false);
            BindGraphics(EnableRayTracing, value => _editingSettings.Graphics.EnableRayTracing = value, true);
            BindGraphics(EnableAo, value => _editingSettings.Graphics.EnableAo = value, true);
            BindGraphics(EnableSsr, value => _editingSettings.Graphics.EnableSsr = value, true);
            BindGraphics(EnableContactShadow, value => _editingSettings.Graphics.EnableContactShadow = value, true);
            BindGraphics(EnableSss, value => _editingSettings.Graphics.EnableSss = value, true);
            BindGraphics(EnableBloom, value => _editingSettings.Graphics.EnableBloom = value, true);
            BindGraphics(EnableLensFlare, value => _editingSettings.Graphics.EnableLensFlare = value, true);
            BindGraphics(EnableFilmGrain, value => _editingSettings.Graphics.EnableFilmGrain = value, false);
            BindGraphics(EnableDepthOfField, value => _editingSettings.Graphics.EnableDepthOfField = value, false);
            BindGraphics(EnableLensDistortion, value => _editingSettings.Graphics.EnableLensDistortion = value, false);

            // 自動セーブのDebounce
            Observable.Merge(
                GraphicsPreset.Skip(1).Select(_ => Unit.Default),
                ResolutionIndex.Skip(1).Select(_ => Unit.Default),
                ScreenMode.Skip(1).Select(_ => Unit.Default),
                FrameRateLimit.Skip(1).Select(_ => Unit.Default),
                AntiAliasing.Skip(1).Select(_ => Unit.Default),
                TextureQuality.Skip(1).Select(_ => Unit.Default),
                TextureFiltering.Skip(1).Select(_ => Unit.Default),
                MeshQuality.Skip(1).Select(_ => Unit.Default),
                ShadowQuality.Skip(1).Select(_ => Unit.Default),
                GiAndReflectionQuality.Skip(1).Select(_ => Unit.Default),
                VolumeLightQuality.Skip(1).Select(_ => Unit.Default),
                EnableVSync.Skip(1).Select(_ => Unit.Default),
                EnableRayTracing.Skip(1).Select(_ => Unit.Default),
                EnableAo.Skip(1).Select(_ => Unit.Default),
                EnableSsr.Skip(1).Select(_ => Unit.Default),
                EnableContactShadow.Skip(1).Select(_ => Unit.Default),
                EnableSss.Skip(1).Select(_ => Unit.Default),
                EnableBloom.Skip(1).Select(_ => Unit.Default),
                EnableLensFlare.Skip(1).Select(_ => Unit.Default),
                EnableFilmGrain.Skip(1).Select(_ => Unit.Default),
                EnableDepthOfField.Skip(1).Select(_ => Unit.Default),
                EnableLensDistortion.Skip(1).Select(_ => Unit.Default),
                MasterVolume.Skip(1).Select(_ => Unit.Default),
                BgmVolume.Skip(1).Select(_ => Unit.Default),
                SeVolume.Skip(1).Select(_ => Unit.Default),
                VoiceVolume.Skip(1).Select(_ => Unit.Default),
                ControllerNormalSpeed.Skip(1).Select(_ => Unit.Default),
                MouseNormalSensitivity.Skip(1).Select(_ => Unit.Default),
                Brightness.Skip(1).Select(_ => Unit.Default),
                EnableDlss.Skip(1).Select(_ => Unit.Default),
                DlssQuality.Skip(1).Select(_ => Unit.Default),
                EnableFsr2.Skip(1).Select(_ => Unit.Default),
                Fsr2Quality.Skip(1).Select(_ => Unit.Default),
                ShowCenterDot.Skip(1).Select(_ => Unit.Default)
            )
            .Debounce(TimeSpan.FromSeconds(0.5f))
            .Subscribe(_ => AutoSave())
            .AddTo(_editDisposables);
        }

        /// <summary>編集内容をプレビューする</summary>
        private void ApplyPreview()
        {
            if (_editingSettings != null)
            {
                _applier.ApplyAll(_editingSettings);
            }
        }

        /// <summary>編集内容を正式設定として保存する</summary>
        private void AutoSave()
        {
            if (_editingSettings == null) return;

            _activeSettings = _editingSettings.Clone();
            _repository.Save(_activeSettings);
            UnityEngine.Debug.Log("[Settings] Auto-saved successfully via Debounce.");
        }

        /// <summary>
        /// 編集内容を確定し、ファイル保存を行う
        /// </summary>
        public void SaveAndApply()
        {
            if (_editingSettings == null) return;

            _activeSettings = _editingSettings.Clone();
            _repository.Save(_activeSettings);
            _applier.ApplyAll(_activeSettings);

            _editDisposables.Clear();
            _editingSettings = null;
        }

        /// <summary>
        /// 編集をキャンセルし、プレビューを元の正式設定にロールバックする
        /// </summary>
        public void CancelEdit()
        {
            if (_editingSettings == null) return;

            // 元の設定に戻す
            _applier.ApplyAll(_activeSettings);

            _editDisposables.Clear();
            _editingSettings = null;
        }

        /// <summary>
        /// 編集中のドラフトをデフォルト値にリセット
        /// </summary>
        public void ResetToDefault()
        {
            _editDisposables.Clear();
            _editingSettings = new GameSettings();
            _editingSettings.ResetToDefault();

            // ドメインデータをUIプロパティに再適用
            SyncModelToProperties(_editingSettings);

            // プレビュー反映
            _applier.ApplyAll(_editingSettings);

            // 監視を再接続
            BindPropertiesToModel();

            // 初期値への変更を保存する
            AutoSave();
        }

        /// <summary>コンソール実機か判定する</summary>
        /// <returns>コンソール実機なら真</returns>
        public bool IsConsolePlatform()
        {
#if (UNITY_SWITCH || UNITY_SWITCH2 || UNITY_PS4 || UNITY_PS5 || UNITY_XBOXONE || UNITY_GAMECORE) && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        /// <summary>編集通知と公開プロパティを解放する</summary>
        public void Dispose()
        {
            // 通知を停止して各プロパティを破棄する
            _editDisposables.Dispose();
            GraphicsPreset.Dispose();
            ResolutionIndex.Dispose();
            ScreenMode.Dispose();
            FrameRateLimit.Dispose();
            AntiAliasing.Dispose();
            TextureQuality.Dispose();
            TextureFiltering.Dispose();
            MeshQuality.Dispose();
            ShadowQuality.Dispose();
            GiAndReflectionQuality.Dispose();
            VolumeLightQuality.Dispose();
            EnableVSync.Dispose();
            EnableRayTracing.Dispose();
            EnableAo.Dispose();
            EnableSsr.Dispose();
            EnableContactShadow.Dispose();
            EnableSss.Dispose();
            EnableBloom.Dispose();
            EnableLensFlare.Dispose();
            EnableFilmGrain.Dispose();
            EnableDepthOfField.Dispose();
            EnableLensDistortion.Dispose();
            MasterVolume.Dispose();
            BgmVolume.Dispose();
            SeVolume.Dispose();
            VoiceVolume.Dispose();
            ControllerNormalSpeed.Dispose();
            MouseNormalSensitivity.Dispose();
            Brightness.Dispose();
            EnableDlss.Dispose();
            DlssQuality.Dispose();
            EnableFsr2.Dispose();
            Fsr2Quality.Dispose();
            ShowCenterDot.Dispose();
        }
    }
}
