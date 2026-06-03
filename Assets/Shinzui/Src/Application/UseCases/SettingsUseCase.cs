using System;
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
    public class SettingsUseCase
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

        // アクセシビリティ
        public ReactiveProperty<bool> ShowCenterDot { get; } = new();

        public SettingsUseCase(ISettingsRepository repository, ISettingsApplier applier)
        {
            _repository = repository;
            _applier = applier;
            
            // 起動時にロードしてエンジンに反映
            Initialize();
        }

        public void Initialize()
        {
            _activeSettings = _repository.Load();
            _applier.ApplyAll(_activeSettings);
        }

        /// <summary>
        /// オプション画面を開いた際に、編集用セッションを開始します。
        /// 内部でドメインモデルのクローンを生成し、プロパティ群を同期します。
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

        private void SyncModelToProperties(GameSettings model)
        {
            MasterVolume.Value = model.Audio.SystemVolume;
            BgmVolume.Value = model.Audio.BgmVolume;
            SeVolume.Value = model.Audio.SeVolume;
            VoiceVolume.Value = model.Audio.VoiceVolume;

            ControllerNormalSpeed.Value = model.Camera.ControllerNormalSpeed;
            MouseNormalSensitivity.Value = model.Camera.MouseNormalSensitivity;

            Brightness.Value = model.Graphics.BrightnessValue;

            ShowCenterDot.Value = model.Accessibility.ShowCenterDot;
        }

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

            ShowCenterDot.Skip(1).Subscribe(val => { _editingSettings.Accessibility.ShowCenterDot = val; ApplyPreview(); }).AddTo(_editDisposables);

            // 自動セーブのDebounce
            Observable.Merge(
                MasterVolume.Skip(1).Select(_ => Unit.Default),
                BgmVolume.Skip(1).Select(_ => Unit.Default),
                SeVolume.Skip(1).Select(_ => Unit.Default),
                VoiceVolume.Skip(1).Select(_ => Unit.Default),
                ControllerNormalSpeed.Skip(1).Select(_ => Unit.Default),
                MouseNormalSensitivity.Skip(1).Select(_ => Unit.Default),
                Brightness.Skip(1).Select(_ => Unit.Default),
                ShowCenterDot.Skip(1).Select(_ => Unit.Default)
            )
            .Debounce(TimeSpan.FromSeconds(0.5f))
            .Subscribe(_ => AutoSave())
            .AddTo(_editDisposables);
        }

        private void ApplyPreview()
        {
            if (_editingSettings != null)
            {
                _applier.ApplyAll(_editingSettings);
            }
        }

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
        }

        public bool IsConsolePlatform()
        {
#if UNITY_SWITCH && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }
}
