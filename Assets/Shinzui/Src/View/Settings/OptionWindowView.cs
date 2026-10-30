using System;
using System.Collections.Generic;
using Shinzui.View.UI;
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
            public GameObject descriptionPanel;
            public GameObject selectedIndicator;
        }

        [Serializable]
        private sealed class SliderValueBinding
        {
            public Slider slider;
            public TMPro.TMP_Text label;
            [NonSerialized] public UnityEngine.Events.UnityAction<float> listener;
        }

        [SerializeField] private List<SliderValueBinding> sliderValues = new();

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
        [SerializeField] private Toggle dlssToggle;
        [SerializeField] private Button dlssQualityButton;
        [SerializeField] private TMPro.TMP_Text upscalingLabel;

        [Header("Accessibility UI")]
        [SerializeField] private Toggle centerDotToggle;



        private readonly Dictionary<string, GameObject> _categoryPanelMap = new(StringComparer.OrdinalIgnoreCase);
        private bool _categoryPanelsInitialized;
        private GameObject _graphicsOverlay;
        private GameObject _graphicsPanel;
        private GameObject _graphicsDescription;
        private GameObject _graphicsDetailDescription;
        private Button _graphicsButton;
        private GraphicsOptionsView _graphics;

        /// <summary>詳細なグラフィック設定画面、グラフィックのカテゴリーがない場合はnull</summary>
        /// <remarks>非表示のままPresenterから参照されても使えるよう、初回参照時に生成する</remarks>
        public GraphicsOptionsView Graphics
        {
            get
            {
                BuildGraphicsPage();
                return _graphics;
            }
        }

        /// <summary>詳細なグラフィック設定画面を表示中か</summary>
        public bool IsGraphicsOpen => _graphicsOverlay != null && _graphicsOverlay.activeSelf;

        /// <summary>詳細なグラフィック設定画面を開いたときに通知する</summary>
        public event Action GraphicsOpened;
        
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
        public Toggle DlssToggle => dlssToggle;
        public Button DlssQualityButton => dlssQualityButton;

        /// <summary>対応方式に合わせてアップスケーリングの設定行を表示する</summary>
        /// <param name="name">表示する方式名</param>
        /// <param name="available">対応方式を利用できるか</param>
        public void ConfigureUpscaling(string name, bool available)
        {
            // ラベルを含む行全体を隠し、非対応環境で空の設定行を残さない
            // 詳細なグラフィック設定画面がある場合はそちらへ集約する
            available &= Graphics == null;
            if (dlssToggle != null) dlssToggle.transform.parent.gameObject.SetActive(available);
            if (dlssQualityButton != null) dlssQualityButton.gameObject.SetActive(available);
            if (upscalingLabel != null) upscalingLabel.text = name;
        }

        /// <summary>品質の選択肢を表示用の文字列へ変換する</summary>
        /// <param name="quality">品質の番号</param>
        /// <param name="upscaler">方式名</param>
        /// <returns>現在の品質と切り替え方向を示すラベル</returns>
        public static string UpscalingQualityLabel(int quality, string upscaler)
        {
            string name = quality switch { 1 => "Balanced", 2 => "Performance", _ => "Quality" };
            return $"{upscaler}品質: {name}  >";
        }

        public Toggle CenterDotToggle => centerDotToggle;



        /// <summary>
        /// スライダーの数値表示を接続する
        /// </summary>
        private void Awake()
        {
            // 設定値の変更と数値ラベルを同期する
            foreach (var binding in sliderValues)
            {
                if (binding.slider == null || binding.label == null) continue;
                binding.listener = value => binding.label.text = value.ToString("0.##");
                binding.slider.onValueChanged.AddListener(binding.listener);
                binding.listener(binding.slider.value);
            }

            BuildGraphicsPage();
        }

        /// <summary>
        /// 詳細なグラフィック設定画面を開く
        /// </summary>
        public void OpenGraphics()
        {
            BuildGraphicsPage();
            if (_graphicsOverlay == null || IsGraphicsOpen) return;

            // カテゴリーと操作説明を残して一覧と説明欄だけを切り替える
            ShowCategoryPanel("Graphics");
            _graphicsPanel.SetActive(false);
            _graphicsDescription.SetActive(false);
            _graphicsOverlay.transform.SetAsLastSibling();
            _graphicsOverlay.SetActive(true);
            _graphicsDetailDescription.SetActive(true);
            GraphicsOpened?.Invoke();
            _graphics.Focus();
        }

        /// <summary>
        /// 詳細なグラフィック設定画面を閉じてグラフィックのカテゴリーへ戻る
        /// </summary>
        public void CloseGraphics()
        {
            CloseGraphics(true);
        }

        /// <summary>
        /// 詳細設定を閉じて一覧と説明欄を元に戻す
        /// </summary>
        /// <param name="restoreFocus">入口の行へ選択を戻すか</param>
        private void CloseGraphics(bool restoreFocus)
        {
            if (!IsGraphicsOpen) return;
            _graphicsOverlay.SetActive(false);
            _graphicsDetailDescription.SetActive(false);
            _graphicsPanel.SetActive(true);
            _graphicsDescription.SetActive(true);
            if (restoreFocus && isActiveAndEnabled) _graphicsButton.Select();
        }

        /// <summary>
        /// 画面を閉じた場合も次回は通常のカテゴリー表示から開く
        /// </summary>
        private void OnDisable()
        {
            CloseGraphics(false);
        }

        /// <summary>
        /// グラフィックのカテゴリーへ入口の行と既存レイアウト内の詳細設定を生成する
        /// </summary>
        private void BuildGraphicsPage()
        {
            if (_graphicsOverlay != null) return;
            if (!EnsureCategoryPanelsInitialized() || !_categoryPanelMap.TryGetValue("Graphics", out var panel) || panel == null) return;
            var binding = categoryPanels.Find(item => item != null && string.Equals(item.categoryId, "Graphics", StringComparison.OrdinalIgnoreCase));
            if (binding?.descriptionPanel == null) return;
            _graphicsPanel = panel;
            _graphicsDescription = binding.descriptionPanel;
            var font = panel.GetComponentInChildren<TMPro.TMP_Text>(true).font;

            // 既存の行と同じ高さで、選択時に赤い帯が出る入口の行
            var row = new GameObject("GraphicsDetailButton", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(panel.transform, false);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 82f;
            var image = row.gameObject.AddComponent<Image>();
            _graphicsButton = row.gameObject.AddComponent<Button>();
            _graphicsButton.targetGraphic = image;
            _graphicsButton.onClick.AddListener(OpenGraphics);
            var label = RequiemUiTheme.Stretch("Label", row).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            label.font = font;
            label.text = "詳細なグラフィック設定  <color=#85807A>プリセット・レイトレーシングなど</color>  ›";
            label.fontSize = 24f;
            label.raycastTarget = false;
            RequiemUiTheme.StyleRowButton(_graphicsButton, label);

            // 一覧のスクロール位置に影響されないよう既存Viewport内に配置する
            var list = RequiemUiTheme.Stretch("GraphicsDetails", categoryPanelParent.parent);
            var description = RequiemUiTheme.Stretch("GraphicsDetailDescription", _graphicsDescription.transform.parent);
            var source = (RectTransform)_graphicsDescription.transform;
            description.anchorMin = source.anchorMin;
            description.anchorMax = source.anchorMax;
            description.pivot = source.pivot;
            description.sizeDelta = source.sizeDelta;
            description.anchoredPosition = source.anchoredPosition;
            _graphics = GraphicsOptionsView.CreateEmbedded(list, description, font);
            _graphics.BackRequested += CloseGraphics;

            _graphicsOverlay = list.gameObject;
            _graphicsDetailDescription = description.gameObject;
            _graphicsOverlay.SetActive(false);
            _graphicsDetailDescription.SetActive(false);
        }

        /// <summary>
        /// 数値表示の購読を解除する
        /// </summary>
        private void OnDestroy()
        {
            foreach (var binding in sliderValues)
            {
                if (binding.slider != null && binding.listener != null)
                    binding.slider.onValueChanged.RemoveListener(binding.listener);
            }
        }

        /// <summary>
        /// カテゴリーに対応する設定と説明を表示する
        /// </summary>
        /// <param name="categoryId">表示するカテゴリーの識別子</param>
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

            // 詳細設定中もカテゴリーを直接切り替えられるよう表示を戻す
            CloseGraphics(false);

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

            // 説明欄とタブの選択表示を設定一覧に合わせる
            foreach (var binding in categoryPanels)
            {
                if (binding == null) continue;
                bool selected = string.Equals(binding.categoryId, categoryId, StringComparison.OrdinalIgnoreCase);
                if (binding.descriptionPanel != null) binding.descriptionPanel.SetActive(selected);
                if (binding.selectedIndicator != null) binding.selectedIndicator.SetActive(selected);
            }

            if (!found)
            {
                Debug.LogWarning($"[Settings] Category panel '{categoryId}' was not found.");
            }
        }

        /// <summary>
        /// カテゴリーとパネルの対応を初期化する
        /// </summary>
        /// <returns>初期化が完了した場合はtrue</returns>
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
