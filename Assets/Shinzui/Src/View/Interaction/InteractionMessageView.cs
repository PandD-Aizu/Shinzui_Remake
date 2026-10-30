using System.Collections;
using UnityEngine;
using TMPro;
using Shinzui.View.UI;

namespace Shinzui.View.Interaction
{
    /// <summary>
    /// 画面右中央に操作結果の通知を表示し、一定時間後に消すUI View
    /// </summary>
    public class InteractionMessageView : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private CanvasGroup canvasGroup; // フェード用

        [Header("Settings")]
        [SerializeField] private float displayDuration = 4.0f;
        [SerializeField] private float fadeSpeed = 5.0f;

        private Coroutine _displayCoroutine;
        private float _targetAlpha = 0f;

        /// <summary>既存のテキストを右中央の通知パネルへ移す</summary>
        private void Awake()
        {
            if (messageText == null) return;

            // 下部の表示を空にし、同じCanvasに新しい通知の配置領域を設ける
            messageText.text = string.Empty;
            var canvas = messageText.GetComponentInParent<Canvas>();
            var panel = new GameObject("InteractionNotification", typeof(RectTransform), typeof(CanvasGroup));
            panel.transform.SetParent(canvas.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-48f, 0f);
            rect.sizeDelta = new Vector2(460f, 0f);

            // チュートリアルと同じ背景と余白を用い、高さを本文に合わせる
            var layout = panel.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 10f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = panel.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var background = RequiemUiTheme.Decoration("Background", rect, new Color(0f, 0f, 0f, 0.6f), RequiemUiTheme.FadeRight);
            background.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;

            // 既存の本文を移動するため、下部へ同じ通知が重複表示されない
            messageText.transform.SetParent(rect, false);
            messageText.color = RequiemUiTheme.Text;
            messageText.fontSize = 24f;
            messageText.characterSpacing = 6f;
            messageText.alignment = TextAlignmentOptions.MidlineRight;
            messageText.textWrappingMode = TextWrappingModes.Normal;
            messageText.raycastTarget = false;
            var line = RequiemUiTheme.Decoration("Underline", rect,
                new Color(RequiemUiTheme.Text.r, RequiemUiTheme.Text.g, RequiemUiTheme.Text.b, 0.55f), RequiemUiTheme.FadeRight);
            var lineLayout = line.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            lineLayout.minHeight = lineLayout.preferredHeight = 1.5f;

            // 旧パネルを非表示にし、新しい通知全体をフェード対象にする
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            canvasGroup = panel.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        /// <summary>通知全体の透明度を更新する</summary>
        private void Update()
        {
            if (canvasGroup != null)
            {
                // アルファ値の滑らかな補間 (フェードイン・フェードアウト)
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _targetAlpha, Time.deltaTime * fadeSpeed);
            }
        }

        /// <summary>
        /// 指定されたメッセージを右中央の通知に表示する
        /// </summary>
        /// <param name="message">取得アイテム名や操作結果の文言</param>
        public void ShowMessage(string message)
        {
            if (messageText == null) return;

            if (_displayCoroutine != null)
            {
                StopCoroutine(_displayCoroutine);
            }

            messageText.text = message;
            _targetAlpha = 1f;

            _displayCoroutine = StartCoroutine(DisplayTimerCoroutine());
        }

        /// <summary>表示時間の経過後に通知を消す</summary>
        /// <returns>表示待機のコルーチン</returns>
        private IEnumerator DisplayTimerCoroutine()
        {
            yield return new WaitForSeconds(displayDuration);

            // 表示時間が終わったらフェードアウト開始
            _targetAlpha = 0f;
            _displayCoroutine = null;
        }
    }
}
