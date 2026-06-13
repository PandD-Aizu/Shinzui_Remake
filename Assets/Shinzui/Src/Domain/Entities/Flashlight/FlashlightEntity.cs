using R3;

namespace Shinzui.Domain.Entities.Flashlight
{
    public class FlashlightEntity
    {
        private readonly ReactiveProperty<bool> _isOn = new(false);
        public ReadOnlyReactiveProperty<bool> IsOn => _isOn;

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
    }
}
