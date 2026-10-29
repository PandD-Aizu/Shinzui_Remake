using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shinzui.View.UI
{
    /// <summary>
    /// シーンに配置済みのメニューをレクイエム風の見た目へ整える
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RequiemMenuSkin : MonoBehaviour
    {
        private const float RowWidth = 400f;

        [SerializeField] private TMP_Text heading;
        [SerializeField] private string caption;

        /// <summary>
        /// 背景、文字、操作部品の順に見た目を適用する
        /// </summary>
        private void Awake()
        {
            StyleImages();
            StyleTexts();

            // ボタンは幅で一覧項目と小さなボタンを分ける
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentInChildren<TMP_Text>(true);
                bool isTab = button.transform.Find("SelectedUnderline") != null;
                if (!isTab && ((RectTransform)button.transform).rect.width >= RowWidth) RequiemUiTheme.StyleRowButton(button, label);
                else RequiemUiTheme.StyleCompactButton(button, label, !isTab);
            }

            // スライダーとトグルは行全体を選択帯にする
            foreach (var slider in GetComponentsInChildren<Slider>(true))
            {
                RequiemUiTheme.StyleSlider(slider);
                StyleRow(slider);
            }
            foreach (var toggle in GetComponentsInChildren<Toggle>(true))
            {
                RequiemUiTheme.StyleToggle(toggle);
                StyleRow(toggle);
            }

            if (heading != null) RequiemUiTheme.DecorateHeading(heading, caption);
        }

        /// <summary>
        /// 背景の暗幕と区切り線の色を揃える
        /// </summary>
        private void StyleImages()
        {
            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                string objectName = image.gameObject.name;

                // 区切り線と選択中タブの下線
                if (objectName.Contains("Rule") || objectName.Contains("Divider")) image.color = RequiemUiTheme.Line;
                else if (objectName == "SelectedUnderline") image.color = RequiemUiTheme.AccentBright;
                else if (IsBackdrop(image))
                {
                    var color = RequiemUiTheme.Backdrop;
                    color.a = Mathf.Max(image.color.a, 0.9f);
                    image.color = color;
                    RequiemUiTheme.AddVignette(image.transform);
                }
            }
        }

        /// <summary>
        /// 明るい文字は生成り色、暗い文字は落ち着いた灰色にする
        /// </summary>
        private void StyleTexts()
        {
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            {
                var color = text.color;
                float luminance = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
                text.color = luminance >= 0.7f ? RequiemUiTheme.Text : RequiemUiTheme.TextDim;
            }
        }

        /// <summary>
        /// 親に背景画像がある設定行を選択帯付きにする
        /// </summary>
        /// <param name="target">行の操作対象</param>
        private static void StyleRow(Selectable target)
        {
            var row = target.transform.parent;
            if (row == null || !row.TryGetComponent(out Image background) || row.GetComponent<Selectable>() != null) return;

            // 行の項目名は操作対象の外にある最初の文字とする
            TMP_Text label = null;
            foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.transform.IsChildOf(target.transform)) continue;
                label = text;
                break;
            }
            RequiemUiTheme.StyleRow(background, target, label);
        }

        /// <summary>
        /// 画面全体を覆う暗い背景か判定する
        /// </summary>
        /// <param name="image">判定する画像</param>
        /// <returns>暗幕であればtrue</returns>
        private static bool IsBackdrop(Image image)
        {
            var rect = image.rectTransform;
            if (rect.anchorMin != Vector2.zero || rect.anchorMax != Vector2.one) return false;
            if (image.GetComponent<Selectable>() != null || image.GetComponent<Mask>() != null) return false;
            var color = image.color;
            return color.a >= 0.5f && Mathf.Max(color.r, color.g, color.b) <= 0.1f;
        }
    }
}
