using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shinzui.View.UI
{
    /// <summary>
    /// 選択中の項目に赤い帯と明るい文字を表示する
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RequiemMenuItem : MonoBehaviour, IPointerEnterHandler
    {
        private const float FadeSpeed = 9f;
        private Selectable _target;
        private Graphic _highlight;
        private Graphic _marker;
        private TMP_Text _label;
        private float _highlightAlpha;
        private float _amount = -1f;
        private bool _interactable;

        /// <summary>
        /// 表示を切り替える対象を設定する
        /// </summary>
        /// <param name="target">選択対象</param>
        /// <param name="highlight">選択帯</param>
        /// <param name="marker">選択マーカー</param>
        /// <param name="label">色を切り替える文字</param>
        public void Initialize(Selectable target, Graphic highlight, Graphic marker, TMP_Text label)
        {
            _target = target;
            _highlight = highlight;
            _marker = marker;
            _label = label;
            _highlightAlpha = highlight != null ? highlight.color.a : 0f;
            _amount = -1f;
            Apply(0f);
        }

        /// <summary>
        /// マウスが重なった項目を選択状態にする
        /// </summary>
        /// <param name="eventData">ポインター情報</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_target != null && _target.IsInteractable()) _target.Select();
        }

        /// <summary>
        /// 選択状態に合わせて表示を滑らかに切り替える
        /// </summary>
        private void Update()
        {
            if (_target == null) return;
            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool interactable = _target.IsInteractable();
            float goal = interactable && current == _target.gameObject ? 1f : 0f;

            // 状態に変化がない場合は更新しない
            if (Mathf.Approximately(_amount, goal) && interactable == _interactable) return;
            _interactable = interactable;
            Apply(Mathf.MoveTowards(Mathf.Max(_amount, 0f), goal, Time.unscaledDeltaTime * FadeSpeed));
        }

        /// <summary>
        /// 選択度合いを各表示へ反映する
        /// </summary>
        /// <param name="amount">0から1の選択度合い</param>
        private void Apply(float amount)
        {
            _amount = amount;
            if (_highlight != null) _highlight.color = WithAlpha(_highlight.color, _highlightAlpha * amount);
            if (_marker != null) _marker.color = WithAlpha(_marker.color, amount);
            if (_label != null)
            {
                var rest = _target != null && !_target.IsInteractable() ? RequiemUiTheme.TextDisabled : RequiemUiTheme.Text;
                _label.color = Color.Lerp(rest, RequiemUiTheme.TextActive, amount);
            }
        }

        /// <summary>
        /// 不透明度だけを差し替えた色を返す
        /// </summary>
        /// <param name="color">元の色</param>
        /// <param name="alpha">不透明度</param>
        /// <returns>変更後の色</returns>
        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
