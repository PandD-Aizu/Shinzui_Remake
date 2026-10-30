using System;
using System.Collections;
using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.StageResults;
using Shinzui.View.Settings;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shinzui.View.UI
{
    /// <summary>フレームキャプチャと停止中の結果画面演出を描画する</summary>
    public sealed class StageResultView : MonoBehaviour
    {
        private static readonly Color Gold = new(0.86f, 0.68f, 0.22f, 1f);
        private static readonly Color Silver = new(0.55f, 0.55f, 0.55f, 0.85f);
        private static Sprite _medalSprite;
        private readonly List<Behaviour> _disabledInputs = new();
        private StageResult _result;
        private TMP_FontAsset _font;
        private Texture2D _capture;
        private Material _rankMaterial;
        private GameObject _content;
        private UnityEngine.UI.Image _fade;
        private CanvasGroup _header;
        private CanvasGroup _timeGroup;
        private CanvasGroup _totalGroup;
        private CanvasGroup _rankLabel;
        private CanvasGroup _rankGroup;
        private CanvasGroup[] _rows;
        private TMP_Text _timeText;
        private TMP_Text _totalText;
        private TMP_Text _rankText;
        private TMP_Text _footer;
        private RectTransform _totalIcon;
        private TMP_Text _pointsLabel;
        private InputAction _submit;
        private float _previousTimeScale;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;
        private bool _ownsPause;
        private bool _revealing;
        private bool _skip;
        private bool _ready;
        private int _readyFrame;
        private int _shownMedals;
        private bool _rankSoundPlayed;
        private float _footerTime;
        private int _displayedPoints = -1;

        public event Action ContinueRequested;
        public event Action MedalRevealed;
        public event Action RankRevealed;

        /// <summary>ポーズ画面と共通のフォントを設定する</summary>
        /// <param name="font">日本語フォント</param>
        public void ConfigureFont(TMP_FontAsset font)
        {
            // 組み立て時に共有フォントを受け取る
            _font = font;
        }

        /// <summary>入力を停止してキャプチャと結果演出を開始する</summary>
        /// <param name="result">確定した評価結果</param>
        public void Show(StageResult result)
        {
            // 時間とカーソルの元の状態を保持する
            _result = result;
            if (_font == null) _font = TMP_Settings.defaultFontAsset;
            _previousTimeScale = Time.timeScale;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _ownsPause = true;
            foreach (var input in FindObjectsByType<CinemachineInputAxisController>())
                DisableInput(input);
            foreach (var pause in FindObjectsByType<PauseMenuView>())
                DisableInput(pause);
            foreach (var module in FindObjectsByType<UnityEngine.EventSystems.BaseInputModule>())
                DisableInput(module);

            // プロジェクトのSubmit設定を複製してゲーム入力と独立させる
            _submit = InputSystem.actions?.FindAction("UI/Submit")?.Clone();
            if (_submit == null)
            {
                _submit = new InputAction("ResultSubmit", InputActionType.Button);
                _submit.AddBinding("<Keyboard>/enter");
                _submit.AddBinding("<Keyboard>/numpadEnter");
                _submit.AddBinding("<Keyboard>/space");
                _submit.AddBinding("<Gamepad>/buttonSouth");
            }
            _submit.Enable();
            Cursor.visible = false;
            StartCoroutine(Present());
        }

        /// <summary>同一シーンの操作と共有UIの入力を停止する</summary>
        /// <param name="input">停止する入力処理</param>
        private void DisableInput(Behaviour input)
        {
            // 決定が背面のボタンへ届かないようにUI入力も占有する
            if (!input.enabled) return;
            if (input is not UnityEngine.EventSystems.BaseInputModule && input.gameObject.scene != gameObject.scene) return;
            _disabledInputs.Add(input);
            input.enabled = false;
        }

        /// <summary>暗転前のフレームを取得し、停止して結果を表示する</summary>
        /// <returns>画面遷移のコルーチン</returns>
        private IEnumerator Present()
        {
            // UIを生成する前に実際に描画されたフレームを取得する
            yield return new WaitForEndOfFrame();
            CaptureBackground();
            Time.timeScale = 0f;
            BuildCanvas();
            yield return Fade(0f, 1f, 0.6f);

            // 背景だけを表示してから情報の順次開示を開始する
            _content.SetActive(true);
            yield return Fade(1f, 0f, 0.5f);
            _revealing = true;
            float elapsed = 0f;
            float duration = RevealDuration();
            while (elapsed < duration && !_skip)
            {
                RenderReveal(elapsed, true);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // スキップ時も最終表示値を直接確定し、同じ押下で遷移しない
            RenderReveal(duration, false);
            _revealing = false;
            _ready = true;
            _readyFrame = Time.frameCount;
            _footerTime = 0f;
            _footer.alpha = 1f;
        }

        /// <summary>画面を一度だけグレースケールの22%輝度へ変換する</summary>
        private void CaptureBackground()
        {
            // キャプチャ失敗時だけ単色背景へフォールバックする
            try
            {
                _capture = ScreenCapture.CaptureScreenshotAsTexture();
                if (_capture == null) return;
                var pixels = _capture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    var pixel = pixels[i];
                    byte gray = (byte)((pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f) * 0.22f);
                    pixels[i] = new Color32(gray, gray, gray, 255);
                }
                _capture.SetPixels32(pixels);
                _capture.Apply(false, true);
            }
            catch (Exception exception)
            {
                if (_capture != null) Destroy(_capture);
                _capture = null;
                Debug.LogWarning($"[StageResult] 画面取得に失敗したため単色背景を使用: {exception.Message}");
            }
        }

        /// <summary>キーボードとコントローラーとクリックの決定を処理する</summary>
        private void Update()
        {
            // 演出中の決定は省略だけを行い、次の押下で遷移する
            if (_submit == null) return;
            bool confirm = _submit.WasPressedThisFrame() || Mouse.current?.leftButton.wasPressedThisFrame == true;
            if (confirm && _revealing) _skip = true;
            else if (confirm && _ready && Time.frameCount > _readyFrame)
            {
                _ready = false;
                _footer.alpha = 0f;
                StartCoroutine(Leave());
            }

            // 確定後だけ2.4秒周期で操作案内を明滅する
            if (!_ready) return;
            _footerTime += Time.unscaledDeltaTime;
            _footer.alpha = 0.725f + 0.275f * Mathf.Cos(_footerTime * Mathf.PI * 2f / 2.4f);
        }

        /// <summary>他のHUD処理がカーソルを表示しても非表示を維持する</summary>
        private void LateUpdate()
        {
            // クリック入力はカーソル位置に依存させない
            if (_ownsPause) Cursor.visible = false;
        }

        /// <summary>暗転を完了してからシーン遷移を依頼する</summary>
        /// <returns>終了演出のコルーチン</returns>
        private IEnumerator Leave()
        {
            // 操作を確定した後はロード完了か失敗通知を待つ
            yield return Fade(0f, 1f, 0.6f);
            ContinueRequested?.Invoke();
        }

        /// <summary>読み込み失敗時に結果画面を再表示する</summary>
        public void ShowLoadFailure()
        {
            // 次の決定で同じSaveStageへの遷移を再試行する
            if (!this || !_ownsPause) return;
            Time.timeScale = 0f;
            StartCoroutine(Recover());
        }

        /// <summary>次のシーンが通常の時間で初期化されるように停止を解除する</summary>
        public void PrepareSceneLoad()
        {
            // 画面は暗転したまま、プレイヤー入力と敵AIの停止は維持する
            Time.timeScale = _previousTimeScale;
        }

        /// <summary>再試行の案内を表示して決定入力を戻す</summary>
        /// <returns>復帰演出のコルーチン</returns>
        private IEnumerator Recover()
        {
            // 暗幕を戻して操作可能な最終状態へ復帰する
            yield return Fade(1f, 0f, 0.5f);
            _footer.text = "読み込み失敗　決定で再試行";
            _readyFrame = Time.frameCount;
            _ready = true;
        }

        /// <summary>非スケール時間で黒い暗幕を補間する</summary>
        /// <param name="from">開始時の不透明度</param>
        /// <param name="to">終了時の不透明度</param>
        /// <param name="duration">秒数</param>
        /// <returns>暗転処理のコルーチン</returns>
        private IEnumerator Fade(float from, float to, float duration)
        {
            // timeScaleが0でも一定時間で完了する
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                _fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, elapsed / duration));
                yield return null;
            }
            _fade.color = new Color(0f, 0f, 0f, to);
        }

        /// <summary>ランク表示を始める時刻を求める</summary>
        /// <returns>開示開始からの秒数</returns>
        private float RankStart()
        {
            // 最後の行の開始から0.4秒待つ
            return 0.6f + Mathf.Max(0, _rows.Length - 1) * 0.14f + 0.4f;
        }

        /// <summary>フッターを表示する時刻を求める</summary>
        /// <returns>開示演出全体の秒数</returns>
        private float RevealDuration()
        {
            // S評価だけ一度の発光演出を追加する
            return RankStart() + 0.2f + 0.35f + (_result.Rank == "S" ? 0.6f : 0f) + 0.5f;
        }

        /// <summary>指定時刻の開示状態をまとめて描画する</summary>
        /// <param name="time">開示開始からの秒数</param>
        /// <param name="playSounds">新しく開示した項目の音を通知するか</param>
        private void RenderReveal(float time, bool playSounds)
        {
            // 見出しとクリア時間を先に開示する
            _header.alpha = Mathf.Clamp01(time / 0.3f);
            _totalGroup.alpha = _header.alpha;
            _timeGroup.alpha = Mathf.Clamp01((time - 0.3f) / 0.3f);
            float count = Mathf.Clamp01((time - 0.3f) / 0.8f);
            _timeText.text = FormatTime(_result.Stats.ClearSeconds * (1f - Mathf.Pow(1f - count, 3f)));

            // 行の開始時点で得点を加え、直近の獲得だけ数値を拡大する
            int points = 0;
            float lastEarned = float.NegativeInfinity;
            for (int i = 0; i < _rows.Length; i++)
            {
                float start = 0.6f + i * 0.14f;
                float progress = Mathf.Clamp01((time - start) / 0.25f);
                _rows[i].alpha = progress;
                ((RectTransform)_rows[i].transform).anchoredPosition = new Vector2(120f - 16f * (1f - progress), -(196f + i * 54f));
                if (time < start) continue;
                if (_result.Earned[i])
                {
                    points += _result.Rules.Medals[i].Points;
                    lastEarned = start;
                    if (playSounds && i >= _shownMedals) MedalRevealed?.Invoke();
                }
                _shownMedals = Math.Max(_shownMedals, i + 1);
            }
            SetTotal(points);
            _totalText.rectTransform.localScale = Vector3.one * (1f + 0.15f * (1f - Mathf.Clamp01((time - lastEarned) / 0.15f)));

            // 評価ラベルの後にランクを軽く行き過ぎる拡大補間で押し出す
            float rankStart = RankStart();
            _rankLabel.alpha = Mathf.Clamp01((time - rankStart) / 0.2f);
            float rank = Mathf.Clamp01((time - rankStart - 0.2f) / 0.35f);
            _rankGroup.alpha = rank;
            float x = rank - 1f;
            float easeBack = 1f + 1.8f * x * x * x + 0.8f * x * x;
            _rankGroup.transform.localScale = Vector3.one * Mathf.LerpUnclamped(1.8f, 1f, easeBack);
            if (playSounds && rank > 0f && !_rankSoundPlayed)
            {
                _rankSoundPlayed = true;
                RankRevealed?.Invoke();
            }

            // Sの赤い発光は一度だけ強めて元の柔らかい光へ戻す
            if (_rankMaterial != null)
            {
                float flash = Mathf.Clamp01((time - rankStart - 0.55f) / 0.6f);
                var glow = RequiemUiTheme.AccentBright;
                glow.a = 0.18f + 0.5f * Mathf.Sin(flash * Mathf.PI);
                _rankMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, glow);
            }
        }

        /// <summary>時間を総分数と秒と百分秒へ整形する</summary>
        /// <param name="seconds">経過秒数</param>
        /// <returns>mm:ss.ff形式の文字列</returns>
        private static string FormatTime(double seconds)
        {
            // 時間を超えても分数を繰り上げて表示する
            long hundredths = (long)(Math.Max(0, seconds) * 100);
            return $"{hundredths / 6000:00}:{hundredths / 100 % 60:00}.{hundredths % 100:00}";
        }

        /// <summary>得点と単位を同じベースラインで中央に並べる</summary>
        /// <param name="points">現在の獲得点</param>
        private void SetTotal(int points)
        {
            // 桁数が変化したときも左列の中央を保つ
            if (_displayedPoints == points) return;
            _displayedPoints = points;
            _totalText.text = points.ToString();
            float numberWidth = _totalText.GetPreferredValues(_totalText.text).x;
            float unitWidth = _pointsLabel.GetPreferredValues("点").x;
            float left = 600f - (36f + 14f + numberWidth + 12f + unitWidth) * 0.5f;
            _totalIcon.anchoredPosition = new Vector2(left, -864f);
            _totalText.rectTransform.anchoredPosition = new Vector2(left + 50f, -900f);
            _totalText.rectTransform.sizeDelta = new Vector2(numberWidth, 80f);
            _pointsLabel.rectTransform.anchoredPosition = new Vector2(left + 62f + numberWidth, -900f);
        }

        /// <summary>指定寸法で全画面Canvasと結果の各要素を生成する</summary>
        private void BuildCanvas()
        {
            // HUDとポーズ画面より上の描画順を使う
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // キャプチャの上に周辺減光と左側の暗幕を重ねる
            var content = RequiemUiTheme.Stretch("ResultContent", transform);
            _content = content.gameObject;
            var background = RequiemUiTheme.Decoration("Background", content, RequiemUiTheme.Backdrop);
            if (_capture != null)
            {
                var raw = RequiemUiTheme.Stretch("CapturedFrame", content).gameObject.AddComponent<UnityEngine.UI.RawImage>();
                raw.texture = _capture;
                raw.raycastTarget = false;
            }
            var shading = RequiemUiTheme.Stretch("Shading", content);
            RequiemUiTheme.AddVignette(shading);
            var shade = RequiemUiTheme.Backdrop;
            shade.a = 0.9f;
            RequiemUiTheme.Decoration("LeftShade", shading, shade, RequiemUiTheme.FadeRight);
            background.raycastTarget = true;

            // 左列の見出しとメダルを固定寸法で配置する
            _header = Group("Header", content);
            Label(_header.transform, _result.Rules.StageName, 120, 96, 960, 44, 34, RequiemUiTheme.Text);
            Line(_header.transform, 156);
            _rows = new CanvasGroup[_result.Rules.Medals.Count];
            for (int i = 0; i < _rows.Length; i++)
            {
                var row = Rect("MedalRow", content, 120, 196 + i * 54, 960, 54);
                _rows[i] = row.gameObject.AddComponent<CanvasGroup>();
                _rows[i].alpha = 0f;
                var medal = _result.Rules.Medals[i];
                var color = _result.Earned[i] ? RequiemUiTheme.Text : RequiemUiTheme.TextDim;
                Icon(row, 10, 12, 30, _result.Earned[i] ? Gold : Silver);
                Label(row, medal.Name, 70, 0, 230, 54, 28, color);
                Label(row, medal.ConditionText, 320, 0, 640, 54, 26, color);
            }

            // 合計点の数字と単位だけを共通のベースラインへそろえる
            _totalGroup = Group("Total", content);
            Line(_totalGroup.transform, 820);
            _totalIcon = Icon(_totalGroup.transform, 0, 864, 36, Gold);
            _totalText = Label(_totalGroup.transform, "0", 0, 900, 120, 80, 60, RequiemUiTheme.TextActive);
            _pointsLabel = Label(_totalGroup.transform, "点", 0, 900, 60, 80, 30, RequiemUiTheme.Text);
            _totalText.alignment = _pointsLabel.alignment = TextAlignmentOptions.BaselineLeft;
            _totalText.rectTransform.pivot = _pointsLabel.rectTransform.pivot = new Vector2(0f, 0.5f);

            // 右列にクリア時間と評価を配置する
            _timeGroup = Group("ClearTime", content);
            Label(_timeGroup.transform, "クリア時間", 1240, 170, 560, 42, 28, RequiemUiTheme.Text);
            _timeText = Label(_timeGroup.transform, "00:00.00", 1270, 232, 530, 48, 34, RequiemUiTheme.TextActive);
            _rankLabel = Group("RankLabel", content);
            Label(_rankLabel.transform, "評価", 1260, 860, 140, 40, 28, RequiemUiTheme.Text);
            var rankRect = Rect("Rank", content, 1590, 880, 340, 280);
            rankRect.pivot = new Vector2(0.5f, 0.5f);
            _rankGroup = rankRect.gameObject.AddComponent<CanvasGroup>();
            _rankGroup.alpha = 0f;
            var rankColor = _result.Rank switch
            {
                "S" => RequiemUiTheme.TextActive,
                "A" => RequiemUiTheme.Text,
                "B" => RequiemUiTheme.TextDim,
                _ => RequiemUiTheme.TextDisabled
            };
            _rankText = Label(rankRect, _result.Rank, 0, 0, 340, 280, 220, rankColor);
            _rankText.alignment = TextAlignmentOptions.Center;

            // 文字の実幅に合わせ、左端1420pxと拡大の中心を両立する
            float rankWidth = _rankText.GetPreferredValues(_result.Rank).x;
            rankRect.sizeDelta = _rankText.rectTransform.sizeDelta = new Vector2(rankWidth, 280f);
            rankRect.anchoredPosition = new Vector2(1420f + rankWidth * 0.5f, -880f);
            if (_result.Rank == "S") ConfigureRankGlow();

            // フッターは右下に固定し、全演出後にだけ表示する
            _footer = Label(content, "決定　セーブステージへ", 0, 0, 600, 36, 24, RequiemUiTheme.TextDim);
            _footer.alignment = TextAlignmentOptions.Right;
            _footer.rectTransform.anchorMin = _footer.rectTransform.anchorMax = _footer.rectTransform.pivot = new Vector2(1f, 0f);
            _footer.rectTransform.anchoredPosition = new Vector2(-120f, 70f);
            _footer.alpha = 0f;
            _content.SetActive(false);
            _fade = RequiemUiTheme.Decoration("Fade", transform, Color.clear);
            _fade.raycastTarget = true;
        }

        /// <summary>S評価にだけ専用マテリアルの柔らかい下地を追加する</summary>
        private void ConfigureRankGlow()
        {
            // 共有フォントマテリアルを変更せず、この画面だけの発光にする
            _rankMaterial = new Material(_rankText.fontSharedMaterial);
            _rankMaterial.EnableKeyword("UNDERLAY_ON");
            _rankMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.65f);
            _rankMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
            _rankMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.04f);
            _rankMaterial.SetColor(ShaderUtilities.ID_OutlineColor, RequiemUiTheme.AccentBright);
            _rankText.fontSharedMaterial = _rankMaterial;
            _rankText.UpdateMeshPadding();
        }

        /// <summary>左上基準の矩形を生成する</summary>
        /// <param name="name">オブジェクト名</param>
        /// <param name="parent">配置先</param>
        /// <param name="x">左からの距離</param>
        /// <param name="y">上からの距離</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <returns>生成した矩形</returns>
        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            // 指定値を1920x1080の基準座標として扱う
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>全画面サイズの透明な演出グループを生成する</summary>
        /// <param name="name">オブジェクト名</param>
        /// <param name="parent">配置先</param>
        /// <returns>生成したグループ</returns>
        private static CanvasGroup Group(string name, Transform parent)
        {
            // 子の文字と線を同時にフェードできるようにする
            var group = RequiemUiTheme.Stretch(name, parent).gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            return group;
        }

        /// <summary>指定色とサイズで日本語ラベルを生成する</summary>
        /// <param name="parent">配置先</param>
        /// <param name="text">表示文字</param>
        /// <param name="x">左からの距離</param>
        /// <param name="y">上からの距離</param>
        /// <param name="width">幅</param>
        /// <param name="height">高さ</param>
        /// <param name="fontSize">文字サイズ</param>
        /// <param name="color">文字色</param>
        /// <returns>生成した文字</returns>
        private TMP_Text Label(Transform parent, string text, float x, float y, float width, float height, float fontSize, Color color)
        {
            // 自動縮小を使わず指定の文字寸法を維持する
            var label = Rect(text, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>左列の幅いっぱいに1pxの区切り線を引く</summary>
        /// <param name="parent">配置先</param>
        /// <param name="y">上からの距離</param>
        private static void Line(Transform parent, float y)
        {
            // 共通テーマの薄い線を使う
            var image = Rect("Line", parent, 120, y, 960, 1).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = RequiemUiTheme.Line;
            image.raycastTarget = false;
        }

        /// <summary>共通の円形メダル画像を配置する</summary>
        /// <param name="parent">配置先</param>
        /// <param name="x">左からの距離</param>
        /// <param name="y">上からの距離</param>
        /// <param name="size">一辺の寸法</param>
        /// <param name="color">獲得状態の色</param>
        /// <returns>メダルの矩形</returns>
        private static RectTransform Icon(Transform parent, float x, float y, float size, Color color)
        {
            // 一度生成した円形スプライトを全メダルで共有する
            if (_medalSprite == null) _medalSprite = CreateMedalSprite();
            var rect = Rect("Medal", parent, x, y, size, size);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = _medalSprite;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>細い暗色の内周線を持つ円形スプライトを生成する</summary>
        /// <returns>キャッシュ用のメダル画像</returns>
        private static Sprite CreateMedalSprite()
        {
            // 高解像度の円周を平滑化して小さな表示でも輪郭を保つ
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "StageResultMedal";
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64f, 64f));
                float alpha = Mathf.Clamp01(62f - radius);
                float ring = Mathf.Clamp01(1.5f - Mathf.Abs(radius - 52f));
                float shade = Mathf.Lerp(1f, 0.55f, ring);
                pixels[y * size + x] = new Color(shade, shade, shade, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>画面破棄時に時間と入力と描画用リソースを戻す</summary>
        private void OnDestroy()
        {
            // シーン遷移や外部からの破棄でも停止状態を残さない
            _submit?.Dispose();
            if (_ownsPause)
            {
                Time.timeScale = _previousTimeScale;
                Cursor.lockState = _previousCursorLock;
                Cursor.visible = _previousCursorVisible;
                foreach (var input in _disabledInputs)
                    if (input != null) input.enabled = true;
            }

            // キャプチャとこの画面専用のマテリアルを解放する
            if (_capture != null) Destroy(_capture);
            if (_rankMaterial != null) Destroy(_rankMaterial);
        }
    }
}
