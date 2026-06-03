using System;
using System.Collections.Generic;
using R3;
using Shinzui.Application.Settings;
using Shinzui.Domain.Settings;

namespace Shinzui.Presentation.Settings
{
    /// <summary>
    /// オプション設定画面のプレゼンテーションロジックを提供するViewModel。
    /// R3のReactivePropertyを活用し、View（UIコンポーネント）とモデルの状態を疎結合に結びつけます。
    /// </summary>
    public class OptionViewModel : IDisposable
    {
        private readonly SettingsUseCase _useCase;
        private readonly CompositeDisposable _disposables = new();

        // 現在選択中のカテゴリーインデックス
        private readonly ReactiveProperty<int> _selectedCategoryIndex = new(0);
        public ReadOnlyReactiveProperty<int> SelectedCategoryIndex => _selectedCategoryIndex;

        // UI側に表示する有効なカテゴリー名のリスト（プラットフォームで動的に変化）
        private readonly List<string> _availableCategories = new();
        public IReadOnlyList<string> AvailableCategories => _availableCategories;

        // 編集中の設定データのドラフト（即時反映プレビュー用）
        private GameSettings _draftSettings;

        // --- 各設定値をViewにバインドするためのリアクティブ・プロパティ ---
        // (UIの表示更新やスライダーの即時変更をバインド)
        
        // 音量関連
        public ReactiveProperty<float> MasterVolume { get; } = new();
        public ReactiveProperty<float> BgmVolume { get; } = new();
        public ReactiveProperty<float> SeVolume { get; } = new();
        public ReactiveProperty<float> VoiceVolume { get; } = new();

        // カメラ・マウス感度
        public ReactiveProperty<float> ControllerNormalSpeed { get; } = new();
        public ReactiveProperty<float> MouseNormalSensitivity { get; } = new();

        // グラフィックス・明るさ
        public ReactiveProperty<float> Brightness { get; } = new();

        // アクセシビリティ
        public ReactiveProperty<bool> ShowCenterDot { get; } = new();

        public OptionViewModel(SettingsUseCase useCase)
        {
            _useCase = useCase;

            // 1. プラットフォームに基づくカテゴリーリストの初期化
            SetupCategories();

            // 2. 編集セッションの開始
            _useCase.BeginEdit();
            _draftSettings = _useCase.GetEditingSettings();

            // 3. 各ReactivePropertyの初期値セット＆変更時のドラフト反映バインド
            InitializeBindings();
        }

        private void SetupCategories()
        {
            // デフォルトの全カテゴリー
            _availableCategories.Add("Controls");
            _availableCategories.Add("Camera");
            _availableCategories.Add("Game Settings");

            // Nintendo Switchなどのコンソール機では「Graphics（PC画質詳細）」をスキップし、
            // 明るさや画面調整のみに限定するか、カテゴリー自体を非表示にします。
            if (!_useCase.IsConsolePlatform())
            {
                _availableCategories.Add("Graphics");
            }
            else
            {
                // 代わりに「Display (Switch用明るさ等の簡易画面設定)」と名付けても良い
                _availableCategories.Add("Display");
            }

            _availableCategories.Add("Audio");
            _availableCategories.Add("Language");
            _availableCategories.Add("Accessibility");
        }

        private void InitializeBindings()
        {
            // モデル -> ViewModel (初期値読み込み)
            MasterVolume.Value = _draftSettings.Audio.SystemVolume;
            BgmVolume.Value = _draftSettings.Audio.BgmVolume;
            SeVolume.Value = _draftSettings.Audio.SeVolume;
            VoiceVolume.Value = _draftSettings.Audio.VoiceVolume;

            ControllerNormalSpeed.Value = _draftSettings.Camera.ControllerNormalSpeed;
            MouseNormalSensitivity.Value = _draftSettings.Camera.MouseNormalSensitivity;

            Brightness.Value = _draftSettings.Graphics.BrightnessValue;
            ShowCenterDot.Value = _draftSettings.Accessibility.ShowCenterDot;

            // ViewModel -> Model (値がスライダー等で変更されたら、ドラフトを更新して即時プレビュー適用)
            MasterVolume.Skip(1).Subscribe(val => { _draftSettings.Audio.SystemVolume = val; ApplyPreview(); }).AddTo(ref _disposables);
            BgmVolume.Skip(1).Subscribe(val => { _draftSettings.Audio.BgmVolume = val; ApplyPreview(); }).AddTo(ref _disposables);
            SeVolume.Skip(1).Subscribe(val => { _draftSettings.Audio.SeVolume = val; ApplyPreview(); }).AddTo(ref _disposables);
            VoiceVolume.Skip(1).Subscribe(val => { _draftSettings.Audio.VoiceVolume = val; ApplyPreview(); }).AddTo(ref _disposables);

            ControllerNormalSpeed.Skip(1).Subscribe(val => { _draftSettings.Camera.ControllerNormalSpeed = val; ApplyPreview(); }).AddTo(ref _disposables);
            MouseNormalSensitivity.Skip(1).Subscribe(val => { _draftSettings.Camera.MouseNormalSensitivity = val; ApplyPreview(); }).AddTo(ref _disposables);

            Brightness.Skip(1).Subscribe(val => { _draftSettings.Graphics.BrightnessValue = val; ApplyPreview(); }).AddTo(ref _disposables);
            ShowCenterDot.Skip(1).Subscribe(val => { _draftSettings.Accessibility.ShowCenterDot = val; ApplyPreview(); }).AddTo(ref _disposables);
        }

        /// <summary>
        /// カテゴリーを選択します（タブ遷移）
        /// </summary>
        public void SelectCategory(int index)
        {
            if (index >= 0 && index < _availableCategories.Count)
            {
                _selectedCategoryIndex.Value = index;
            }
        }

        /// <summary>
        /// ドラフトの変更をゲーム世界に即時プレビューします
        /// </summary>
        private void ApplyPreview()
        {
            _useCase.UpdateEditingSettings(_draftSettings);
        }

        /// <summary>
        /// 変更内容をセーブして確定し、オプション編集を完了します。
        /// </summary>
        public void SaveAndClose()
        {
            _useCase.SaveAndApply();
        }

        /// <summary>
        /// 変更を保存せずに破棄し、元の設定に戻してオプション編集を完了します。
        /// </summary>
        public void CancelAndClose()
        {
            _useCase.CancelEdit();
        }

        /// <summary>
        /// 全設定をデフォルト（初期値）にリセットします。
        /// </summary>
        public void ResetToDefault()
        {
            _useCase.ResetToDefault();
            _draftSettings = _useCase.GetEditingSettings();

            // 各プロパティに値を再通知し、UI表現をリセットする
            MasterVolume.Value = _draftSettings.Audio.SystemVolume;
            BgmVolume.Value = _draftSettings.Audio.BgmVolume;
            SeVolume.Value = _draftSettings.Audio.SeVolume;
            VoiceVolume.Value = _draftSettings.Audio.VoiceVolume;

            ControllerNormalSpeed.Value = _draftSettings.Camera.ControllerNormalSpeed;
            MouseNormalSensitivity.Value = _draftSettings.Camera.MouseNormalSensitivity;

            Brightness.Value = _draftSettings.Graphics.BrightnessValue;
            ShowCenterDot.Value = _draftSettings.Accessibility.ShowCenterDot;
        }

        public void Dispose()
        {
            _disposables.Dispose();
            _selectedCategoryIndex.Dispose();
            MasterVolume.Dispose();
            BgmVolume.Dispose();
            SeVolume.Dispose();
            VoiceVolume.Dispose();
            ControllerNormalSpeed.Dispose();
            MouseNormalSensitivity.Dispose();
            Brightness.Dispose();
            ShowCenterDot.Dispose();
        }
    }
}
