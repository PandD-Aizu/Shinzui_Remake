using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shinzui.Src.Title
{
    /// <summary>
    /// クレジット画面のUI表示を管理するビュークラス
    /// </summary>
    public class CreditScreenView : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private TextMeshProUGUI creditText;
        [SerializeField] private RectTransform contentTransform;
        [SerializeField] private Image backgroundPanel;

        [Header("Visual Settings (Common)")]
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private Color textColor = Color.white;

        [Header("TextMeshPro Settings")]
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private bool useAutoSizing = false;
        [SerializeField, Min(1f)] private float fontSize = 36f;
        [SerializeField, Min(1f)] private float autoSizeMin = 20f;
        [SerializeField, Min(1f)] private float autoSizeMax = 60f;
        [SerializeField] private TextAlignmentOptions alignment = TextAlignmentOptions.Top;
        [SerializeField] private bool enableWordWrapping = true;
        [SerializeField] private bool enableRichText = true;
        [SerializeField] private bool enableKerning = true;
        [SerializeField] private TextOverflowModes overflowMode = TextOverflowModes.Overflow;
        [SerializeField] private float lineSpacing = 0f;          // 行間(追加量)
        [SerializeField] private float paragraphSpacing = 0f;     // 段落間
        [SerializeField] private float characterSpacing = 0f;     // 文字間
        [SerializeField] private float wordSpacing = 0f;          // 単語間
        [SerializeField] private Vector4 textMargins = Vector4.zero; // L,T,R,B

        [Header("Credit Layout")]
        [SerializeField] private float sectionSpacing = 50f;

        private float maxScrollPosition;

        private void OnEnable()
        {
            InitializeVisuals();
        }

        private void InitializeVisuals()
        {
            if (backgroundPanel != null)
                backgroundPanel.color = backgroundColor;

            if (creditText != null)
            {
                ApplyTMPSettings();
            }

            if (scrollRect != null)
            {
                scrollRect.vertical = true;
                scrollRect.horizontal = false;

                if (scrollRect.content == null && contentTransform != null)
                    scrollRect.content = contentTransform;

                FixScrollRectSizes();
            }
        }

        private void FixScrollRectSizes()
        {
            if (scrollRect == null) return;

            RectTransform scrollRectTransform = scrollRect.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;

            if (backgroundPanel != null)
            {
                RectTransform bgRect = backgroundPanel.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
            }

            if (scrollRect.viewport != null)
            {
                scrollRect.viewport.anchorMin = Vector2.zero;
                scrollRect.viewport.anchorMax = Vector2.one;
                scrollRect.viewport.offsetMin = Vector2.zero;
                scrollRect.viewport.offsetMax = Vector2.zero;
            }
        }

        /// <summary>
        /// TextMeshProUGUI の設定を適用
        /// </summary>
        private void ApplyTMPSettings()
        {
            if (creditText == null) return;

            if (fontAsset != null) creditText.font = fontAsset;

            creditText.enableAutoSizing = useAutoSizing;
            if (useAutoSizing)
            {
                creditText.fontSizeMin = autoSizeMin;
                creditText.fontSizeMax = autoSizeMax;
            }
            else
            {
                creditText.fontSize = fontSize;
            }

            creditText.color = textColor;
            creditText.alignment = alignment;
            creditText.enableWordWrapping = enableWordWrapping;
            creditText.richText = enableRichText;
            creditText.enableKerning = enableKerning;
            creditText.overflowMode = overflowMode;

            // 文字組み設定
            creditText.characterSpacing = characterSpacing;
            creditText.wordSpacing = wordSpacing;
            creditText.lineSpacing = lineSpacing;
            creditText.paragraphSpacing = paragraphSpacing;

            // マージン(L,T,R,B)
            creditText.margin = textMargins;
        }

        /// <summary>
        /// クレジットテキストを設定する
        /// </summary>
        public void SetCreditText(CreditData creditData)
        {
            if (creditText == null || creditData == null) return;

            // 設定変更がある前提で毎回反映
            ApplyTMPSettings();

            StringBuilder sb = new StringBuilder();

            // 必要最小の空行のみ
            sb.AppendLine();
            sb.AppendLine($"<b>{creditData.gameTitle}</b>");
            sb.AppendLine();

            foreach (var section in creditData.creditSections)
            {
                sb.AppendLine($"<b>{section.sectionTitle}</b>");
                sb.AppendLine();

                foreach (var credit in section.credits)
                {
                    sb.AppendLine(credit);
                }

                // 段落間隔に加え、セクション間の追加スペースを確保
                if (sectionSpacing > 0f)
                {
                    // 段落改行を1つ。視覚的な余白は paragraphSpacing と sectionSpacing の併用で表現
                    sb.AppendLine();
                }
            }

            sb.AppendLine("Thank You For Playing!");
            sb.AppendLine();

            creditText.text = sb.ToString();

            ForceContentSize();
            Canvas.ForceUpdateCanvases();
            CalculateMaxScrollPosition();
        }

        /// <summary>
        /// 外部から設定変更後にレイアウトを再適用したい場合に呼ぶ
        /// </summary>
        public void ApplyTextSettingsAndRelayout()
        {
            ApplyTMPSettings();
            ForceContentSize();
            Canvas.ForceUpdateCanvases();
            CalculateMaxScrollPosition();
            ResetScrollPosition();
        }

        private void ForceContentSize()
        {
            if (creditText == null || contentTransform == null) return;

            RectTransform textRect = creditText.GetComponent<RectTransform>();
            if (textRect == null) return;

            // 上揃えのアンカー/ピボットに統一（縦スクロールの基準を上に）
            contentTransform.anchorMin = new Vector2(0f, 1f);
            contentTransform.anchorMax = new Vector2(1f, 1f);
            contentTransform.pivot     = new Vector2(0.5f, 1f);

            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot     = new Vector2(0.5f, 1f);

            // テキストの実高を最新化
            creditText.ForceMeshUpdate();
            float preferredHeight = creditText.preferredHeight;

            float viewportHeight = 0f;
            if (scrollRect != null)
            {
                viewportHeight = scrollRect.viewport != null
                    ? scrollRect.viewport.rect.height
                    : scrollRect.GetComponent<RectTransform>().rect.height;
            }

            // viewport 分のリードイン／トレーリング余白を付与
            float leadTrail = viewportHeight;
            float contentHeight = Mathf.Max(preferredHeight + (leadTrail * 2f), viewportHeight, 800f);

            // content は余白込み、text はテキスト分のみ
            contentTransform.sizeDelta = new Vector2(0f, contentHeight);
            textRect.sizeDelta         = new Vector2(0f, preferredHeight);

            // テキストを上から viewport 分だけ下げる（下から出現）
            contentTransform.anchoredPosition = new Vector2(contentTransform.anchoredPosition.x, 0f);
            textRect.anchoredPosition         = new Vector2(textRect.anchoredPosition.x, -leadTrail);

            // Content Size Fitter があれば無効化
            var contentSizeFitter = contentTransform.GetComponent<ContentSizeFitter>();
            if (contentSizeFitter != null)
            {
                contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
        }

        private void CalculateMaxScrollPosition()
        {
            if (contentTransform == null || scrollRect == null) return;

            float contentHeight = contentTransform.rect.height;
            float viewportHeight = scrollRect.viewport != null
                ? scrollRect.viewport.rect.height
                : scrollRect.GetComponent<RectTransform>().rect.height;

            maxScrollPosition = Mathf.Max(0f, contentHeight - viewportHeight);
        }

        public void UpdateScrollPosition(float normalizedPosition)
        {
            if (scrollRect == null) return;

            float clampedPosition = Mathf.Clamp01(normalizedPosition);
            scrollRect.verticalNormalizedPosition = 1f - clampedPosition;
        }

        public float GetMaxScrollPosition() => maxScrollPosition;

        public void ResetScrollPosition()
        {
            if (scrollRect != null)
            {
                // 余白の先頭（真っ黒）から開始
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        public void EnableInteraction(bool enable)
        {
            if (scrollRect != null)
            {
                scrollRect.enabled = enable;
            }
        }
    }
}