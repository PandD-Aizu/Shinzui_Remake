using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Domain.Entities.Flashlight;
using Shinzui.Domain.ValueObjects.FMOD;

namespace Shinzui.Application.UseCases.Flashlight
{
    public class FlashlightUseCase
    {
        private readonly FlashlightEntity _flashlightEntity;
        private readonly IFMODSEService _fmodSeService;

        // Presenter向けにオンオフ状態を読み取り専用で公開
        public ReadOnlyReactiveProperty<bool> IsOn => _flashlightEntity.IsOn;

        public FlashlightUseCase(
            FlashlightEntity flashlightEntity,
            IFMODSEService fmodSeService)
        {
            _flashlightEntity = flashlightEntity;
            _fmodSeService = fmodSeService;
        }

        /// <summary>
        /// 懐中電灯のオンオフをトグルする
        /// </summary>
        public void ToggleFlashlight()
        {
            _flashlightEntity.Toggle();
            _fmodSeService.PlayOneShot(FMODEventPath.FLASH_LIGHT_BUTTON_SE.Reference);
        }
    }
}
