using R3;
using Shinzui.Domain.Entities.Flashlight;

namespace Shinzui.Application.UseCases.Flashlight
{
    public class FlashlightUseCase
    {
        private readonly FlashlightEntity _flashlightEntity;

        // Presenter向けにオンオフ状態を読み取り専用で公開
        public ReadOnlyReactiveProperty<bool> IsOn => _flashlightEntity.IsOn;

        public FlashlightUseCase(FlashlightEntity flashlightEntity)
        {
            _flashlightEntity = flashlightEntity;
        }

        /// <summary>
        /// 懐中電灯のオンオフをトグルする
        /// </summary>
        public void ToggleFlashlight()
        {
            _flashlightEntity.Toggle();
        }
    }
}
