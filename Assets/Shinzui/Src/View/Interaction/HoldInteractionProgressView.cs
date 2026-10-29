using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Shinzui.View.Interaction
{
    /// <summary>長押し操作で共用できるボタン表示と進捗リング</summary>
    public sealed class HoldInteractionProgressView : MonoBehaviour
    {
        private HoldProgressGraphic _graphic;
        private TMP_Text _label;
        private CanvasGroup _group;
        private float _completedUntil;
        private bool _usingGamepad;
        public bool IsShowingCompletion => _completedUntil > Time.unscaledTime;

        /// <summary>対象のマーカー位置へ配置できる解像度追従の長押しUIを作成</summary>
        /// <param name="owner">UIの所有者</param>
        /// <param name="font">既存HUDのフォント</param>
        /// <param name="offset">画面中央からの位置</param>
        /// <param name="diameter">1920×1080を基準にした直径</param>
        /// <returns>長押しUIの制御コンポーネント</returns>
        public static HoldInteractionProgressView Create(Transform owner, TMP_FontAsset font, Vector2 offset, float diameter)
        {
            var root = new GameObject("HoldInteractionCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            root.transform.SetParent(owner, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            // 表示位置を後から指定できるゲージを作り、入力を遮らない
            var content = new GameObject("HoldProgress", typeof(RectTransform), typeof(CanvasGroup), typeof(HoldProgressGraphic));
            content.transform.SetParent(root.transform, false);
            var rect = (RectTransform)content.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = Vector2.one * diameter;

            var text = new GameObject("ButtonLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(content.transform, false);
            var label = text.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = "E";
            label.fontSize = diameter * .4f;
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.08f, .09f, .10f, 1f);
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

            var view = root.AddComponent<HoldInteractionProgressView>();
            view._graphic = content.GetComponent<HoldProgressGraphic>();
            view._graphic.raycastTarget = false;
            view._label = label;
            view._group = content.GetComponent<CanvasGroup>();
            view._group.blocksRaycasts = false;
            view._group.interactable = false;
            view.Hide();
            return view;
        }

        /// <summary>元の白い円と同じ画面位置へゲージを重ねる</summary>
        /// <param name="screenPosition">対象マーカーの画面座標</param>
        public void SetScreenPosition(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                screenPosition, null, out Vector2 localPoint);
            _graphic.rectTransform.anchoredPosition = localPoint;
        }

        /// <summary>最後に操作した入力機器に合わせてボタン表示を更新</summary>
        public void UpdateInputDevice()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool keyboardUsed = keyboard != null && keyboard.anyKey.wasPressedThisFrame;
            bool mouseUsed = mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
                || mouse.delta.ReadValue().sqrMagnitude > 1f);
            var gamepad = Gamepad.current;
            if (keyboardUsed || mouseUsed) _usingGamepad = false;
            else if (gamepad != null)
            {
                for (int i = 0; i < gamepad.allControls.Count; i++)
                    if (gamepad.allControls[i] is ButtonControl button && button.wasPressedThisFrame) _usingGamepad = true;
                if (gamepad.leftStick.ReadValue().sqrMagnitude > .25f || gamepad.rightStick.ReadValue().sqrMagnitude > .25f)
                    _usingGamepad = true;
            }
            if (gamepad == null) _usingGamepad = false;

            // 既存の下側ボタン割り当てに合わせ、PSのCross表記を記号にする
            string key = "E";
            if (_usingGamepad)
            {
                key = gamepad.buttonSouth.shortDisplayName;
                if (string.IsNullOrEmpty(key)) key = gamepad.buttonSouth.displayName;
                if (key == "Cross") key = "×";
                if (key == "Button South") key = "A";
            }
            if (_label.text != key) _label.text = key;
        }

        /// <summary>長押し中や操作可能な対象の進捗を表示</summary>
        /// <param name="visible">通常表示するか</param>
        /// <param name="progress">長押しの達成割合</param>
        public void Refresh(bool visible, float progress)
        {
            float remaining = _completedUntil - Time.unscaledTime;
            bool completed = remaining > 0f;
            _group.alpha = completed ? Mathf.Clamp01(remaining / .12f) : visible ? 1f : 0f;
            _graphic.SetProgress(completed ? 1f : progress, completed);
        }

        /// <summary>操作成立時は満ちたリングを短く保持</summary>
        public void ShowCompleted()
        {
            _completedUntil = Time.unscaledTime + .4f;
        }

        /// <summary>キャンセルや無効化時に完了表示も含めて消去</summary>
        public void Hide()
        {
            _completedUntil = 0f;
            _group.alpha = 0f;
            _graphic.SetProgress(0f, false);
        }
    }
}
