using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Domain.Entities.Flashlight;
using Shinzui.Domain.ValueObjects.FMOD;

namespace Shinzui.Application.UseCases.Flashlight
{
    public readonly struct StrobeReleaseResult
    {
        public bool Fired { get; }
        public float Charge { get; }

        public StrobeReleaseResult(bool fired, float charge)
        {
            Fired = fired;
            Charge = charge;
        }
    }

    public class FlashlightUseCase
    {
        private readonly FlashlightEntity _flashlightEntity;
        private readonly IFMODSEService _fmodSeService;

        private const float MaxChargeTime = 0.8f;      // 0.8秒でフルチャージ
        private const float DecaySpeed = 1.8f;         // ストロボ減衰速度
        private const float MinChargeThreshold = 0.15f; // 15%以上のチャージで発光可能

        private float _currentPushTime = 0f;

        // Presenter向けに各種状態を公開
        public ReadOnlyReactiveProperty<bool> IsOn => _flashlightEntity.IsOn;
        public ReadOnlyReactiveProperty<float> StrobeCharge => _flashlightEntity.StrobeCharge;
        public ReadOnlyReactiveProperty<float> StrobeIntensity => _flashlightEntity.StrobeIntensity;

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

        /// <summary>
        /// ストロボのチャージ処理
        /// </summary>
        public void Charge(float deltaTime)
        {
            _currentPushTime += deltaTime;
            float charge = UnityEngine.Mathf.Clamp01(_currentPushTime / MaxChargeTime);
            _flashlightEntity.SetStrobeCharge(charge);
        }

        /// <summary>
        /// ストロボ発光処理（リリース時）
        /// </summary>
        public StrobeReleaseResult Release()
        {
            float charge = 0f;
            bool fired = false;

            if (_currentPushTime > 0f)
            {
                charge = _flashlightEntity.StrobeCharge.CurrentValue;
                if (charge >= MinChargeThreshold)
                {
                    // チャージ量に応じた強さでストロボ発光
                    _flashlightEntity.SetStrobeIntensity(charge);
                    fired = true;
                    
                    // 発光時の演出SE
                    _fmodSeService.PlayOneShot(FMODEventPath.FLASH_LIGHT_BUTTON_SE.Reference);
                }

                // チャージリセット
                _currentPushTime = 0f;
                _flashlightEntity.SetStrobeCharge(0f);
            }

            return new StrobeReleaseResult(fired, charge);
        }

        /// <summary>
        /// ストロボ減衰などの毎フレーム更新
        /// </summary>
        public void Update(float deltaTime)
        {
            float intensity = _flashlightEntity.StrobeIntensity.CurrentValue;
            if (intensity > 0f)
            {
                float nextIntensity = intensity - DecaySpeed * deltaTime;
                if (nextIntensity < 0f) nextIntensity = 0f;
                _flashlightEntity.SetStrobeIntensity(nextIntensity);
            }
        }
    }
}
