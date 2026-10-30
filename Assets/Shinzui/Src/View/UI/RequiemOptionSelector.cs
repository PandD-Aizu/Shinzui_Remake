using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shinzui.View.UI
{
    /// <summary>
    /// 左右の矢印または区切りボタンで選択肢を切り替える設定行
    /// </summary>
    public sealed class RequiemOptionSelector : Selectable, ISubmitHandler, IPointerClickHandler
    {
        private const float FlashDuration = 0.6f;
        private const float FlashDelay = 0.2f;

        private string[] _choices = Array.Empty<string>();
        private int _index = -1;
        private string _lockedText;
        private TMP_Text _value;
        private Graphic _left;
        private Graphic _right;
        private RectTransform _pips;
        private readonly List<Image> _pipImages = new();
        private readonly List<(Image box, TMP_Text text, Image rule)> _segments = new();
        private TMP_Text _customTag;
        private float _flashTime = -1f;
        private float _enabledAt;

        /// <summary>利用者の操作で選択肢が変わったときに通知する</summary>
        public event Action<int> ValueChanged;

        /// <summary>現在の選択位置、区切り表示では-1がどれにも当てはまらない状態</summary>
        public int Index => _index;

        /// <summary>選択肢の数</summary>
        public int Count => _choices.Length;

        /// <summary>
        /// 矢印形式の表示部品を設定する
        /// </summary>
        /// <param name="value">選択中の値を表示する文字</param>
        /// <param name="left">左矢印</param>
        /// <param name="right">右矢印</param>
        /// <param name="pips">段階表示の配置先、不要ならnull</param>
        public void InitializeArrows(TMP_Text value, Graphic left, Graphic right, RectTransform pips)
        {
            _value = value;
            _left = left;
            _right = right;
            _pips = pips;
            RequiemSelectorPart.Attach(left, this, -1);
            RequiemSelectorPart.Attach(right, this, 1);
        }

        /// <summary>
        /// 区切りボタン形式の表示部品を設定する
        /// </summary>
        /// <param name="segments">各選択肢の背景、文字、下線</param>
        /// <param name="customTag">どれにも当てはまらないときに表示する文字</param>
        public void InitializeSegments(IReadOnlyList<(Image box, TMP_Text text, Image rule)> segments, TMP_Text customTag)
        {
            _segments.Clear();
            _segments.AddRange(segments);
            _customTag = customTag;

            // 区切りを直接押してもその選択肢を選べるようにする
            for (int i = 0; i < segments.Count; i++) RequiemSelectorPart.Attach(segments[i].box, this, 0, i);
        }

        /// <summary>
        /// 選択肢を差し替える
        /// </summary>
        /// <param name="choices">表示する選択肢</param>
        public void SetChoices(IReadOnlyList<string> choices)
        {
            _choices = new string[choices.Count];
            for (int i = 0; i < choices.Count; i++) _choices[i] = choices[i];

            // 段階表示の数を選択肢に合わせる
            if (_pips != null)
            {
                while (_pipImages.Count < _choices.Length)
                {
                    var pip = RequiemUiTheme.Decoration("Pip", _pips, Color.white);
                    pip.rectTransform.anchorMin = pip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    _pipImages.Add(pip);
                }

                const float width = 18f, gap = 5f;
                float start = -(_choices.Length * width + (_choices.Length - 1) * gap) * 0.5f + width * 0.5f;
                for (int i = 0; i < _pipImages.Count; i++)
                {
                    _pipImages[i].gameObject.SetActive(i < _choices.Length);
                    _pipImages[i].rectTransform.sizeDelta = new Vector2(width, 3f);
                    _pipImages[i].rectTransform.anchoredPosition = new Vector2(start + i * (width + gap), 0f);
                }
            }

            _index = Mathf.Clamp(_index, -1, _choices.Length - 1);
            Refresh();
        }

        /// <summary>
        /// 通知せずに選択位置を変更する
        /// </summary>
        /// <param name="index">選択位置</param>
        public void SetIndexWithoutNotify(int index)
        {
            index = Mathf.Clamp(index, -1, _choices.Length - 1);
            if (index == _index) return;

            // 表示直後の初期反映を除き、外部からの変更を一瞬強調する
            if (isActiveAndEnabled && Time.unscaledTime - _enabledAt > FlashDelay) _flashTime = Time.unscaledTime;
            _index = index;
            Refresh();
        }

        /// <summary>
        /// 操作できない理由を表示して値を固定する
        /// </summary>
        /// <param name="text">値の代わりに表示する文字、nullで解除</param>
        public void SetLocked(string text)
        {
            _lockedText = text;
            Refresh();
        }

        /// <summary>
        /// 選択位置を一つずらして通知する
        /// </summary>
        /// <param name="direction">-1で左、1で右</param>
        /// <param name="wrap">端で反対側へ戻るか</param>
        public void Step(int direction, bool wrap)
        {
            if (!IsInteractable() || _lockedText != null || _choices.Length == 0) return;

            // どれにも当てはまらない状態からは、右で先頭、左で末尾を選ぶ
            int next = _index < 0 ? (direction > 0 ? 0 : _choices.Length - 1) : _index + direction;
            if (wrap) next = (next + _choices.Length) % _choices.Length;
            Choose(Mathf.Clamp(next, 0, _choices.Length - 1));
        }

        /// <summary>
        /// 指定した選択肢を選んで通知する
        /// </summary>
        /// <param name="index">選択位置</param>
        public void Choose(int index)
        {
            if (!IsInteractable() || _lockedText != null || index == _index) return;
            _index = index;
            _flashTime = -1f;
            Refresh();
            ValueChanged?.Invoke(index);
        }

        /// <summary>
        /// 左右入力で値を変え、上下入力は通常の移動として扱う
        /// </summary>
        /// <param name="eventData">移動入力</param>
        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir is MoveDirection.Left or MoveDirection.Right)
            {
                Step(eventData.moveDir == MoveDirection.Right ? 1 : -1, false);
                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }

        /// <summary>
        /// 決定入力で次の選択肢へ循環させる
        /// </summary>
        /// <param name="eventData">入力情報</param>
        public void OnSubmit(BaseEventData eventData)
        {
            Step(1, true);
        }

        /// <summary>
        /// 行を押したときに次の選択肢へ循環させる
        /// </summary>
        /// <param name="eventData">ポインター情報</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Select();
            Step(1, true);
        }

        /// <summary>
        /// 表示開始時刻を記録して初期反映を強調しない
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            _enabledAt = Time.unscaledTime;
            _flashTime = -1f;
            Refresh();
        }

        /// <summary>
        /// 外部から変わった値の強調を時間経過で戻す
        /// </summary>
        private void Update()
        {
            if (_flashTime < 0f) return;
            if (Time.unscaledTime - _flashTime > FlashDuration) _flashTime = -1f;
            Refresh();
        }

        /// <summary>
        /// 選択状態と固定状態を各表示へ反映する
        /// </summary>
        private void Refresh()
        {
            bool locked = _lockedText != null;
            bool usable = IsInteractable() && !locked;
            float flash = _flashTime < 0f ? 0f : 1f - Mathf.Clamp01((Time.unscaledTime - _flashTime) / FlashDuration);
            var rest = usable ? RequiemUiTheme.Text : RequiemUiTheme.TextDim;

            // 矢印形式は値の文字、端の矢印、段階表示を更新する
            if (_value != null)
            {
                _value.text = locked ? _lockedText : _index >= 0 && _index < _choices.Length ? _choices[_index] : "-";
                _value.color = Color.Lerp(rest, RequiemUiTheme.AccentBright, flash);
                SetArrow(_left, usable && _index > 0);
                SetArrow(_right, usable && _index < _choices.Length - 1);
                for (int i = 0; i < _pipImages.Count; i++)
                {
                    var color = i <= _index && !locked ? RequiemUiTheme.AccentBright : RequiemUiTheme.Line;
                    _pipImages[i].color = i == _index ? Color.Lerp(color, RequiemUiTheme.TextActive, flash) : color;
                }
            }

            // 区切り形式は選択中だけを赤く塗り、どれにも当てはまらなければカスタム表示を出す
            for (int i = 0; i < _segments.Count; i++)
            {
                bool selected = i == _index;
                var (box, text, rule) = _segments[i];
                box.color = selected ? new Color(RequiemUiTheme.Accent.r, RequiemUiTheme.Accent.g, RequiemUiTheme.Accent.b, 0.55f)
                    : new Color(RequiemUiTheme.Text.r, RequiemUiTheme.Text.g, RequiemUiTheme.Text.b, 0.05f);
                text.color = selected ? RequiemUiTheme.TextActive : RequiemUiTheme.TextDim;
                rule.color = selected ? RequiemUiTheme.AccentBright : Color.clear;
            }

            if (_customTag != null) _customTag.gameObject.SetActive(_segments.Count > 0 && _index < 0);
        }

        /// <summary>
        /// 矢印の表示色を操作可否に合わせる
        /// </summary>
        /// <param name="arrow">対象の矢印</param>
        /// <param name="active">押せるか</param>
        private void SetArrow(Graphic arrow, bool active)
        {
            if (arrow == null) return;
            arrow.gameObject.SetActive(_lockedText == null);
            arrow.color = active ? RequiemUiTheme.Text : RequiemUiTheme.TextDisabled;
        }
    }
}
