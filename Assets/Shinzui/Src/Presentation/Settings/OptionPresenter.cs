using System;
using System.Collections.Generic;
using R3;
using Shinzui.Application.UseCases;
using Shinzui.Domain.Settings;
using Shinzui.View.Settings;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

namespace Shinzui.Presentation.Settings
{
    public class OptionPresenter : IInitializable, IDisposable
    {
        private sealed class CategoryDefinition
        {
            public string Id { get; }
            public string Label { get; }

            public CategoryDefinition(string id, string label)
            {
                Id = id;
                Label = label;
            }
        }

        private readonly SettingsUseCase _useCase;
        private readonly OptionWindowView _view;
        private readonly CompositeDisposable _disposables = new();
        private readonly CompositeDisposable _upscalingDisposables = new();

        private readonly List<CategoryDefinition> _availableCategories = new();
        private readonly List<Button> _categoryButtons = new();
        private int _selectedCategoryIndex = 0;

        private GraphicsOptionsBinder _graphicsBinder;

        private Button _dynamicDefaultButton;
        private Button _dynamicCloseButton;

        public OptionPresenter(SettingsUseCase useCase, OptionWindowView view)
        {
            _useCase = useCase;
            _view = view;
        }

        public void Initialize()
        {
            // 1. 編集用ドラフトデータの準備
            _useCase.BeginEdit();

            // 2. プラットフォーム別のカテゴリー決定
            SetupCategories();

            // 3. UIとUseCaseの双方向バインディング
            BindUi();

            // 4. 初期カテゴリー選択
            SelectCategory(0);
        }

        private void SetupCategories()
        {
            _availableCategories.Clear();
            _availableCategories.Add(new CategoryDefinition("Controls", "Controls"));
            _availableCategories.Add(new CategoryDefinition("Camera", "Camera"));
            _availableCategories.Add(new CategoryDefinition("GameSettings", "Game Settings"));

            if (!_useCase.IsConsolePlatform())
            {
                _availableCategories.Add(new CategoryDefinition("Graphics", "Graphics"));
            }
            else
            {
                _availableCategories.Add(new CategoryDefinition("Graphics", "Display"));
            }

            _availableCategories.Add(new CategoryDefinition("Audio", "Audio"));
            _availableCategories.Add(new CategoryDefinition("Language", "Language"));
            _availableCategories.Add(new CategoryDefinition("Accessibility", "Accessibility"));

            // The title scene already has authored tabs and UnityEvents. Preserve them.
            if (!_view.GenerateCategoryTabs) return;
            if (_view.CategoryTabParent == null || _view.CategoryTabPrefab == null)
                throw new InvalidOperationException("Dynamic option tabs require a parent and prefab.");

            // タブの動的生成
            foreach (Transform child in _view.CategoryTabParent)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
            _categoryButtons.Clear();

            for (int i = 0; i < _availableCategories.Count; i++)
            {
                int index = i;
                var category = _availableCategories[i];

                GameObject tabObj = UnityEngine.Object.Instantiate(_view.CategoryTabPrefab, _view.CategoryTabParent);
                var btn = tabObj.GetComponent<Button>();
                var txt = tabObj.GetComponentInChildren<TMPro.TMP_Text>();

                if (txt != null)
                {
                    txt.text = category.Label;
                }

                if (btn != null)
                {
                    btn.onClick.AddListener(() => SelectCategory(index));
                    _categoryButtons.Add(btn);
                }
            }

            CreateDynamicActionButtons();
        }

        private void CreateDynamicActionButtons()
        {
            if (_dynamicDefaultButton != null) UnityEngine.Object.Destroy(_dynamicDefaultButton.gameObject);
            if (_dynamicCloseButton != null) UnityEngine.Object.Destroy(_dynamicCloseButton.gameObject);

            if (_view.CategoryTabPrefab != null && _view.CategoryTabParent != null)
            {
                // Defaultボタンの生成
                GameObject defaultObj = UnityEngine.Object.Instantiate(_view.CategoryTabPrefab, _view.CategoryTabParent);
                _dynamicDefaultButton = defaultObj.GetComponent<Button>();
                var defaultTxt = defaultObj.GetComponentInChildren<TMPro.TMP_Text>();
                if (defaultTxt != null)
                {
                    defaultTxt.text = "Default";
                }
                if (_dynamicDefaultButton != null)
                {
                    _dynamicDefaultButton.onClick.AddListener(() => _useCase.ResetToDefault());
                }

                // Closeボタンの生成
                GameObject closeObj = UnityEngine.Object.Instantiate(_view.CategoryTabPrefab, _view.CategoryTabParent);
                _dynamicCloseButton = closeObj.GetComponent<Button>();
                var closeTxt = closeObj.GetComponentInChildren<TMPro.TMP_Text>();
                if (closeTxt != null)
                {
                    closeTxt.text = "Close";
                }
                if (_dynamicCloseButton != null)
                {
                    _dynamicCloseButton.onClick.AddListener(() =>
                    {
                        _useCase.SaveAndApply();
                        _view.gameObject.SetActive(false);
                    });
                }
            }
        }

        /// <summary>設定画面の操作と編集値を接続する</summary>
        private void BindUi()
        {
            // 詳細画面がある場合は共通の接続処理へ委譲する
            if (_view.Graphics != null)
            {
                _view.ConfigureUpscaling(null, false);
                _graphicsBinder = new GraphicsOptionsBinder(_useCase, _view.Graphics);
                _view.GraphicsOpened += _graphicsBinder.Refresh;
            }
            else
            {
                // 既存のタイトル画面では従来のアップスケーリング操作を維持する
                BindUpscalingUi(_useCase.SupportedUpscaler);
                Observable.EveryUpdate().Select(_ => _useCase.SupportedUpscaler).DistinctUntilChanged()
                    .Subscribe(BindUpscalingUi).AddTo(_disposables);
            }

            BindStandardUi();
        }

        /// <summary>対応方式の保存値へアップスケーリングUIを接続する</summary>
        /// <param name="upscaler">現在利用できる方式</param>
        private void BindUpscalingUi(UpscalerType upscaler)
        {
            // 表示方式が変わる前に旧方式の購読を解除する
            _upscalingDisposables.Clear();
            string name = upscaler == UpscalerType.Fsr2 ? "FSR 2" : "DLSS";
            _view.ConfigureUpscaling(name, upscaler != UpscalerType.None);
            if (upscaler == UpscalerType.None) return;

            var enabled = _useCase.UpscalingEnabled;
            var quality = _useCase.UpscalingQuality;
            if (_view.DlssToggle != null)
            {
                enabled.Subscribe(val => _view.DlssToggle.isOn = val).AddTo(_upscalingDisposables);
                OnToggleValueChanged(_view.DlssToggle).Subscribe(val => enabled.Value = val).AddTo(_upscalingDisposables);
            }

            // 品質ボタンを押すたびにQuality、Balanced、Performanceを順番に選ぶ
            if (_view.DlssQualityButton != null)
            {
                var button = _view.DlssQualityButton;
                enabled.Subscribe(val => button.interactable = val).AddTo(_upscalingDisposables);
                quality.Subscribe(val => button.GetComponentInChildren<TMPro.TMP_Text>().text =
                    OptionWindowView.UpscalingQualityLabel(val, name)).AddTo(_upscalingDisposables);
                Observable.FromEvent(h => button.onClick.AddListener(h.Invoke), h => button.onClick.RemoveListener(h.Invoke))
                    .Subscribe(_ => quality.Value = (quality.Value + 1) % 3).AddTo(_upscalingDisposables);
            }
        }

        /// <summary>既存の音量、カメラ、表示設定をUIへ接続する</summary>
        private void BindStandardUi()
        {
            // --- UseCase -> View のバインディング (UseCaseのプロパティ値が変わったらUIへ反映する) ---
            _useCase.MasterVolume.Subscribe(val => _view.MasterVolumeSlider.value = val).AddTo(_disposables);
            _useCase.BgmVolume.Subscribe(val => _view.BgmVolumeSlider.value = val).AddTo(_disposables);
            _useCase.SeVolume.Subscribe(val => _view.SeVolumeSlider.value = val).AddTo(_disposables);
            _useCase.VoiceVolume.Subscribe(val => _view.VoiceVolumeSlider.value = val).AddTo(_disposables);

            _useCase.ControllerNormalSpeed.Subscribe(val => _view.ControllerSpeedSlider.value = val).AddTo(_disposables);
            _useCase.MouseNormalSensitivity.Subscribe(val => _view.MouseSensitivitySlider.value = val).AddTo(_disposables);

            _useCase.Brightness.Subscribe(val => _view.BrightnessSlider.value = val).AddTo(_disposables);

            _useCase.ShowCenterDot.Subscribe(val => _view.CenterDotToggle.isOn = val).AddTo(_disposables);

            // --- View -> UseCase のバインディング (UIの変更をUseCaseへ反映する) ---
            OnSliderValueChanged(_view.MasterVolumeSlider).Subscribe(val => _useCase.MasterVolume.Value = val).AddTo(_disposables);
            OnSliderValueChanged(_view.BgmVolumeSlider).Subscribe(val => _useCase.BgmVolume.Value = val).AddTo(_disposables);
            OnSliderValueChanged(_view.SeVolumeSlider).Subscribe(val => _useCase.SeVolume.Value = val).AddTo(_disposables);
            OnSliderValueChanged(_view.VoiceVolumeSlider).Subscribe(val => _useCase.VoiceVolume.Value = val).AddTo(_disposables);

            OnSliderValueChanged(_view.ControllerSpeedSlider).Subscribe(val => _useCase.ControllerNormalSpeed.Value = val).AddTo(_disposables);
            OnSliderValueChanged(_view.MouseSensitivitySlider).Subscribe(val => _useCase.MouseNormalSensitivity.Value = val).AddTo(_disposables);

            OnSliderValueChanged(_view.BrightnessSlider).Subscribe(val => _useCase.Brightness.Value = val).AddTo(_disposables);

            OnToggleValueChanged(_view.CenterDotToggle).Subscribe(val => _useCase.ShowCenterDot.Value = val).AddTo(_disposables);
        }

        private Observable<float> OnSliderValueChanged(Slider slider)
        {
            return Observable.FromEvent<float>(
                h => slider.onValueChanged.AddListener(h.Invoke),
                h => slider.onValueChanged.RemoveListener(h.Invoke)
            );
        }

        private Observable<bool> OnToggleValueChanged(Toggle toggle)
        {
            return Observable.FromEvent<bool>(
                h => toggle.onValueChanged.AddListener(h.Invoke),
                h => toggle.onValueChanged.RemoveListener(h.Invoke)
            );
        }

        private void SelectCategory(int index)
        {
            if (index < 0 || index >= _availableCategories.Count) return;

            _selectedCategoryIndex = index;
            var category = _availableCategories[index];

            _view.ShowCategoryPanel(category.Id);

            // タブハイライトの更新
            for (int i = 0; i < _categoryButtons.Count; i++)
            {
                var colors = _categoryButtons[i].colors;
                colors.normalColor = (i == _selectedCategoryIndex) ? Color.yellow : Color.white;
                _categoryButtons[i].colors = colors;
            }

            Debug.Log($"[Settings] Tab Switched to: {category.Label}");
        }

        /// <summary>設定画面の購読と操作イベントを解放する</summary>
        public void Dispose()
        {
            // 共通グラフィック画面の購読と再表示通知を解除する
            if (_graphicsBinder != null) _view.GraphicsOpened -= _graphicsBinder.Refresh;
            _graphicsBinder?.Dispose();

            _upscalingDisposables.Dispose();
            _disposables.Dispose();

            // onClickリスナーは GameObject 破棄時にクリアされますが、念のため明示的解除
            if (_dynamicDefaultButton != null) _dynamicDefaultButton.onClick.RemoveAllListeners();
            if (_dynamicCloseButton != null) _dynamicCloseButton.onClick.RemoveAllListeners();

            foreach (var btn in _categoryButtons)
            {
                if (btn != null) btn.onClick.RemoveAllListeners();
            }
        }
    }
}
