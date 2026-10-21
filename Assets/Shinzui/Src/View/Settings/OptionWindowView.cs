using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shinzui.View.Settings
{
    /// <summary>
    /// オプション設定画面のUIパーツを保持するViewコンポーネント
    /// </summary>
    public class OptionWindowView : MonoBehaviour
    {
        [Serializable]
        private sealed class CategoryPanelBinding
        {
            public string categoryId;
            public GameObject panel;
        }

        [Header("Category Navigation")]
        [SerializeField] private bool generateCategoryTabs = true;
        [SerializeField] private RectTransform categoryTabParent;
        [SerializeField] private GameObject categoryTabPrefab;

        [Header("Category Panels")]
        [SerializeField] private RectTransform categoryPanelParent;
        [SerializeField] private bool autoDiscoverCategoryPanels = true;
        [SerializeField] private List<CategoryPanelBinding> categoryPanels = new();

        [Header("Audio Settings UI")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Slider seVolumeSlider;
        [SerializeField] private Slider voiceVolumeSlider;

        [Header("Camera Settings UI")]
        [SerializeField] private Slider controllerSpeedSlider;
        [SerializeField] private Slider mouseSensitivitySlider;

        [Header("Graphics Settings UI")]
        [SerializeField] private Slider brightnessSlider;
        [SerializeField] private Slider graphicsQualitySlider;
        [SerializeField] private TMP_Text graphicsQualityLabel;

        [Header("Accessibility UI")]
        [SerializeField] private Toggle centerDotToggle;



        private readonly Dictionary<string, GameObject> _categoryPanelMap = new(StringComparer.OrdinalIgnoreCase);
        private bool _categoryPanelsInitialized;
        
        // --- 外部（Presenter）公開用のプロパティ ---
        public RectTransform CategoryTabParent => categoryTabParent;
        public bool GenerateCategoryTabs => generateCategoryTabs;
        public GameObject CategoryTabPrefab => categoryTabPrefab;

        public RectTransform CategoryPanelParent => categoryPanelParent;

        public Slider MasterVolumeSlider => masterVolumeSlider;
        public Slider BgmVolumeSlider => bgmVolumeSlider;
        public Slider SeVolumeSlider => seVolumeSlider;
        public Slider VoiceVolumeSlider => voiceVolumeSlider;

        public Slider ControllerSpeedSlider => controllerSpeedSlider;
        public Slider MouseSensitivitySlider => mouseSensitivitySlider;

        public Slider BrightnessSlider => brightnessSlider;
        public Slider GraphicsQualitySlider => graphicsQualitySlider;

        public Toggle CenterDotToggle => centerDotToggle;

        /// <summary>Extend the authored graphics panel with detailed settings</summary>
        /// <returns>The detail view, or null when no graphics panel is authored</returns>
        public GraphicsOptionsView EnsureGraphicsDetails()
        {
            if (!EnsureCategoryPanelsInitialized() || !_categoryPanelMap.TryGetValue("Graphics", out var panel)) return null;
            var existing = GetComponent<GraphicsOptionsView>();
            if (existing != null) return existing;
            var detail = gameObject.AddComponent<GraphicsOptionsView>();
            var label = brightnessSlider != null ? brightnessSlider.transform.parent.GetComponentInChildren<TMP_Text>(true) : null;
            detail.Build((RectTransform)panel.transform, label != null ? label.font : TMP_Settings.defaultFontAsset);
            return detail;
        }

        /// <summary>
        /// 既存の明るさ行の書式を利用して品質プリセットの選択行を用意する
        /// </summary>
        public void EnsureGraphicsQualitySelector()
        {
            if (graphicsQualitySlider != null || brightnessSlider == null || !EnsureCategoryPanelsInitialized()
                || !_categoryPanelMap.TryGetValue("Graphics", out var graphicsPanel))
            {
                return;
            }

            // グラフィックスパネル直下の行のみ複製し他のカテゴリーを含めない
            Transform brightnessRow = brightnessSlider.transform.parent;
            if (brightnessRow == null || brightnessRow.parent != graphicsPanel.transform)
            {
                return;
            }

            var qualityRow = Instantiate(brightnessRow.gameObject, graphicsPanel.transform);
            qualityRow.name = "Graphics Quality";
            qualityRow.transform.SetSiblingIndex(brightnessRow.GetSiblingIndex());
            graphicsQualitySlider = qualityRow.GetComponentInChildren<Slider>(true);
            graphicsQualitySlider.onValueChanged = new Slider.SliderEvent();
            graphicsQualitySlider.minValue = 0;
            graphicsQualitySlider.maxValue = 4;
            graphicsQualitySlider.wholeNumbers = true;
            graphicsQualityLabel = qualityRow.GetComponentInChildren<TMP_Text>(true);
            if (graphicsQualityLabel != null)
            {
                graphicsQualityLabel.enableAutoSizing = true;
            }
        }

        /// <summary>
        /// 品質選択の現在値とラベルをイベント再発火なしで更新する
        /// </summary>
        /// <param name="quality">品質番号 0: LOW、1: MEDIUM、2: HIGH、3: ULTRA</param>
        public void SetGraphicsQuality(int quality)
        {
            if (graphicsQualitySlider == null)
            {
                return;
            }

            // スライダー操作中も選択中のプリセットを読めるラベルに保つ
            graphicsQualitySlider.SetValueWithoutNotify(quality);
            if (graphicsQualityLabel != null)
            {
                string name = quality switch { 0 => "LOW", 1 => "MEDIUM", 2 => "HIGH", 3 => "ULTRA", _ => "CUSTOM" };
                graphicsQualityLabel.text = $"Graphics Quality: {name}";
            }
        }

        public void ShowCategoryPanel(string categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId))
            {
                return;
            }

            if (!EnsureCategoryPanelsInitialized() || _categoryPanelMap.Count == 0)
            {
                return;
            }

            bool found = false;
            foreach (var pair in _categoryPanelMap)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                bool isTarget = string.Equals(pair.Key, categoryId, StringComparison.OrdinalIgnoreCase);
                pair.Value.SetActive(isTarget);
                found |= isTarget;
            }

            if (!found)
            {
                Debug.LogWarning($"[Settings] Category panel '{categoryId}' was not found.");
            }
        }

        private bool EnsureCategoryPanelsInitialized()
        {
            if (_categoryPanelsInitialized)
            {
                return true;
            }

            _categoryPanelMap.Clear();

            if (categoryPanelParent == null)
            {
                var elements = transform.Find("Elements");
                if (elements != null)
                {
                    categoryPanelParent = elements.GetComponent<RectTransform>();
                }
            }

            if (categoryPanels != null)
            {
                foreach (var binding in categoryPanels)
                {
                    if (binding == null || binding.panel == null || string.IsNullOrWhiteSpace(binding.categoryId))
                    {
                        continue;
                    }

                    _categoryPanelMap[binding.categoryId] = binding.panel;
                }
            }

            if (autoDiscoverCategoryPanels && categoryPanelParent != null)
            {
                foreach (Transform child in categoryPanelParent)
                {
                    if (child == null)
                    {
                        continue;
                    }

                    if (!_categoryPanelMap.ContainsKey(child.name))
                    {
                        _categoryPanelMap[child.name] = child.gameObject;
                    }
                }
            }

            _categoryPanelsInitialized = true;
            return true;
        }
    }
}
