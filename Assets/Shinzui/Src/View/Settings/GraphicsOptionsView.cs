using System;
using System.Collections.Generic;
using Shinzui.View.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shinzui.View.Settings
{
    /// <summary>
    /// プリセットと個別項目を切り替えるグラフィック設定画面を生成する
    /// </summary>
    public sealed class GraphicsOptionsView : MonoBehaviour
    {
        private sealed class RowInfo
        {
            public RectTransform Rect;
            public RequiemOptionSelector Selector;
            public string Description;
            public int Cost;
        }

        private const float DefaultWidth = 1040f;
        private float _width = DefaultWidth;
        private const float RowHeight = 56f;
        private const float SectionHeight = 64f;
        private float _viewportTop = 236f;
        private float _viewportHeight = 500f;
        private RectTransform _descriptionParent;

        /// <summary>フレームレート上限の選択肢に対応する値、0は無制限</summary>
        public static readonly int[] FrameRateValues = { 30, 60, 120, 144, 0 };

        private static readonly string[] OffOn = { "オフ", "オン" };
        private static readonly string[] Levels3 = { "低", "中", "高" };
        private static readonly string[] Levels4 = { "低", "中", "高", "最高" };
        private static readonly string[] OffLevels = { "オフ", "低", "中", "高" };
        private static readonly string[] CostNames = { "", "低", "中", "高" };

        private TMP_FontAsset _font;
        private RectTransform _content;
        private ScrollRect _scroll;
        private TMP_Text _descriptionTitle;
        private TMP_Text _descriptionBody;
        private TMP_Text _descriptionCost;
        private GameObject _lastSelected;
        private float _contentHeight;
        private readonly List<RowInfo> _rows = new();
        private readonly Dictionary<GameObject, RowInfo> _rowMap = new();

        /// <summary>プリセット、0から3が低、中、高、最高、-1がカスタム</summary>
        public RequiemOptionSelector Preset { get; private set; }

        // --- 画面 ---
        public RequiemOptionSelector ScreenMode { get; private set; }
        public RequiemOptionSelector Resolution { get; private set; }
        public RequiemOptionSelector FrameRateLimit { get; private set; }
        public RequiemOptionSelector VSync { get; private set; }

        /// <summary>アップスケーリング、0がオフ、1から3が品質0から2</summary>
        public RequiemOptionSelector Upscaling { get; private set; }

        // --- 描画品質 ---
        public RequiemOptionSelector AntiAliasing { get; private set; }
        public RequiemOptionSelector TextureQuality { get; private set; }
        public RequiemOptionSelector TextureFiltering { get; private set; }
        public RequiemOptionSelector MeshQuality { get; private set; }

        // --- ライティング ---
        public RequiemOptionSelector RayTracing { get; private set; }
        public RequiemOptionSelector GiAndReflection { get; private set; }
        public RequiemOptionSelector Ssr { get; private set; }
        public RequiemOptionSelector Ao { get; private set; }
        public RequiemOptionSelector VolumeLight { get; private set; }
        public RequiemOptionSelector Sss { get; private set; }

        // --- 影 ---
        public RequiemOptionSelector ShadowQuality { get; private set; }
        public RequiemOptionSelector ContactShadow { get; private set; }

        // --- エフェクト ---
        public RequiemOptionSelector Bloom { get; private set; }
        public RequiemOptionSelector LensFlare { get; private set; }
        public RequiemOptionSelector FilmGrain { get; private set; }
        public RequiemOptionSelector DepthOfField { get; private set; }
        public RequiemOptionSelector LensDistortion { get; private set; }

        /// <summary>前の画面へ戻るボタン</summary>
        public Button Back { get; private set; }

        /// <summary>戻る操作を要求したときに通知する</summary>
        public event Action BackRequested;

        /// <summary>
        /// 指定した親の中央左寄せにグラフィック設定画面を生成する
        /// </summary>
        /// <param name="parent">配置先</param>
        /// <param name="font">表示フォント</param>
        /// <param name="position">親の左端中央からの位置</param>
        /// <returns>生成した画面</returns>
        public static GraphicsOptionsView Create(Transform parent, TMP_FontAsset font, Vector2 position)
        {
            var rect = new GameObject("GraphicsOptions", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(DefaultWidth, 920f);
            rect.anchoredPosition = position;
            var view = rect.gameObject.AddComponent<GraphicsOptionsView>();
            view.Build(font);
            return view;
        }

        /// <summary>
        /// オプション画面の設定一覧と説明欄に詳細設定を生成する
        /// </summary>
        /// <param name="list">設定一覧の領域</param>
        /// <param name="description">説明欄の領域</param>
        /// <param name="font">表示フォント</param>
        /// <returns>生成した画面</returns>
        public static GraphicsOptionsView CreateEmbedded(RectTransform list, RectTransform description, TMP_FontAsset font)
        {
            // 既存の二列レイアウトの寸法に合わせる
            var view = list.gameObject.AddComponent<GraphicsOptionsView>();
            view._width = list.rect.width;
            view._viewportTop = list.rect.height * 0.5f - 82f;
            view._viewportHeight = list.rect.height - 150f;
            view._descriptionParent = description;
            view.Build(font);
            return view;
        }

        /// <summary>
        /// 選択可能な解像度を設定する
        /// </summary>
        /// <param name="resolutions">"幅x高さ"形式の解像度一覧</param>
        public void SetResolutions(IReadOnlyList<string> resolutions)
        {
            var labels = new string[resolutions.Count];
            for (int i = 0; i < labels.Length; i++) labels[i] = resolutions[i].Replace("x", " × ");
            Resolution.SetChoices(labels);
        }

        /// <summary>
        /// レイトレーシングの対応状態を表示へ反映する
        /// </summary>
        /// <param name="supported">現在の環境で利用できるか</param>
        public void SetRayTracingSupported(bool supported)
        {
            RayTracing.SetLocked(supported ? null : "非対応");
            _rowMap[RayTracing.gameObject].Description = supported
                ? "光の反射、間接光、影をレイトレーシングで計算する。反射とアンビエントオクルージョンはレイトレース版に置き換わる。対応GPUでも負荷が大きい"
                : "この環境ではレイトレーシングを利用できない。DirectX 12とレイトレーシング対応GPU（NVIDIA RTX、AMD RX 6000以降など）が必要";
        }

        /// <summary>
        /// 利用できるアップスケーラーに合わせて行を表示する
        /// </summary>
        /// <param name="name">方式名、利用できない場合はnull</param>
        public void SetUpscaler(string name)
        {
            bool available = !string.IsNullOrEmpty(name);
            var row = _rowMap[Upscaling.gameObject];
            row.Rect.gameObject.SetActive(available);
            if (available)
            {
                row.Rect.GetComponentInChildren<TMP_Text>(true).text = $"アップスケーリング（{name}）";
                row.Description = $"{name}で低い解像度から高解像度の画像を生成し、フレームレートを上げる。Performanceほど軽いが細部がぼやける";
            }

            Layout();
        }

        /// <summary>
        /// 先頭のプリセットを選択する
        /// </summary>
        public void Focus()
        {
            _lastSelected = null;
            Preset.Select();
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// 選択中の項目に合わせて説明とスクロール位置を更新する
        /// </summary>
        private void LateUpdate()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == _lastSelected) return;
            _lastSelected = selected;

            // 説明欄の内容を選択中の項目に切り替える
            if (selected == null || !_rowMap.TryGetValue(selected, out var row))
            {
                _descriptionTitle.text = "";
                _descriptionBody.text = "";
                _descriptionCost.text = "";
                return;
            }

            _descriptionTitle.text = row.Rect.GetComponentInChildren<TMP_Text>(true).text;
            _descriptionBody.text = row.Description;
            _descriptionCost.text = row.Cost > 0
                ? $"<color=#85807A>負荷</color>  {new string('■', row.Cost)}<color=#4D4A47>{new string('■', 3 - row.Cost)}</color>  {CostNames[row.Cost]}"
                : "";

            // スクロール一覧の行は表示範囲に収まるよう移動する
            if (row.Rect.parent == _content) ScrollIntoView(row.Rect);
        }

        /// <summary>
        /// 見出し、プリセット、項目一覧、説明欄を生成する
        /// </summary>
        /// <param name="font">表示フォント</param>
        private void Build(TMP_FontAsset font)
        {
            _font = font;

            // 埋め込み表示では既存のOption見出しを使う
            if (_descriptionParent == null)
            {
                var heading = Label(transform, "グラフィック", new Vector2(0f, 400f), new Vector2(_width, 70f), 48f);
                heading.alignment = TextAlignmentOptions.Left;
                RequiemUiTheme.DecorateHeading(heading, "GRAPHICS");
            }

            // 一覧の上に固定したプリセット切り替え
            BuildPreset();

            // 項目一覧はスクロール領域に並べる
            BuildScroll();
            Section("画面", "DISPLAY");
            ScreenMode = Choice("画面モード", new[] { "フルスクリーン", "ウィンドウ", "ボーダーレス" }, false, 0,
                "フルスクリーンは最も遅延が少ない。ボーダーレスはほかのウィンドウへの切り替えが速い");
            Resolution = Choice("解像度", Array.Empty<string>(), false, 3,
                "描画する画面の解像度。下げるほど軽くなるが文字や細部が粗くなる");
            FrameRateLimit = Choice("フレームレート上限", new[] { "30", "60", "120", "144", "無制限" }, false, 2,
                "1秒あたりの最大描画回数。上限を下げるとGPUの発熱と消費電力を抑えられる");
            VSync = Choice("垂直同期", OffOn, true, 0,
                "画面の更新に合わせて描画し、映像の裂け目（ティアリング）を防ぐ。わずかに入力遅延が増える");
            Upscaling = Choice("アップスケーリング", new[] { "オフ", "Quality", "Balanced", "Performance" }, true, 0, "");

            Section("描画品質", "QUALITY");
            AntiAliasing = Choice("アンチエイリアス", new[] { "オフ", "FXAA", "SMAA", "TAA" }, true, 1,
                "輪郭のギザギザを滑らかにする。TAAが最もきれいだが、動きの速い場面でわずかに残像が出る");
            TextureQuality = Choice("テクスチャ品質", Levels4, true, 2,
                "表面の模様や汚れの解像度。ビデオメモリの使用量に大きく影響する");
            TextureFiltering = Choice("テクスチャフィルタリング", new[] { "2x", "4x", "8x", "16x" }, true, 1,
                "斜めから見た床や壁の模様をくっきり表示する。負荷はわずか");
            MeshQuality = Choice("モデル詳細度", Levels3, true, 2,
                "遠くの物体を簡略化し始める距離。高いほど遠くまで細かい形を保つ");

            Section("ライティング", "LIGHTING");
            RayTracing = Choice("レイトレーシング", OffOn, true, 3, "");
            GiAndReflection = Choice("GI・反射品質", OffLevels, true, 3,
                "光の照り返し（間接光）と反射の精度。暗いトンネルの雰囲気に大きく影響する");
            Ssr = Choice("スクリーンスペース反射", OffOn, true, 2,
                "濡れた路面や金属に周囲の景色を映り込ませる");
            Ao = Choice("アンビエントオクルージョン", OffOn, true, 1,
                "隅や物の接地部分を暗くして立体感を出す");
            VolumeLight = Choice("ボリュームライト", OffLevels, true, 3,
                "霧の中に浮かぶ光の筋。ライトの照らす範囲が見えやすくなる");
            Sss = Choice("サブサーフェス散乱", OffOn, true, 1,
                "肌などの半透明な表面に光が透ける表現");

            Section("影", "SHADOWS");
            ShadowQuality = Choice("影の品質", OffLevels, true, 3,
                "影の解像度と描画距離。低いと影の輪郭がぼやけ、オフでは影が消える");
            ContactShadow = Choice("コンタクトシャドウ", OffOn, true, 1,
                "物が床に触れている部分の細かな影を追加する");

            Section("エフェクト", "EFFECTS");
            Bloom = Choice("ブルーム", OffOn, true, 1, "明るい光源のまわりに光がにじむ効果");
            LensFlare = Choice("レンズフレア", OffOn, true, 1, "強い光がレンズに入ったときの光の輪");
            FilmGrain = Choice("フィルムグレイン", OffOn, true, 0, "映像に細かなノイズを重ね、ざらついた質感を出す");
            DepthOfField = Choice("被写界深度", OffOn, true, 1, "注目している物以外をぼかす");
            LensDistortion = Choice("レンズゆがみ", OffOn, true, 0, "画面の端をゆがませ、色をにじませる演出。酔いやすい場合はオフ");
            SetRayTracingSupported(false);
            SetUpscaler(null);

            // 選択中の項目の説明欄と戻るボタン
            BuildDescription();
            Back = Button("もどる", _descriptionParent == null ? -408f : -((RectTransform)transform).rect.height * 0.5f + 30f);
            Layout();
        }

        /// <summary>
        /// 低、中、高、最高の区切りボタンとカスタム表示を生成する
        /// </summary>
        private void BuildPreset()
        {
            var row = Row(transform, "プリセット", out var label);
            row.anchoredPosition = new Vector2(0f, _descriptionParent == null ? 296f : _viewportTop + 44f);
            row.sizeDelta = new Vector2(_width, 76f);
            label.fontSize = 30f;

            var hit = RequiemUiTheme.Stretch("Selector", row);
            hit.gameObject.AddComponent<Image>().color = Color.clear;
            Preset = hit.gameObject.AddComponent<RequiemOptionSelector>();
            Preset.transition = Selectable.Transition.None;

            // 右寄せで四つの区切りを並べる
            var segments = new List<(Image, TMP_Text, Image)>();
            string[] names = { "低", "中", "高", "最高" };
            for (int i = 0; i < names.Length; i++)
            {
                float segmentWidth = _descriptionParent == null ? 104f : 88f;
                var box = Rect("Segment", hit, new Vector2(segmentWidth, 46f));
                box.anchoredPosition = new Vector2(_width * 0.5f - 50f - segmentWidth * 0.5f - (3 - i) * (segmentWidth + 8f), 0f);
                var image = box.gameObject.AddComponent<Image>();
                var text = Label(box, names[i], Vector2.zero, box.sizeDelta, 26f);
                var rule = RequiemUiTheme.Decoration("Rule", box, Color.clear);
                rule.rectTransform.anchorMax = new Vector2(1f, 0f);
                rule.rectTransform.pivot = new Vector2(0.5f, 0f);
                rule.rectTransform.sizeDelta = new Vector2(0f, 2f);
                segments.Add((image, text, rule));
            }

            // どのプリセットとも一致しない場合の表示
            var custom = Label(hit, "カスタム", new Vector2(-118f, 0f), new Vector2(140f, 40f), 20f);
            custom.color = RequiemUiTheme.AccentBright;
            custom.characterSpacing = 20f;
            custom.alignment = TextAlignmentOptions.Right;

            Preset.InitializeSegments(segments, custom);
            Preset.SetChoices(names);
            RequiemUiTheme.StyleRow(row.GetComponent<Image>(), Preset, label);
            Register(row, Preset,
                "描画品質をまとめて切り替える。画面、フレームレート、演出系の設定は変わらない。個別に変更するとカスタムになる", 0);
        }

        /// <summary>
        /// 上下端をぼかしたスクロール領域を生成する
        /// </summary>
        private void BuildScroll()
        {
            var viewport = Rect("Viewport", transform, new Vector2(_width, _viewportHeight));
            viewport.anchoredPosition = new Vector2(0f, _viewportTop - _viewportHeight * 0.5f);
            viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 24);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;

            _content = Rect("Content", viewport, new Vector2(_width, 0f));
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;

            _scroll = gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
        }

        /// <summary>
        /// 選択中の項目名、説明、負荷を表示する欄を生成する
        /// </summary>
        private void BuildDescription()
        {
            // Option画面では右側に確保された既存の説明領域を使う
            if (_descriptionParent != null)
            {
                float width = _descriptionParent.rect.width;
                float topY = _descriptionParent.rect.height * 0.5f;
                _descriptionTitle = Label(_descriptionParent, "", new Vector2(0f, topY - 25f), new Vector2(width, 50f), 32f);
                _descriptionTitle.alignment = TextAlignmentOptions.TopLeft;
                _descriptionBody = Label(_descriptionParent, "", new Vector2(0f, topY - 180f), new Vector2(width, 200f), 24f);
                _descriptionBody.alignment = TextAlignmentOptions.TopLeft;
                _descriptionBody.color = RequiemUiTheme.TextDim;
                _descriptionBody.textWrappingMode = TextWrappingModes.Normal;
                _descriptionCost = Label(_descriptionParent, "", new Vector2(0f, topY - 310f), new Vector2(width, 40f), 22f);
                _descriptionCost.alignment = TextAlignmentOptions.Left;
                return;
            }

            var box = Rect("Description", transform, new Vector2(_width, 112f));
            box.anchoredPosition = new Vector2(0f, -320f);
            var top = RequiemUiTheme.Decoration("Rule", box, RequiemUiTheme.Line);
            top.rectTransform.anchorMin = new Vector2(0f, 1f);
            top.rectTransform.pivot = new Vector2(0.5f, 1f);
            top.rectTransform.sizeDelta = new Vector2(0f, 1f);

            _descriptionTitle = Label(box, "", new Vector2(-150f, 30f), new Vector2(_width - 300f, 36f), 22f);
            _descriptionTitle.alignment = TextAlignmentOptions.Left;
            _descriptionTitle.color = RequiemUiTheme.AccentBright;

            _descriptionCost = Label(box, "", new Vector2(_width * 0.5f - 150f, 30f), new Vector2(300f, 36f), 20f);
            _descriptionCost.alignment = TextAlignmentOptions.Right;

            _descriptionBody = Label(box, "", new Vector2(0f, -18f), new Vector2(_width, 64f), 21f);
            _descriptionBody.alignment = TextAlignmentOptions.TopLeft;
            _descriptionBody.color = RequiemUiTheme.TextDim;
            _descriptionBody.textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>
        /// 英字を添えた区切り見出しを一覧へ追加する
        /// </summary>
        /// <param name="text">見出し</param>
        /// <param name="caption">英字の小見出し</param>
        private void Section(string text, string caption)
        {
            var rect = Rect(text, _content, new Vector2(_width, SectionHeight));
            var label = Label(rect, $"<color=#D1211F><size=16><cspace=0.3em>{caption}</cspace></size></color>   {text}",
                new Vector2(0f, -8f), new Vector2(_width, 40f), 22f);
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.margin = new Vector4(12f, 0f, 0f, 0f);
            label.color = RequiemUiTheme.TextDim;
            _rows.Add(new RowInfo { Rect = rect });
        }

        /// <summary>
        /// 左右の矢印で値を切り替える設定行を一覧へ追加する
        /// </summary>
        /// <param name="text">項目名</param>
        /// <param name="choices">選択肢</param>
        /// <param name="pips">段階表示を出すか</param>
        /// <param name="cost">負荷の目安、0で非表示</param>
        /// <param name="description">説明文</param>
        /// <returns>生成した設定行</returns>
        private RequiemOptionSelector Choice(string text, string[] choices, bool pips, int cost, string description)
        {
            var row = Row(_content, text, out var label);
            var hit = RequiemUiTheme.Stretch("Selector", row);
            hit.gameObject.AddComponent<Image>().color = Color.clear;
            var selector = hit.gameObject.AddComponent<RequiemOptionSelector>();
            selector.transition = Selectable.Transition.None;

            // 右側に矢印で挟んだ値と段階表示を置く
            float center = _width * 0.5f - 230f;
            var value = Label(hit, "", new Vector2(center, pips ? 7f : 0f), new Vector2(240f, 32f), 24f);
            var left = Arrow(hit, "‹", center - 150f);
            var right = Arrow(hit, "›", center + 150f);
            RectTransform pipRoot = null;
            if (pips)
            {
                pipRoot = Rect("Pips", hit, new Vector2(200f, 4f));
                pipRoot.anchoredPosition = new Vector2(center, -19f);
            }

            selector.InitializeArrows(value, left, right, pipRoot);
            selector.SetChoices(choices);
            RequiemUiTheme.StyleRow(row.GetComponent<Image>(), selector, label);
            Register(row, selector, description, cost);
            return selector;
        }

        /// <summary>
        /// 押せる範囲を広げた矢印を生成する
        /// </summary>
        /// <param name="parent">配置先</param>
        /// <param name="text">矢印の文字</param>
        /// <param name="x">横位置</param>
        /// <returns>生成した矢印</returns>
        private TMP_Text Arrow(Transform parent, string text, float x)
        {
            var arrow = Label(parent, text, new Vector2(x, 2f), new Vector2(48f, 52f), 40f);
            arrow.raycastTarget = true;
            return arrow;
        }

        /// <summary>
        /// 項目と説明を登録する
        /// </summary>
        /// <param name="row">行</param>
        /// <param name="selector">操作対象</param>
        /// <param name="description">説明文</param>
        /// <param name="cost">負荷の目安</param>
        private void Register(RectTransform row, RequiemOptionSelector selector, string description, int cost)
        {
            var info = new RowInfo { Rect = row, Selector = selector, Description = description, Cost = cost };
            _rowMap[selector.gameObject] = info;
            if (row.parent == _content) _rows.Add(info);
        }

        /// <summary>
        /// 表示中の行を上から並べ、上下の移動先を設定する
        /// </summary>
        private void Layout()
        {
            if (Back == null) return;

            // 非表示の行を詰めて縦に並べる
            float y = 0f;
            var order = new List<Selectable> { Preset };
            foreach (var row in _rows)
            {
                if (!row.Rect.gameObject.activeSelf) continue;
                float height = row.Selector == null ? SectionHeight : RowHeight;
                row.Rect.anchorMin = row.Rect.anchorMax = new Vector2(0.5f, 1f);
                row.Rect.pivot = new Vector2(0.5f, 1f);
                row.Rect.anchoredPosition = new Vector2(0f, -y);
                y += height;
                if (row.Selector != null) order.Add(row.Selector);
            }

            _contentHeight = y + 12f;
            _content.sizeDelta = new Vector2(0f, _contentHeight);
            order.Add(Back);

            // 上下は一覧順、端では反対側へ戻らず止める
            for (int i = 0; i < order.Count; i++)
            {
                var navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? order[i - 1] : null,
                    selectOnDown = i < order.Count - 1 ? order[i + 1] : null
                };
                order[i].navigation = navigation;
            }
        }

        /// <summary>
        /// 行が表示範囲の外にある場合だけ一覧をスクロールする
        /// </summary>
        /// <param name="row">表示する行</param>
        private void ScrollIntoView(RectTransform row)
        {
            float range = _contentHeight - _viewportHeight;
            if (range <= 0f) return;

            // 前後の行が少し見えるよう余白を取って位置を合わせる
            const float margin = RowHeight;
            float top = -row.anchoredPosition.y;
            float bottom = top + row.rect.height;
            float offset = (1f - _scroll.verticalNormalizedPosition) * range;
            if (top - margin < offset) offset = top - margin;
            else if (bottom + margin > offset + _viewportHeight) offset = bottom + margin - _viewportHeight;
            _scroll.verticalNormalizedPosition = 1f - Mathf.Clamp(offset, 0f, range) / range;
            _scroll.velocity = Vector2.zero;
        }

        /// <summary>
        /// 中央基準のUI領域を生成する
        /// </summary>
        /// <param name="name">オブジェクト名</param>
        /// <param name="parent">配置先</param>
        /// <param name="size">表示サイズ</param>
        /// <returns>生成した領域</returns>
        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>
        /// 指定フォントでラベルを生成する
        /// </summary>
        /// <param name="parent">配置先</param>
        /// <param name="text">表示文字列</param>
        /// <param name="position">中央からの位置</param>
        /// <param name="size">表示サイズ</param>
        /// <param name="fontSize">文字サイズ</param>
        /// <returns>生成したテキスト</returns>
        private TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
        {
            var rect = Rect("Label", parent, size);
            rect.anchoredPosition = position;
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = RequiemUiTheme.Text;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// 背景と下線を持つ設定行を生成する
        /// </summary>
        /// <param name="parent">配置先</param>
        /// <param name="text">項目名</param>
        /// <param name="label">生成した項目名</param>
        /// <returns>生成した行</returns>
        private RectTransform Row(Transform parent, string text, out TMP_Text label)
        {
            var row = Rect(text, parent, new Vector2(_width, RowHeight));
            row.gameObject.AddComponent<Image>();
            var rule = RequiemUiTheme.Decoration("Rule", row, RequiemUiTheme.Line);
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 1f);
            label = Label(row, text, new Vector2(-240f, 0f), new Vector2(_width - 480f, RowHeight), 26f);
            label.alignment = TextAlignmentOptions.Left;
            label.margin = new Vector4(44f, 0f, 0f, 0f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 26f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return row;
        }

        /// <summary>
        /// 選択時に赤い帯が出る一覧形式のボタンを生成する
        /// </summary>
        /// <param name="text">表示文字列</param>
        /// <param name="y">縦位置</param>
        /// <returns>生成したボタン</returns>
        private Button Button(string text, float y)
        {
            var rect = Rect(text, transform, new Vector2(_width, 60f));
            rect.anchoredPosition = new Vector2(0f, y);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => BackRequested?.Invoke());
            var label = Label(rect, text, Vector2.zero, rect.sizeDelta, 30f);
            RequiemUiTheme.StyleRowButton(button, label);
            return button;
        }
    }
}
