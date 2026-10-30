using UnityEngine;


namespace Shinzui.View
{
    /// <summary>隠れている間だけ表示する息止め用のスタミナゲージ</summary>
    public sealed class BreathGaugeView : MonoBehaviour
    {
        private UnityEngine.UI.Image _fill;
        /// <summary>小さな画面下部ゲージを作成</summary>
        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var panel = new GameObject("Breath", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .12f);
            rect.sizeDelta = new Vector2(220, 8);
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .7f);
            var fill = new GameObject("Remaining", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fill.transform.SetParent(panel.transform, false);
            _fill = fill.GetComponent<UnityEngine.UI.Image>();
            var fr = (RectTransform)fill.transform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            _fill.raycastTarget = false;
            panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var label = new GameObject("Controls", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            label.transform.SetParent(panel.transform, false);
            var lr = (RectTransform)label.transform; lr.sizeDelta = new Vector2(460, 35); lr.anchoredPosition = new Vector2(0, 24);
            var text = label.GetComponent<UnityEngine.UI.Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "Hold RMB / LT: Hold breath     E / A: Exit"; text.alignment = TextAnchor.MiddleCenter; text.fontSize = 15;
            text.raycastTarget = false;
        }
        /// <summary>スタミナ残量と息止め状態を反映</summary>
        /// <param name="ratio">スタミナの残量割合</param>
        /// <param name="holding">息止め中か</param>
        public void Show(float ratio, bool holding)
        { _fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1); _fill.color = holding ? Color.cyan : Color.white; }
    }
}
