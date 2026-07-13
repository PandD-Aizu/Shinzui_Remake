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
        }
    }
}
