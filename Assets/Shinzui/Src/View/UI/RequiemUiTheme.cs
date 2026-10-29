using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shinzui.View.UI
{
    /// <summary>
    /// レクイエム風メニューの配色と装飾パーツを提供する
    /// </summary>
    public static class RequiemUiTheme
    {
        public static readonly Color Text = new(0.88f, 0.86f, 0.82f, 1f);
        public static readonly Color TextActive = new(1f, 0.97f, 0.94f, 1f);
        public static readonly Color TextDim = new(0.52f, 0.5f, 0.47f, 1f);
        public static readonly Color TextDisabled = new(0.3f, 0.29f, 0.28f, 1f);
        public static readonly Color Accent = new(0.55f, 0.04f, 0.05f, 1f);
        public static readonly Color AccentBright = new(0.82f, 0.13f, 0.12f, 1f);
        public static readonly Color Line = new(0.88f, 0.86f, 0.82f, 0.14f);
        public static readonly Color Backdrop = new(0.016f, 0.012f, 0.012f, 1f);

        private static Sprite _fadeRight;
        private static Sprite _vignette;

        /// <summary>
        /// 左から右へ透明になるグラデーション
        /// </summary>
        public static Sprite FadeRight => _fadeRight ??= CreateFadeRight();

        /// <summary>
        /// 中央が透明で外周が暗くなる周辺減光
        /// </summary>
        public static Sprite Vignette => _vignette ??= CreateVignette();

        /// <summary>
        /// 親全体に広がるUI領域を生成する
        /// </summary>
        /// <param name="name">オブジェクト名</param>
        /// <param name="parent">配置先</param>
        /// <returns>生成した領域</returns>
        public static RectTransform Stretch(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        /// <summary>
        /// 入力を受けない装飾用の画像を生成する
        /// </summary>
        /// <param name="name">オブジェクト名</param>
        /// <param name="parent">配置先</param>
        /// <param name="color">表示色</param>
        /// <param name="sprite">表示スプライト</param>
        /// <returns>生成した画像</returns>
        public static Image Decoration(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var image = Stretch(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// 背景の最背面に周辺減光を追加する
        /// </summary>
        /// <param name="parent">背景となる領域</param>
        public static void AddVignette(Transform parent)
        {
            Decoration("Vignette", parent, new Color(0f, 0f, 0f, 0.85f), Vignette).transform.SetAsFirstSibling();
        }

        /// <summary>
        /// 一覧形式の項目として左寄せの選択帯を付ける
        /// </summary>
        /// <param name="button">対象ボタン</param>
        /// <param name="label">項目名</param>
        public static void StyleRowButton(Button button, TMP_Text label)
        {
            // 背景は控えめにし、下端に細い区切り線を引く
            if (button.targetGraphic != null) button.targetGraphic.color = new Color(1f, 1f, 1f, 0.025f);
            var rule = Decoration("Rule", button.transform, Line);
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 1f);
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);

            // 選択時に左から滲む赤い帯と縦のマーカーを用意する
            var highlight = Decoration("Highlight", button.transform, new Color(Accent.r, Accent.g, Accent.b, 0.6f), FadeRight);
            highlight.transform.SetAsFirstSibling();
            var marker = Decoration("Marker", button.transform, AccentBright);
            marker.rectTransform.anchorMax = new Vector2(0f, 1f);
            marker.rectTransform.pivot = new Vector2(0f, 0.5f);
            marker.rectTransform.sizeDelta = new Vector2(4f, -18f);

            // 項目名は左寄せにしてマーカー分の余白を空ける
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Left;
                label.margin = new Vector4(44f, 0f, 24f, 0f);
            }

            button.transition = Selectable.Transition.None;
            Attach(button, highlight, marker, label);
        }

        /// <summary>
        /// 横並びや確認用の小さなボタンに選択表示を付ける
        /// </summary>
        /// <param name="button">対象ボタン</param>
        /// <param name="label">項目名</param>
        /// <param name="manageLabel">文字色を選択状態で切り替えるか</param>
        public static void StyleCompactButton(Button button, TMP_Text label, bool manageLabel)
        {
            if (button.targetGraphic != null) button.targetGraphic.color = new Color(1f, 1f, 1f, 0.03f);

            // 選択時は暗い赤で塗り、下端に明るい赤の線を出す
            var highlight = Decoration("Highlight", button.transform, new Color(Accent.r, Accent.g, Accent.b, 0.45f));
            highlight.transform.SetAsFirstSibling();
            var marker = Decoration("Marker", button.transform, AccentBright);
            marker.rectTransform.anchorMax = new Vector2(1f, 0f);
            marker.rectTransform.pivot = new Vector2(0.5f, 0f);
            marker.rectTransform.sizeDelta = new Vector2(0f, 2f);

            button.transition = Selectable.Transition.None;
            Attach(button, highlight, marker, manageLabel ? label : null);
        }

        /// <summary>
        /// 設定行の背景を選択帯として扱う
        /// </summary>
        /// <param name="row">行の背景画像</param>
        /// <param name="target">行の操作対象</param>
        /// <param name="label">行の項目名</param>
        public static void StyleRow(Image row, Selectable target, TMP_Text label)
        {
            row.sprite = FadeRight;
            row.color = new Color(Accent.r, Accent.g, Accent.b, 0.5f);
            row.raycastTarget = false;
            var marker = Decoration("Marker", row.transform, AccentBright);
            marker.rectTransform.anchorMax = new Vector2(0f, 1f);
            marker.rectTransform.pivot = new Vector2(0f, 0.5f);
            marker.rectTransform.sizeDelta = new Vector2(4f, -18f);
            Attach(target, row, marker, label);
        }

        /// <summary>
        /// 細い線と赤い進捗で構成されたスライダーにする
        /// </summary>
        /// <param name="slider">対象スライダー</param>
        public static void StyleSlider(Slider slider)
        {
            if (slider.transform.Find("Background") is RectTransform background && background.TryGetComponent(out Image track))
                track.color = new Color(Text.r, Text.g, Text.b, 0.2f);
            if (slider.fillRect != null && slider.fillRect.TryGetComponent(out Image fill)) fill.color = Accent;
            if (slider.handleRect != null && slider.handleRect.TryGetComponent(out Image handle)) handle.color = Text;
            slider.transition = Selectable.Transition.None;
        }

        /// <summary>
        /// 枠線だけの四角に赤い印が入るトグルにする
        /// </summary>
        /// <param name="toggle">対象トグル</param>
        public static void StyleToggle(Toggle toggle)
        {
            if (toggle.targetGraphic != null)
            {
                toggle.targetGraphic.color = new Color(Text.r, Text.g, Text.b, 0.06f);
                var outline = toggle.targetGraphic.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(Text.r, Text.g, Text.b, 0.5f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            // チェック印は枠の内側に収める
            if (toggle.graphic != null)
            {
                toggle.graphic.color = AccentBright;
                var rect = toggle.graphic.rectTransform;
                rect.anchorMin = new Vector2(0.22f, 0.22f);
                rect.anchorMax = new Vector2(0.78f, 0.78f);
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
            }

            toggle.transition = Selectable.Transition.None;
        }

        /// <summary>
        /// 見出しの上に英字の小見出し、下に赤い線を添える
        /// </summary>
        /// <param name="heading">見出し</param>
        /// <param name="caption">英字の小見出し</param>
        public static void DecorateHeading(TMP_Text heading, string caption)
        {
            heading.color = Text;
            bool centered = (heading.horizontalAlignment & HorizontalAlignmentOptions.Center) != 0;
            float anchorX = centered ? 0.5f : 0f;

            // 見出しの下に、左寄せならフェードする線、中央なら短い線を引く
            var rule = Decoration("HeadingRule", heading.transform, AccentBright, centered ? null : FadeRight);
            rule.rectTransform.anchorMin = rule.rectTransform.anchorMax = new Vector2(anchorX, 0f);
            rule.rectTransform.pivot = new Vector2(anchorX, 1f);
            rule.rectTransform.sizeDelta = new Vector2(centered ? 120f : 420f, 2f);

            if (string.IsNullOrEmpty(caption)) return;

            // 小見出しは字間を広げた赤い英字にする
            var rect = new GameObject("Caption", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(heading.transform, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 32f);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = heading.font;
            text.text = caption;
            text.fontSize = 20f;
            text.characterSpacing = 40f;
            text.color = AccentBright;
            text.alignment = centered ? TextAlignmentOptions.Bottom : TextAlignmentOptions.BottomLeft;
            text.raycastTarget = false;
        }

        /// <summary>
        /// 選択表示を制御するコンポーネントを付ける
        /// </summary>
        /// <param name="target">選択対象</param>
        /// <param name="highlight">選択帯</param>
        /// <param name="marker">選択マーカー</param>
        /// <param name="label">色を切り替える文字</param>
        private static void Attach(Selectable target, Graphic highlight, Graphic marker, TMP_Text label)
        {
            if (!target.TryGetComponent(out RequiemMenuItem item)) item = target.gameObject.AddComponent<RequiemMenuItem>();
            item.Initialize(target, highlight, marker, label);
        }

        /// <summary>
        /// 横方向のフェード用テクスチャを生成する
        /// </summary>
        /// <returns>生成したスプライト</returns>
        private static Sprite CreateFadeRight()
        {
            const int width = 256;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int x = 0; x < width; x++)
            {
                float t = x / (width - 1f);
                texture.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Pow(1f - t, 1.6f)));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 楕円形の周辺減光テクスチャを生成する
        /// </summary>
        /// <returns>生成したスプライト</returns>
        private static Sprite CreateVignette()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x / (size - 1f) - 0.5f, y / (size - 1f) - 0.5f) * 2f;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.45f, p.magnitude));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
