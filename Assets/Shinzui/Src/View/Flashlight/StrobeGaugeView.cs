using TMPro;
using UnityEngine;

namespace Shinzui.View.Flashlight
{
    /// <summary>
    /// ストロボの電池残量ゲージと予備の乾電池を表示するUI制御クラス
    /// </summary>
    public class StrobeGaugeView : MonoBehaviour
    {
        [Header("UI Components")]
        [Tooltip("電池残量を描く円弧")]
        [SerializeField] private StrobeArcGraphic arcGraphic;

        [Tooltip("ゲージ全体を表示・非表示にするための親オブジェクト")]
        [SerializeField] private GameObject rootObject;

        [Tooltip("ゲージ全体をフェードさせる CanvasGroup")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("表示")]
        [Tooltip("構えているだけの時の不透明度")]
        [SerializeField, Range(0f, 1f)] private float readyAlpha = 0.15f;

        [Tooltip("フェードにかかる秒数")]
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;

        [Tooltip("消費後に残像が追いつくまでの時間")]
        [SerializeField, Min(0.01f)] private float trailSmoothTime = 0.25f;

        [Header("予備の乾電池")]
        [Tooltip("予備の乾電池1本ごとのアイコン（並び順に点灯）")]
        [SerializeField] private GameObject[] batteryIcons;

        [Tooltip("アイコン数を超えた本数を表示するテキスト")]
        [SerializeField] private TMP_Text batteryCountText;

        private CanvasGroup[] _batteryGroups;
        private float _energy = 1f;
        private float _cost;
        private float _trail = 1f;
        private float _trailVelocity;
        private float _activity;
        private float _flash;
        private float _time;
        private float _targetAlpha;
        private bool _charging;
        private int _batteryCount;

        private void Awake()
        {
            // アイコンごとにフェード用の CanvasGroup を用意
            _batteryGroups = new CanvasGroup[batteryIcons?.Length ?? 0];
            for (int i = 0; i < _batteryGroups.Length; i++)
            {
                if (batteryIcons[i] == null) continue;
                if (!batteryIcons[i].TryGetComponent(out _batteryGroups[i]))
                {
                    _batteryGroups[i] = batteryIcons[i].AddComponent<CanvasGroup>();
                }
                _batteryGroups[i].alpha = 0f;
            }
        }

        private void Start()
        {
            if (rootObject == null)
            {
                rootObject = gameObject;
            }

            // 初期状態は透明にして非表示
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            rootObject.SetActive(false);
        }

        private void Update()
        {
            // ポーズ中でもフェードを止めないよう実時間で進める
            float deltaTime = Time.unscaledDeltaTime;
            _time += deltaTime;

            // 構え、チャージ、非表示の切り替えをフェードでつなぎ、消え切ったら止める
            float fadeStep = deltaTime / fadeDuration;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _targetAlpha, fadeStep);
                if (_targetAlpha <= 0f && canvasGroup.alpha <= 0f) rootObject.SetActive(false);
            }

            // 予備の乾電池も増減をフェードで見せる
            for (int i = 0; i < _batteryGroups.Length; i++)
            {
                if (_batteryGroups[i] == null) continue;
                _batteryGroups[i].alpha = Mathf.MoveTowards(_batteryGroups[i].alpha, i < _batteryCount ? 1f : 0f, fadeStep);
            }

            if (batteryCountText != null)
            {
                bool overflow = _batteryCount > _batteryGroups.Length;
                batteryCountText.alpha = Mathf.MoveTowards(batteryCountText.alpha, overflow ? 1f : 0f, fadeStep);
                if (overflow) batteryCountText.text = $"×{_batteryCount}";
            }

            // 残量が減った時だけ残像を遅れて追従させる
            if (_energy >= _trail)
            {
                _trail = _energy;
                _trailVelocity = 0f;
            }
            else
            {
                _trail = Mathf.SmoothDamp(_trail, _energy, ref _trailVelocity, trailSmoothTime,
                    Mathf.Infinity, deltaTime);
            }

            // チャージ中の揺れと電池交換時の発光を減衰させながら円弧へ渡す
            float blend = 1f - Mathf.Exp(-10f * deltaTime);
            _activity = Mathf.Lerp(_activity, _charging ? 1f : 0f, blend);
            _flash = Mathf.MoveTowards(_flash, 0f, deltaTime * 1.5f);
            if (arcGraphic != null)
            {
                arcGraphic.SetState(_energy - _cost, _energy, _trail, _time, _activity, _flash);
            }
        }

        /// <summary>
        /// 構えとチャージの状態からゲージの表示を切り替える
        /// </summary>
        /// <param name="ready">ストロボを構えているか</param>
        /// <param name="charging">左クリックでチャージしているか</param>
        public void SetVisibility(bool ready, bool charging)
        {
            _charging = ready && charging;
            _targetAlpha = !ready ? 0f : _charging ? 1f : readyAlpha;

            // 表示し始める時だけ親を有効化し、透明からフェードインさせる
            if (ready && rootObject != null && !rootObject.activeSelf)
            {
                rootObject.SetActive(true);
            }
        }

        /// <summary>
        /// 電池残量と、今発光した場合に減る量を設定する
        /// </summary>
        /// <param name="energy">電池残量（0.0 〜 1.0）</param>
        /// <param name="cost">発光で減る量（0.0 〜 1.0）</param>
        public void SetEnergy(float energy, float cost)
        {
            // 乾電池の交換で増えた時は光らせる
            float next = Mathf.Clamp01(energy);
            if (next > _energy + 0.001f) _flash = 1f;

            _energy = next;
            _cost = Mathf.Clamp(cost, 0f, _energy);
        }

        /// <summary>
        /// 予備の乾電池の本数を表示する
        /// </summary>
        /// <param name="count">所持している乾電池の本数</param>
        public void SetBatteryCount(int count)
        {
            _batteryCount = Mathf.Max(0, count);
        }
    }
}
