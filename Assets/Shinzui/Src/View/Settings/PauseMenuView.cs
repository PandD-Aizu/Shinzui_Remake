using System;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Shinzui.View.UI;

namespace Shinzui.View.Settings
{
    /// <summary>ゲームを停止してポーズメニューと設定項目を表示する</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class PauseMenuView : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        public TMP_FontAsset Font => font;
        private GameObject _overlay;
        private GameObject _menu;
        private GameObject _options;
        private UnityEngine.UI.Button _graphicsButton;
        private UnityEngine.UI.Button _resume;
        private UnityEngine.UI.Button _optionButton;
        private readonly List<Behaviour> _cameraInputs = new();
        private float _previousTimeScale;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;
        private GameObject _previousSelection;
        private int _resumeFrame = -1;
        private TMP_Text[] _sliderValues;
        private float[] _shownValues;

        public event Action OptionsOpened;
        public event Action OptionsClosed;
        public event Action GraphicsOpened;
        public event Action TitleRequested;
        public bool IsPaused { get; private set; }
        public bool IsBusy { get; set; }
        public UnityEngine.UI.Slider[] Sliders { get; private set; }
        public UnityEngine.UI.Toggle CenterDot { get; private set; }
        public GraphicsOptionsView Graphics { get; private set; }

        /// <summary>画面全体を覆うメニューと設定パネルを構築する</summary>
        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 既存のEventSystemを利用し、存在しないシーンだけ補完する
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("PauseEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(transform);

            // 止まったゲーム画面を左側ほど深く沈める暗幕
            var overlay = RequiemUiTheme.Stretch("Overlay", transform);
            overlay.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.6f);
            RequiemUiTheme.AddVignette(overlay);
            var shade = RequiemUiTheme.Backdrop;
            shade.a = 0.92f;
            RequiemUiTheme.Decoration("Shade", overlay, shade, RequiemUiTheme.FadeRight);
            _overlay = overlay.gameObject;
            Footer(overlay);

            // 左寄せのメニュー一覧
            _menu = Panel("Menu", overlay, new Vector2(620, 520)).gameObject;
            Heading(_menu.transform, "ポーズ", "PAUSE", 620, 200);
            _resume = Button(_menu.transform, "もどる", 70, 620, Resume);
            _optionButton = Button(_menu.transform, "オプション", 0, 620, OpenOptions);
            Button(_menu.transform, "タイトルに戻る", -70, 620, () => TitleRequested?.Invoke());

            // 項目名と細いスライダーを一行に並べた設定一覧
            _options = Panel("Options", overlay, new Vector2(960, 920)).gameObject;
            Heading(_options.transform, "オプション", "OPTIONS", 960, 400);
            string[] labels = { "全体音量", "BGM音量", "効果音音量", "ボイス音量", "コントローラー感度", "マウス感度", "明るさ" };
            Sliders = new UnityEngine.UI.Slider[labels.Length];
            _sliderValues = new TMP_Text[labels.Length];
            _shownValues = new float[labels.Length];
            for (int i = 0; i < labels.Length; i++)
                Sliders[i] = Slider(labels[i], 300 - i * 66, i == 4 || i == 5, i);
            CenterDot = Toggle("中央のドットを表示", 300 - labels.Length * 66);
            _graphicsButton = Button(_options.transform, "グラフィック設定  ›", 300 - (labels.Length + 1) * 66, 960, OpenGraphics);
            Button(_options.transform, "もどる", -310, 960, CloseOptions);

            // プリセットと個別項目を切り替えるグラフィック設定画面
            Graphics = GraphicsOptionsView.Create(overlay, font, new Vector2(160f, 20f));
            Graphics.BackRequested += CloseGraphics;
            Graphics.gameObject.SetActive(false);
            _overlay.SetActive(false);
        }

        /// <summary>Escでポーズを切り替え、再開操作のフレームでは入力を止めたままにする</summary>
        private void Update()
        {
            if (_resumeFrame >= 0 && Time.frameCount > _resumeFrame)
            {
                _resumeFrame = -1;
                RestoreGame();
            }

            if (IsBusy || Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            if (!IsPaused) Pause();
            else if (Graphics.gameObject.activeSelf) CloseGraphics();
            else if (_options.activeSelf) CloseOptions();
            else Resume();
        }

        /// <summary>他の画面や設定反映によるカーソルの再ロックを防ぎ、設定の数値表示を更新する</summary>
        private void LateUpdate()
        {
            if (!IsPaused) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (!_options.activeSelf) return;

            // 値が変わった項目だけ数値の文字を更新する
            for (int i = 0; i < Sliders.Length; i++)
            {
                float value = Sliders[i].value;
                if (_shownValues[i] == value) continue;
                _shownValues[i] = value;
                _sliderValues[i].text = (Sliders[i].maxValue > 1f ? value : value * 100f).ToString("0");
            }
        }

        /// <summary>時間とカメラ入力の状態を保存して停止する</summary>
        public void Pause()
        {
            if (IsPaused || Time.timeScale <= 0f) return;
            _previousTimeScale = Time.timeScale;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _cameraInputs.Clear();
            foreach (var input in FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None))
            {
                if (!input.enabled) continue;
                _cameraInputs.Add(input);
                input.enabled = false;
            }

            IsPaused = true;
            Time.timeScale = 0f;
            _overlay.SetActive(true);
            _options.SetActive(false);
            Graphics.gameObject.SetActive(false);
            _menu.SetActive(true);
            _resume.Select();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>ポーズメニューを閉じ、次フレームにゲームを再開する</summary>
        public void Resume()
        {
            if (!IsPaused || IsBusy || _resumeFrame >= 0) return;
            if (Graphics.gameObject.activeSelf) CloseGraphics();
            if (_options.activeSelf) CloseOptions();
            _overlay.SetActive(false);
            _resumeFrame = Time.frameCount;
        }

        /// <summary>設定パネルを開く</summary>
        private void OpenOptions()
        {
            if (IsBusy) return;
            OptionsOpened?.Invoke();
            _menu.SetActive(false);
            _options.SetActive(true);
            Sliders[0].Select();
        }

        /// <summary>設定を確定してポーズメニューへ戻る</summary>
        private void CloseOptions()
        {
            if (IsBusy) return;
            OptionsClosed?.Invoke();
            _options.SetActive(false);
            _menu.SetActive(true);
            _optionButton.Select();
        }

        /// <summary>設定一覧からグラフィック設定画面へ切り替える</summary>
        private void OpenGraphics()
        {
            if (IsBusy) return;
            _options.SetActive(false);
            Graphics.gameObject.SetActive(true);
            GraphicsOpened?.Invoke();
            Graphics.Focus();
        }

        /// <summary>グラフィック設定画面から設定一覧へ戻る</summary>
        private void CloseGraphics()
        {
            if (IsBusy) return;
            Graphics.gameObject.SetActive(false);
            _options.SetActive(true);
            _graphicsButton.Select();
        }

        /// <summary>ゲームの時間、カメラ入力、カーソルを元に戻す</summary>
        private void RestoreGame()
        {
            if (!IsPaused) return;
            Time.timeScale = _previousTimeScale;
            foreach (var input in _cameraInputs)
                if (input != null) input.enabled = true;
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_previousSelection);
            IsPaused = false;
        }

        /// <summary>シーン遷移や無効化時にも停止状態を解除する</summary>
        private void OnDisable()
        {
            RestoreGame();
        }

        /// <summary>中央基準のUI領域を生成する</summary>
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

        /// <summary>日本語フォントでラベルを生成する</summary>
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
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = RequiemUiTheme.Text;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>画面左側に寄せたパネルを生成する</summary>
        /// <param name="name">オブジェクト名</param>
        /// <param name="parent">配置先</param>
        /// <param name="size">表示サイズ</param>
        /// <returns>生成したパネル</returns>
        private static RectTransform Panel(string name, Transform parent, Vector2 size)
        {
            var rect = Rect(name, parent, size);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(160f, 20f);
            return rect;
        }

        /// <summary>英字の小見出しと赤い線を添えた見出しを生成する</summary>
        /// <param name="parent">配置先</param>
        /// <param name="text">見出し</param>
        /// <param name="caption">英字の小見出し</param>
        /// <param name="width">表示幅</param>
        /// <param name="y">縦位置</param>
        private void Heading(Transform parent, string text, string caption, float width, float y)
        {
            var heading = Label(parent, text, new Vector2(0, y), new Vector2(width, 70), 48);
            heading.alignment = TextAlignmentOptions.Left;
            RequiemUiTheme.DecorateHeading(heading, caption);
        }

        /// <summary>選択時に赤い帯が出る一覧形式のボタンを生成する</summary>
        /// <param name="parent">配置先</param>
        /// <param name="text">表示文字列</param>
        /// <param name="y">縦位置</param>
        /// <param name="width">表示幅</param>
        /// <param name="action">押下時の処理</param>
        /// <returns>生成したボタン</returns>
        private UnityEngine.UI.Button Button(Transform parent, string text, float y, float width, Action action)
        {
            var rect = Rect(text, parent, new Vector2(width, 60));
            rect.anchoredPosition = new Vector2(0, y);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => { if (!IsBusy) action(); });
            var label = Label(rect, text, Vector2.zero, rect.sizeDelta, 30);
            RequiemUiTheme.StyleRowButton(button, label);
            return button;
        }

        /// <summary>設定一覧の一行分の背景と項目名を生成する</summary>
        /// <param name="text">項目名</param>
        /// <param name="y">縦位置</param>
        /// <param name="label">生成した項目名</param>
        /// <returns>生成した行</returns>
        private RectTransform Row(string text, float y, out TMP_Text label)
        {
            var row = Rect(text, _options.transform, new Vector2(960, 60));
            row.anchoredPosition = new Vector2(0, y);
            row.gameObject.AddComponent<UnityEngine.UI.Image>();
            var rule = RequiemUiTheme.Decoration("Rule", row, RequiemUiTheme.Line);
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(0f, 1f);
            label = Label(row, text, new Vector2(-220, 0), new Vector2(520, 60), 27);
            label.alignment = TextAlignmentOptions.Left;
            label.margin = new Vector4(44f, 0f, 0f, 0f);
            return row;
        }

        /// <summary>設定値を調整する細いスライダーを生成する</summary>
        /// <param name="text">項目名</param>
        /// <param name="y">縦位置</param>
        /// <param name="sensitivity">感度設定の範囲を使用するか</param>
        /// <param name="index">数値表示の格納先</param>
        /// <returns>生成したスライダー</returns>
        private UnityEngine.UI.Slider Slider(string text, float y, bool sensitivity, int index)
        {
            var row = Row(text, y, out var label);
            var rect = Rect("Slider", row, new Vector2(340, 32));
            rect.anchoredPosition = new Vector2(230, 0);
            rect.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.clear;

            // 中央を通る細い線と赤い進捗
            var track = RequiemUiTheme.Decoration("Background", rect, Color.white);
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0f, 2f);
            var fill = RequiemUiTheme.Decoration("Fill", track.transform, Color.white);

            // 操作位置を示す縦長のつまみ
            var handle = Rect("Handle", rect, new Vector2(4, 0));
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.targetGraphic = handle.gameObject.AddComponent<UnityEngine.UI.Image>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            handle.sizeDelta = new Vector2(4, 0);
            slider.minValue = sensitivity ? 1 : 0;
            slider.maxValue = sensitivity ? 100 : 1;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            RequiemUiTheme.StyleSlider(slider);

            _sliderValues[index] = Label(row, "0", new Vector2(440, 0), new Vector2(70, 60), 25);
            RequiemUiTheme.StyleRow(row.GetComponent<UnityEngine.UI.Image>(), slider, label);
            return slider;
        }

        /// <summary>行全体で切り替えられるトグルを生成する</summary>
        /// <param name="text">項目名</param>
        /// <param name="y">縦位置</param>
        /// <returns>生成したトグル</returns>
        private UnityEngine.UI.Toggle Toggle(string text, float y)
        {
            var row = Row(text, y, out var label);

            // 行全体を押下範囲にし、右側の四角で状態を示す
            var hit = RequiemUiTheme.Stretch("Toggle", row);
            hit.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.clear;
            var toggle = hit.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            var box = Rect("Box", hit, new Vector2(28, 28));
            box.anchoredPosition = new Vector2(74, 0);
            toggle.targetGraphic = box.gameObject.AddComponent<UnityEngine.UI.Image>();
            toggle.graphic = Rect("Check", box, Vector2.zero).gameObject.AddComponent<UnityEngine.UI.Image>();
            RequiemUiTheme.StyleToggle(toggle);
            RequiemUiTheme.StyleRow(row.GetComponent<UnityEngine.UI.Image>(), toggle, label);
            return toggle;
        }

        /// <summary>画面下部に区切り線と操作説明を表示する</summary>
        /// <param name="parent">配置先</param>
        private void Footer(Transform parent)
        {
            var rule = RequiemUiTheme.Decoration("FooterRule", parent, RequiemUiTheme.Line);
            rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);
            rule.rectTransform.sizeDelta = new Vector2(-320f, 1f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, 96f);

            // キー名を明るく、操作名を控えめに表示する
            var hint = Label(parent, "←→  <color=#85807A>変更</color>      Enter  <color=#85807A>決定</color>      Esc  <color=#85807A>戻る</color>", Vector2.zero, new Vector2(800, 60), 24);
            var rect = hint.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-160f, 30f);
            hint.alignment = TextAlignmentOptions.Right;
        }
    }
}
