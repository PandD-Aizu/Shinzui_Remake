using System.Collections;
using Shinzui.View.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shinzui.View.Objective
{
    /// <summary>
    /// 画面右上に現在の目標をアイコン付きで表示するUI View
    /// 子要素が未構築の場合は実行時に自動生成する
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ObjectiveView : MonoBehaviour
    {
        [Header("Assets")]
        [SerializeField] private Sprite icon;
        [Tooltip("未指定時は同じCanvas内のTMPテキストのフォントを流用")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Layout")]
        [SerializeField] private Vector2 screenMargin = new(48f, 48f);
        [SerializeField] private float height = 52f;
        [SerializeField] private float iconSize = 30f;
        [SerializeField] private float fontSize = 24f;

        [Header("Animation")]
        [SerializeField] private float fadeDuration = 0.45f;
        [SerializeField] private float slideDistance = 28f;

        private CanvasGroup _canvasGroup;
        private RectTransform _root;
        private Vector2 _basePosition;
        private Image _iconImage;
        private TMP_Text _label;
        private CanvasGroup _tutorialBody;
        private Coroutine _transition;
        private string _currentText = string.Empty;

        /// <summary>
        /// 自動生成時にアイコンとフォントを設定する
        /// </summary>
        /// <param name="iconSprite">目標アイコン</param>
        /// <param name="fontAsset">表示フォント 未指定可</param>
        public void Configure(Sprite iconSprite, TMP_FontAsset fontAsset = null)
        {
            icon = iconSprite;
            if (fontAsset != null) font = fontAsset;
            if (_iconImage != null) ApplyIcon();
        }

        /// <summary>表示要素を初期化する</summary>
        private void Awake()
        {
            EnsureBuilt();
        }

        /// <summary>同じ意匠の操作案内を画面左中央に生成する</summary>
        /// <returns>操作案内用の表示</returns>
        public ObjectiveView CreateTutorialView()
        {
            EnsureBuilt();

            // 非アクティブで設定を引き継いでから表示要素を生成する
            var tutorialObject = new GameObject("ControlTutorialHud", typeof(RectTransform));
            tutorialObject.SetActive(false);
            tutorialObject.transform.SetParent(transform.parent, false);
            var tutorial = tutorialObject.AddComponent<ObjectiveView>();
            tutorial.icon = icon;
            tutorial.font = _label.font;
            tutorial.height = Mathf.Max(height, fontSize * 4f + 32f);
            tutorial.iconSize = iconSize;
            tutorial.fontSize = fontSize;
            tutorial.fadeDuration = fadeDuration;
            tutorial.slideDistance = 0f;
            tutorial.EnsureBuilt();

            // 左中央を基準にし、アイコンを文言の左側へ配置する
            tutorial._root.anchorMin = tutorial._root.anchorMax = new Vector2(0f, 0.5f);
            tutorial._root.pivot = new Vector2(0f, 0.5f);
            tutorial._basePosition = new Vector2(screenMargin.x, 0f);
            tutorial._root.anchoredPosition = tutorial._basePosition;
            var content = tutorial._label.transform.parent;
            content.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().padding = new RectOffset(16, 40, 0, 4);
            tutorial._iconImage.transform.SetSiblingIndex(tutorial._label.transform.GetSiblingIndex());

            // 操作案内の下線は左から右へ薄くする
            content.Find("Underline").localScale = Vector3.one;

            // 操作案内のアイコンは左へ90度回転する
            tutorial._iconImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
            tutorial.BuildTutorialText();
            tutorialObject.SetActive(true);
            return tutorial;
        }

        /// <summary>タイトルと本文を縦に並べ、本文の透明度を個別に制御する</summary>
        private void BuildTutorialText()
        {
            // 既存の本文を縦並びのコンテナへ移す
            var textColumn = new GameObject("TutorialText", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup));
            textColumn.transform.SetParent(_label.transform.parent, false);
            var layout = textColumn.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // アイコンとタイトルを同じ行に配置する
            var header = new GameObject("TutorialHeader", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
            header.transform.SetParent(textColumn.transform, false);
            var headerLayout = header.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            headerLayout.spacing = 12f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;
            _iconImage.transform.SetParent(header.transform, false);

            // タイトルはアイコンや背景と一緒にフェードインする
            var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            title.transform.SetParent(header.transform, false);
            title.font = _label.font;
            title.fontSize = fontSize;
            title.color = _label.color;
            title.characterSpacing = _label.characterSpacing;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.raycastTarget = false;
            title.text = "操作チュートリアル";

            // タイトルと本文の間に左から右へ薄くなる区切り線を引く
            var separator = RequiemUiTheme.Decoration("TitleSeparator", textColumn.transform,
                new Color(RequiemUiTheme.Text.r, RequiemUiTheme.Text.g, RequiemUiTheme.Text.b, 0.55f), RequiemUiTheme.FadeRight);
            var separatorLayout = separator.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            separatorLayout.minHeight = 1.5f;
            separatorLayout.preferredHeight = 1.5f;
            separatorLayout.flexibleWidth = 1f;

            // 本文はタイトルの表示完了後にフェードインする
            _label.transform.SetParent(textColumn.transform, false);
            var body = new GameObject("TutorialBody", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(CanvasGroup));
            body.transform.SetParent(textColumn.transform, false);
            var bodyLayout = body.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            bodyLayout.spacing = 6f;
            bodyLayout.childControlWidth = bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = bodyLayout.childForceExpandHeight = false;
            _label.gameObject.SetActive(false);
            _tutorialBody = body.GetComponent<CanvasGroup>();
            _tutorialBody.alpha = 0f;
            _tutorialBody.interactable = false;
            _tutorialBody.blocksRaycasts = false;
        }

        /// <summary>操作案内の角括弧内のキー名をKenneyの入力アイコンとして表示する</summary>
        /// <param name="text">[Ctrl]や[W][A][S][D]を含む表示文言</param>
        private void SetLabelText(string text)
        {
            _label.text = text;
            if (_tutorialBody == null) return;

            // 古い行をレイアウトから外して破棄する
            foreach (Transform child in _tutorialBody.transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            // 改行ごとにキーキャップと説明を横並びにする
            foreach (var line in text.Split('\n'))
            {
                var row = new GameObject("InstructionRow", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
                row.transform.SetParent(_tutorialBody.transform, false);
                var rowLayout = row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                rowLayout.spacing = 6f;
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.childControlWidth = rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;

                foreach (var part in System.Text.RegularExpressions.Regex.Split(line, @"(\[[^\[\]\r\n]+\])"))
                {
                    if (part.Length == 0) continue;
                    bool isKey = part.StartsWith("[") && part.EndsWith("]");
                    string keyName = isKey ? part.Substring(1, part.Length - 2) : string.Empty;
                    var keySprite = isKey ? Resources.Load<Sprite>("InputPrompts/keyboard_" + keyName.ToLowerInvariant()) : null;

                    // 対応するキーはKenneyの画像を縦横比を保って表示する
                    if (keySprite != null)
                    {
                        var keyImage = RequiemUiTheme.Decoration("Key_" + keyName, row.transform, Color.white, keySprite);
                        keyImage.preserveAspect = true;
                        var keyLayout = keyImage.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                        keyLayout.preferredHeight = fontSize * 1.8f;
                        keyLayout.preferredWidth = keyLayout.preferredHeight * keySprite.rect.width / keySprite.rect.height;
                        continue;
                    }

                    // 説明と未登録のキーは文字で表示する
                    var label = Instantiate(_label, row.transform, false);
                    label.name = "Description";
                    label.text = part;
                    label.gameObject.SetActive(true);
                }
            }
        }

        /// <summary>
        /// 目標を表示する 表示中なら一度消してから差し替える
        /// </summary>
        /// <param name="text">目標文言</param>
        public void Show(string text)
        {
            EnsureBuilt();
            if (_currentText == text && _canvasGroup.alpha > 0f) return;
            _currentText = text;
            Restart(ShowRoutine(text));
        }

        /// <summary>
        /// 目標表示をフェードアウトして隠す
        /// </summary>
        public void Hide()
        {
            EnsureBuilt();
            _currentText = string.Empty;
            Restart(FadeRoutine(0f));
        }

        /// <summary>
        /// 実行中の演出を止めて新しい演出を開始する
        /// </summary>
        /// <param name="routine">開始する演出</param>
        private void Restart(IEnumerator routine)
        {
            if (_transition != null) StopCoroutine(_transition);

            // 非アクティブ時はコルーチンが使えないため即時反映
            if (!isActiveAndEnabled)
            {
                _canvasGroup.alpha = string.IsNullOrEmpty(_currentText) ? 0f : 1f;
                SetLabelText(_currentText);
                if (_tutorialBody != null) _tutorialBody.alpha = 1f;
                _root.anchoredPosition = _basePosition;
                return;
            }

            _transition = StartCoroutine(routine);
        }

        /// <summary>
        /// 前の目標を消してから新しい目標を右からスライドインさせる
        /// </summary>
        /// <param name="text">目標文言</param>
        /// <returns>コルーチン</returns>
        private IEnumerator ShowRoutine(string text)
        {
            // 表示中の目標があれば先に消す
            if (_canvasGroup.alpha > 0f) yield return FadeRoutine(0f);

            // 文言を差し替えて右側からフェードイン
            SetLabelText(text);
            if (_tutorialBody != null) _tutorialBody.alpha = 0f;
            _root.anchoredPosition = _basePosition + new Vector2(slideDistance, 0f);
            yield return FadeRoutine(1f);

            // タイトルの表示が完了してから本文を表示する
            if (_tutorialBody != null) yield return FadeTutorialBodyRoutine();
            _transition = null;
        }

        /// <summary>タイトルを表示したまま本文だけをフェードインする</summary>
        /// <returns>本文のフェード演出</returns>
        private IEnumerator FadeTutorialBodyRoutine()
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration);
                _tutorialBody.alpha = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                yield return null;
            }
            _tutorialBody.alpha = 1f;
        }

        /// <summary>
        /// 透明度と横位置を目標値へ補間する
        /// </summary>
        /// <param name="targetAlpha">目標の透明度</param>
        /// <returns>コルーチン</returns>
        private IEnumerator FadeRoutine(float targetAlpha)
        {
            float startAlpha = _canvasGroup.alpha;
            float startX = _root.anchoredPosition.x - _basePosition.x;
            float targetX = targetAlpha > 0f ? 0f : -slideDistance * 0.5f;
            float t = 0f;

            // ポーズ中も演出が止まらないよう非スケール時間で補間
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration);
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
                _root.anchoredPosition = _basePosition + new Vector2(Mathf.Lerp(startX, targetX, eased), 0f);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
            _root.anchoredPosition = _basePosition + new Vector2(targetX, 0f);
        }

        /// <summary>
        /// 右上配置の背景・下線・アイコン・文言を生成する
        /// </summary>
        private void EnsureBuilt()
        {
            if (_canvasGroup != null) return;

            // ルートを右上に固定し、幅は内容に合わせて伸縮させる
            _root = (RectTransform)transform;
            _root.anchorMin = Vector2.one;
            _root.anchorMax = Vector2.one;
            _root.pivot = Vector2.one;
            _basePosition = new Vector2(-screenMargin.x, -screenMargin.y);
            _root.anchoredPosition = _basePosition;
            _root.sizeDelta = new Vector2(_root.sizeDelta.x, height);

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            // 表示フォントは生成前に決めておく
            var labelFont = font != null ? font : FindCanvasFont();

            // アイコンと文言を横並びにするコンテナ
            var content = RequiemUiTheme.Stretch("Content", transform);
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            // 全体幅を保ち、右余白を左へ移してアイコンを下線の右端に揃える
            layout.padding = new RectOffset(56, 0, 0, 4);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // 背景は左が濃く右へ抜ける黒の帯
            var background = RequiemUiTheme.Decoration("Background", content, new Color(0f, 0f, 0f, 0.6f), RequiemUiTheme.FadeRight);
            background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // 下端に右から左へ薄くなる細い区切り線を引く
            var underline = RequiemUiTheme.Decoration("Underline", content, new Color(RequiemUiTheme.Text.r, RequiemUiTheme.Text.g, RequiemUiTheme.Text.b, 0.55f), RequiemUiTheme.FadeRight);
            underline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            underline.rectTransform.anchorMax = new Vector2(1f, 0f);
            underline.rectTransform.pivot = new Vector2(0.5f, 0f);
            underline.rectTransform.sizeDelta = new Vector2(0f, 1.5f);
            underline.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            // アイコンは右へ90度回転して使う
            _iconImage = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _iconImage.transform.SetParent(content, false);
            _iconImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -90f);
            _iconImage.preserveAspect = true;
            _iconImage.raycastTarget = false;
            _iconImage.color = RequiemUiTheme.Text;
            var iconLayout = _iconImage.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = iconSize;
            iconLayout.preferredHeight = iconSize;
            ApplyIcon();

            // 目標文言
            _label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            _label.transform.SetParent(content, false);
            _label.font = labelFont;
            _label.fontSize = fontSize;
            _label.color = RequiemUiTheme.Text;
            _label.characterSpacing = 6f;
            _label.alignment = TextAlignmentOptions.MidlineLeft;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.raycastTarget = false;
            _label.text = string.Empty;

            // アイコンを目標文言の右側に配置する
            _iconImage.transform.SetAsLastSibling();

            // ルート幅を内容の幅に合わせる
            var rootLayout = gameObject.AddComponent<HorizontalLayoutGroup>();
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = false;
            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>
        /// アイコン画像を反映し、未設定なら非表示にする
        /// </summary>
        private void ApplyIcon()
        {
            _iconImage.sprite = icon;
            _iconImage.gameObject.SetActive(icon != null);
        }

        /// <summary>
        /// 同じCanvas内の既存テキストからフォントを取得する
        /// </summary>
        /// <returns>見つかったフォント 見つからなければnull</returns>
        private TMP_FontAsset FindCanvasFont()
        {
            var canvas = GetComponentInParent<Canvas>();
            var text = canvas != null ? canvas.GetComponentInChildren<TMP_Text>(true) : null;
            return text != null ? text.font : null;
        }
    }
}
