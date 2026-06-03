using System;
using System.Collections.Generic;
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

        [Header("Accessibility UI")]
        [SerializeField] private Toggle centerDotToggle;



        private readonly Dictionary<string, GameObject> _categoryPanelMap = new(StringComparer.OrdinalIgnoreCase);
        private bool _categoryPanelsInitialized;
        
        // --- 外部（Presenter）公開用のプロパティ ---
        public RectTransform CategoryTabParent => categoryTabParent;
        public GameObject CategoryTabPrefab => categoryTabPrefab;

        public RectTransform CategoryPanelParent => categoryPanelParent;

        public Slider MasterVolumeSlider => masterVolumeSlider;
        public Slider BgmVolumeSlider => bgmVolumeSlider;
        public Slider SeVolumeSlider => seVolumeSlider;
        public Slider VoiceVolumeSlider => voiceVolumeSlider;

        public Slider ControllerSpeedSlider => controllerSpeedSlider;
        public Slider MouseSensitivitySlider => mouseSensitivitySlider;

        public Slider BrightnessSlider => brightnessSlider;

        public Toggle CenterDotToggle => centerDotToggle;



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
