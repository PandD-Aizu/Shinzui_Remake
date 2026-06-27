using System.Collections;
using UnityEngine;
using TMPro;

namespace Shinzui.View.Interaction
{
    /// <summary>
    /// 画面下部にメッセージテロップを表示・時間消去するUI View
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

        private void Start()
        {
            if (messageText != null)
            {
                messageText.text = string.Empty;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            if (canvasGroup != null)
            {
                // アルファ値の滑らかな補間 (フェードイン・フェードアウト)
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _targetAlpha, Time.deltaTime * fadeSpeed);
            }
        }

        /// <summary>
        /// 指定されたメッセージを下部テロップに表示
        /// </summary>
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

        private IEnumerator DisplayTimerCoroutine()
        {
            yield return new WaitForSeconds(displayDuration);

            // 表示時間が終わったらフェードアウト開始
            _targetAlpha = 0f;
            _displayCoroutine = null;
        }
    }
}
