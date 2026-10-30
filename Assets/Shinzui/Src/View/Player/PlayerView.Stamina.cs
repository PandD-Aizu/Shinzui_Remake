using UnityEngine;

namespace Shinzui.View
{
    public partial class PlayerView
    {
        [Header("Stamina Animation")]
        [SerializeField, Min(0f)] private float staminaWaveAmplitude = 2.2f;
        [SerializeField, Min(0.01f)] private float staminaTrailSmoothTime = 0.22f;
        [SerializeField, Range(0f, 1f)] private float staminaWarningThreshold = 0.2f;

        private StaminaWaveGraphic _staminaWave;
        private UnityEngine.UI.Image _staminaBackground;
        private Color _staminaBackgroundColor;
        private bool _staminaFillEnabled;
        private float _staminaTrail;
        private float _staminaTrailVelocity;
        private float _staminaPreviousRatio = -1f;
        private float _staminaActivity;
        private float _staminaRecovery;
        private float _staminaFullFlash;
        private float _staminaAnimationTime;

        /// <summary>既存のFill領域に波形描画を重ねてスタミナ演出を初期化</summary>
        private void InitializeStaminaAnimation()
        {
            if (staminaFillImage == null || staminaSlider.fillRect == null || _staminaWave != null) return;

            // 色変更用のImageとSliderの参照を保ったまま描画だけを波形に置き換える
            _staminaFillEnabled = staminaFillImage.enabled;
            staminaFillImage.enabled = false;
            var waveObject = new GameObject("Stamina Wave", typeof(RectTransform), typeof(StaminaWaveGraphic));
            waveObject.layer = staminaFillImage.gameObject.layer;
            _staminaWave = waveObject.GetComponent<StaminaWaveGraphic>();
            var rect = _staminaWave.rectTransform;
            rect.SetParent(staminaSlider.fillRect.parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _staminaWave.raycastTarget = false;

            // 暗い下地で残量のない範囲と黄色の波形を区別する
            var background = staminaSlider.transform.Find("Background");
            if (background != null && background.TryGetComponent(out _staminaBackground))
            {
                _staminaBackgroundColor = _staminaBackground.color;
                _staminaBackground.color = new Color(0.06f, 0.055f, 0.035f, 0.65f);
            }

            _staminaCanvasGroup.blocksRaycasts = false;
            _staminaCanvasGroup.interactable = false;
        }

        /// <summary>実残量を維持しながら消費残像と回復時の光量を補間</summary>
        /// <param name="deltaTime">ゲーム内の経過秒数</param>
        private void UpdateStaminaAnimation(float deltaTime)
        {
            if (_staminaWave == null || deltaTime <= 0f) return;

            float ratio = staminaSlider.normalizedValue;
            if (_staminaPreviousRatio < 0f)
            {
                _staminaPreviousRatio = ratio;
                _staminaTrail = ratio;
            }

            // 連続消費中も残像が追いつくよう、待機タイマーを毎フレーム延長しない
            float change = ratio - _staminaPreviousRatio;
            float blend = 1f - Mathf.Exp(-10f * deltaTime);
            _staminaActivity = Mathf.Lerp(_staminaActivity, change < -0.00001f ? 1f : 0f, blend);
            _staminaRecovery = Mathf.Lerp(_staminaRecovery, change > 0.00001f ? 1f : 0f, blend);
            if (ratio >= 0.999f && _staminaPreviousRatio < 0.999f) _staminaFullFlash = 1f;
            _staminaFullFlash = Mathf.MoveTowards(_staminaFullFlash, 0f, deltaTime * 2f);
            if (ratio >= _staminaTrail)
            {
                _staminaTrail = ratio;
                _staminaTrailVelocity = 0f;
            }
            else
            {
                _staminaTrail = Mathf.Clamp(Mathf.SmoothDamp(_staminaTrail, ratio,
                    ref _staminaTrailVelocity, staminaTrailSmoothTime, Mathf.Infinity, deltaTime), ratio, 1f);
            }
            _staminaPreviousRatio = ratio;
            _staminaAnimationTime += deltaTime;

            // Imageの色を毎フレーム参照し、スタミナ剤の色演出も波形へ反映する
            _staminaWave.SetState(ratio, _staminaTrail, _staminaAnimationTime,
                staminaWaveAmplitude, _staminaActivity, _staminaRecovery,
                _staminaFullFlash, staminaWarningThreshold, staminaFillImage.color, staminaSlider.direction);
        }

        /// <summary>プレイヤーと別階層に追加した描画を破棄し元の画像状態へ戻す</summary>
        private void OnDestroy()
        {
            if (_staminaWave == null) return;

            Destroy(_staminaWave.gameObject);
            if (staminaFillImage != null) staminaFillImage.enabled = _staminaFillEnabled;
            if (_staminaBackground != null) _staminaBackground.color = _staminaBackgroundColor;
        }
    }
}
