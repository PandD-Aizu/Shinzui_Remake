using Unity.Cinemachine;
using UnityEngine;

namespace Shinzui.View
{
    /// <summary>
    /// 実速度に同期した小さな歩行揺れ、加減速時の慣性、着地反動を
    /// Cinemachineの最終CameraStateへ非破壊で加える。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCameraMotionExtension : CinemachineExtension
    {
        [Header("Step Motion")]
        [SerializeField] private float walkFrequency = 1.65f;
        [SerializeField] private float runFrequency = 2.25f;
        [SerializeField] private float walkHorizontalAmplitude = 0.009f;
        [SerializeField] private float walkVerticalAmplitude = 0.014f;
        [SerializeField] private float runAmplitudeMultiplier = 1.15f;
        [SerializeField] private float motionBlendSpeed = 8.0f;

        [Header("Inertia")]
        [SerializeField] private float accelerationPitch = 0.075f;
        [SerializeField] private float accelerationRoll = 0.06f;
        [SerializeField] private float maxInertiaAngle = 0.8f;
        [SerializeField] private float inertiaSmoothTime = 0.12f;

        [Header("Landing")]
        [SerializeField] private float minimumLandingSpeed = 3.0f;
        [SerializeField] private float landingDistancePerSpeed = 0.008f;
        [SerializeField] private float maximumLandingDistance = 0.055f;
        [SerializeField] private float landingPitchPerSpeed = 0.18f;
        [SerializeField] private float maximumLandingPitch = 1.2f;
        [SerializeField] private float landingRecoveryTime = 0.18f;

        [Header("Run FOV")]
        [SerializeField] private float runFovIncrease = 1.5f;
        [SerializeField] private float fovBlendSpeed = 5.0f;

        [Header("Continuous Tremor")]
        [SerializeField, Min(0f)] private float idleTremorAngle = .08f;
        [SerializeField, Min(0f)] private float exhaustedTremorAngle = .35f;
        [SerializeField, Min(0f)] private float chaseTremorAngle = .85f;
        [SerializeField, Min(0f)] private float standingChaseTremorMultiplier = 1.6f;
        [SerializeField, Min(0f)] private float walkingChaseTremorMultiplier = .65f;
        [SerializeField, Min(0f)] private float maximumTremorDistance = .004f;
        [SerializeField, Min(1f)] private float tremorFrequency = 16f;
        [SerializeField, Min(.1f)] private float tremorBlendSpeed = 6f;

        private Transform _player;
        private Vector3 _localVelocity;
        private Vector3 _previousLocalVelocity;
        private Vector3 _localAcceleration;
        private bool _isGrounded;
        private bool _isRunning;
        private float _horizontalSpeed;
        private float _stepPhase;
        private float _motionWeight;
        private float _pitch;
        private float _pitchVelocity;
        private float _roll;
        private float _rollVelocity;
        private float _landingOffset;
        private float _landingOffsetVelocity;
        private float _landingPitch;
        private float _landingPitchVelocity;
        private float _fovOffset;
        private float _staminaRatio = 1f;
        private bool _isChased;
        private float _tremorTime;
        private float _tremorAngle;

        /// <summary>スタミナと追跡状態から常時振動の目標強度を受け取る</summary>
        /// <param name="staminaRatio">スタミナの残量割合</param>
        /// <param name="isChased">敵に追跡されているか</param>
        public void SetTensionState(float staminaRatio, bool isChased)
        {
            _staminaRatio = Mathf.Clamp01(staminaRatio);
            _isChased = isChased;
        }

        public void Configure(Transform player)
        {
            _player = player;
            ResetMotion();
        }

        public void SetMotionState(
            Vector3 worldVelocity,
            bool isGrounded,
            bool isRunning,
            float deltaTime)
        {
            if (_player == null)
            {
                return;
            }

            _localVelocity = _player.InverseTransformDirection(worldVelocity);
            _horizontalSpeed = new Vector2(_localVelocity.x, _localVelocity.z).magnitude;
            _isGrounded = isGrounded;
            _isRunning = isRunning;

            if (deltaTime > 0.0001f)
            {
                _localAcceleration = (_localVelocity - _previousLocalVelocity) / deltaTime;
            }
            _previousLocalVelocity = _localVelocity;
        }

        public void RegisterLanding(float impactSpeed)
        {
            float effectiveSpeed = Mathf.Max(0.0f, impactSpeed - minimumLandingSpeed);
            if (effectiveSpeed <= 0.0f)
            {
                return;
            }

            _landingOffset = -Mathf.Min(
                maximumLandingDistance,
                effectiveSpeed * landingDistancePerSpeed);
            _landingPitch = Mathf.Min(
                maximumLandingPitch,
                effectiveSpeed * landingPitchPerSpeed);
        }

        public void ResetMotion()
        {
            _localVelocity = Vector3.zero;
            _previousLocalVelocity = Vector3.zero;
            _localAcceleration = Vector3.zero;
            _horizontalSpeed = 0.0f;
            _stepPhase = 0.0f;
            _motionWeight = 0.0f;
            _pitch = 0.0f;
            _pitchVelocity = 0.0f;
            _roll = 0.0f;
            _rollVelocity = 0.0f;
            _landingOffset = 0.0f;
            _landingOffsetVelocity = 0.0f;
            _landingPitch = 0.0f;
            _landingPitchVelocity = 0.0f;
            _fovOffset = 0.0f;
            _tremorTime = 0f;
            _tremorAngle = idleTremorAngle;
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize || _player == null)
            {
                return;
            }

            if (deltaTime < 0.0f)
            {
                ResetMotion();
                return;
            }

            float dt = Mathf.Max(0.0f, deltaTime);
            float targetMotionWeight = _isGrounded
                ? Mathf.InverseLerp(0.15f, 2.0f, _horizontalSpeed)
                : 0.0f;
            _motionWeight = Mathf.MoveTowards(
                _motionWeight,
                targetMotionWeight,
                motionBlendSpeed * dt);

            float frequency = _isRunning ? runFrequency : walkFrequency;
            float strideSpeedScale = Mathf.Lerp(0.65f, 1.15f, Mathf.Clamp01(_horizontalSpeed / 4.0f));
            _stepPhase = Mathf.Repeat(
                _stepPhase + dt * frequency * strideSpeedScale * Mathf.PI * 2.0f,
                Mathf.PI * 2.0f);

            float amplitudeMultiplier = _isRunning ? runAmplitudeMultiplier : 1.0f;
            float horizontalBob = Mathf.Sin(_stepPhase)
                * walkHorizontalAmplitude
                * amplitudeMultiplier
                * _motionWeight;
            float verticalBob = -Mathf.Cos(_stepPhase * 2.0f)
                * walkVerticalAmplitude
                * amplitudeMultiplier
                * _motionWeight;

            float targetPitch = Mathf.Clamp(
                -_localAcceleration.z * accelerationPitch,
                -maxInertiaAngle,
                maxInertiaAngle);
            float targetRoll = Mathf.Clamp(
                -_localAcceleration.x * accelerationRoll,
                -maxInertiaAngle,
                maxInertiaAngle);
            _pitch = Mathf.SmoothDamp(
                _pitch,
                targetPitch,
                ref _pitchVelocity,
                inertiaSmoothTime,
                Mathf.Infinity,
                dt);
            _roll = Mathf.SmoothDamp(
                _roll,
                targetRoll,
                ref _rollVelocity,
                inertiaSmoothTime,
                Mathf.Infinity,
                dt);

            _landingOffset = Mathf.SmoothDamp(
                _landingOffset,
                0.0f,
                ref _landingOffsetVelocity,
                landingRecoveryTime,
                Mathf.Infinity,
                dt);
            _landingPitch = Mathf.SmoothDamp(
                _landingPitch,
                0.0f,
                ref _landingPitchVelocity,
                landingRecoveryTime,
                Mathf.Infinity,
                dt);

            Vector3 localOffset = new Vector3(horizontalBob, verticalBob + _landingOffset, 0.0f);
            state.PositionCorrection += state.GetCorrectedOrientation() * localOffset;
            state.OrientationCorrection *= Quaternion.Euler(
                _pitch + _landingPitch,
                0.0f,
                _roll);

            float targetFovOffset = _isGrounded && _isRunning ? runFovIncrease : 0.0f;
            _fovOffset = Mathf.MoveTowards(
                _fovOffset,
                targetFovOffset,
                fovBlendSpeed * dt);
            state.Lens.FieldOfView += _fovOffset;

            // 歩行の有無にかかわらず、最終視点へ小刻みな震えを重ねる
            ApplyTremor(ref state, dt);
        }

        /// <summary>通常・疲労・追跡の順に震えを強め、基準姿勢へ加算する</summary>
        /// <param name="state">このフレームのカメラ状態</param>
        /// <param name="deltaTime">カメラ更新の経過時間</param>
        private void ApplyTremor(ref CameraState state, float deltaTime)
        {
            // 追跡中は停止時に大きく震え、歩行では細かく、走行では従来の強度にする
            float movingMultiplier = _isRunning ? 1f : walkingChaseTremorMultiplier;
            float chaseMultiplier = Mathf.Lerp(standingChaseTremorMultiplier, movingMultiplier,
                Mathf.InverseLerp(.05f, .5f, _horizontalSpeed));
            float targetAngle = _isChased ? chaseTremorAngle * chaseMultiplier
                : Mathf.Lerp(idleTremorAngle, exhaustedTremorAngle, 1f - _staminaRatio);

            // 移動状態の切り替わりや追跡解除でも振幅を滑らかにつなぐ
            _tremorAngle = Mathf.Lerp(_tremorAngle, targetAngle, 1f - Mathf.Exp(-tremorBlendSpeed * deltaTime));
            _tremorTime += deltaTime;
            float phase = _tremorTime * tremorFrequency * Mathf.PI * 2f;
            var vibration = new Vector3(
                Mathf.Sin(phase) * .7f + Mathf.Sin(phase * 1.73f) * .3f,
                Mathf.Sin(phase * 1.31f),
                Mathf.Sin(phase * .83f) * .6f);

            // Cinemachineの補正値だけを変え、プレイヤーの照準やTransformへ揺れを累積させない
            float distance = maximumTremorDistance * Mathf.Clamp01(_tremorAngle / Mathf.Max(.001f, chaseTremorAngle));
            state.PositionCorrection += state.GetCorrectedOrientation()
                * new Vector3(vibration.y, vibration.x, 0f) * distance;
            state.OrientationCorrection *= Quaternion.Euler(vibration * _tremorAngle);
        }
    }
}
