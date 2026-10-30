using R3;

namespace Shinzui.Domain.Entities.Flashlight
{
    public class FlashlightEntity
    {
        private readonly ReactiveProperty<bool> _isOn = new(false);
        public ReadOnlyReactiveProperty<bool> IsOn => _isOn;

        // ストロボのチャージ進行度 (0.0 〜 1.0)
        private readonly ReactiveProperty<float> _strobeCharge = new(0f);
        public ReadOnlyReactiveProperty<float> StrobeCharge => _strobeCharge;

        // ストロボの現在の追加発光強度 (0.0 〜 1.0)
        private readonly ReactiveProperty<float> _strobeIntensity = new(0f);
        public ReadOnlyReactiveProperty<float> StrobeIntensity => _strobeIntensity;

        // ストロボに使える電池残量 (0.0 〜 1.0)
        private readonly ReactiveProperty<float> _strobeEnergy = new(1f);
        public ReadOnlyReactiveProperty<float> StrobeEnergy => _strobeEnergy;

        public FlashlightEntity(bool initialOn = false)
        {
            _isOn.Value = initialOn;
        }

        /// <summary>
        /// オンオフ状態を反転する（ビジネスロジック）
        /// </summary>
        public void Toggle()
        {
            _isOn.Value = !_isOn.Value;
        }

        /// <summary>
        /// 強制的な状態設定
        /// </summary>
        public void SetState(bool on)
        {
            _isOn.Value = on;
        }

        /// <summary>
        /// ストロボのチャージ率を設定する
        /// </summary>
        public void SetStrobeCharge(float charge)
        {
            _strobeCharge.Value = UnityEngine.Mathf.Clamp01(charge);
        }

        /// <summary>
        /// ストロボの輝度倍率を設定する
        /// </summary>
        public void SetStrobeIntensity(float intensity)
        {
            _strobeIntensity.Value = UnityEngine.Mathf.Max(0f, intensity);
        }

        /// <summary>
        /// ストロボの電池残量を設定する
        /// </summary>
        /// <param name="energy">電池残量</param>
        public void SetStrobeEnergy(float energy)
        {
            _strobeEnergy.Value = UnityEngine.Mathf.Clamp01(energy);
        }

        /// <summary>
        /// 乾電池の交換でストロボの電池残量を満タンにする
        /// </summary>
        /// <returns>満タンでなく交換できたか</returns>
        public bool TryRefillStrobeEnergy()
        {
            if (_strobeEnergy.Value >= 1f) return false;

            _strobeEnergy.Value = 1f;
            return true;
        }
    }
}
