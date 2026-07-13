using R3;
using Shinzui.Domain.ValueObjects.Inventory;
using Shinzui.Domain.ValueObjects.Player;
using UnityEngine;

namespace Shinzui.Domain.Entities
{
    public class PlayerEntity
    {
        private SpecialItemModifiers _specialItemModifiers = SpecialItemModifiers.None;
        public PlayerSpeedStatus PlayerSpeedStatus { get; set; }
        public PlayerCrouchStatus PlayerCrouchStatus { get; set; }
        public PlayerStamina PlayerStamina { get; set; }
        
        public ReactiveProperty<PlayerMovementState> MovementState { get; } = new (PlayerMovementState.Idle);
        public ReactiveProperty<Vector3> Velocity { get; } = new (Vector3.zero);
        public Vector3 CurrentVelocity => Velocity.Value;
        public ReactiveProperty<float> CurrentHeight { get; }
        public ReactiveProperty<float> CurrentStamina { get; }
        public ReadOnlyReactiveProperty<float> StaminaRatio { get; }
        public ReactiveProperty<bool> IsExhausted { get; } = new (false);
        public SpecialItemModifiers SpecialItemModifiers => _specialItemModifiers;

        public PlayerEntity(
            PlayerSpeedStatus playerSpeedStatus,
            PlayerCrouchStatus playerCrouchStatus,
            PlayerStamina playerStamina)
        {
            PlayerSpeedStatus = playerSpeedStatus;
            PlayerCrouchStatus = playerCrouchStatus;
            PlayerStamina = playerStamina;
            CurrentHeight = new ReactiveProperty<float>(playerCrouchStatus.StandingHeight);
            CurrentStamina = new ReactiveProperty<float>(playerStamina.CurrentStamina);
            StaminaRatio = CurrentStamina.Select(x => x / playerStamina.MaxStamina).ToReadOnlyReactiveProperty();
        }

        /// <summary>
        /// 入力の有無、走る要求、しゃがむ要求に基づいて移動状態を更新する
        /// </summary>
        public void UpdateState(bool hasInput, bool isRunningRequested, bool isCrouchingRequested)
        {
            if (isCrouchingRequested)
                MovementState.Value = PlayerMovementState.Crouching;
            else if (isRunningRequested && hasInput && !IsExhausted.Value)
                MovementState.Value = PlayerMovementState.Running;
            else if (hasInput)
                MovementState.Value = PlayerMovementState.Walking;
            else
                MovementState.Value = PlayerMovementState.Idle;
        }

        /// <summary>
        /// 特殊アイテム由来の常時効果を設定する
        /// </summary>
        public void SetSpecialItemModifiers(SpecialItemModifiers modifiers)
        {
            _specialItemModifiers = modifiers ?? SpecialItemModifiers.None;
        }

        /// <summary>
        /// 移動状態に基づいてスタミナを更新する
        /// </summary>
        public void UpdateStamina(float deltaTime)
        {
            float staminaChange;
            if (MovementState.Value == PlayerMovementState.Running)
            {
                staminaChange = -PlayerStamina.StaminaDecreaseRate * deltaTime;
            }
            else
            {
                staminaChange = PlayerStamina.StaminaIncreaseRate
                    * _specialItemModifiers.StaminaRecoveryMultiplier
                    * deltaTime;
            }

            float newStamina = Mathf.Clamp(
                PlayerStamina.CurrentStamina + staminaChange,
                PlayerStamina.MinStamina,
                PlayerStamina.MaxStamina
            );

            PlayerStamina = PlayerStamina with { CurrentStamina = newStamina };
            CurrentStamina.Value = newStamina;

            if (newStamina <= PlayerStamina.MinStamina)
            {
                IsExhausted.Value = true;
            }
            else if (IsExhausted.Value && newStamina >= PlayerStamina.MaxStamina * 0.2f)
            {
                IsExhausted.Value = false;
            }
        }

        /// <summary>
        /// 現在の移動状態と入力の有無に基づいて、目標速度を計算して返す
        /// </summary>
        public float GetTargetSpeed(bool hasInput)
        {
            if (!hasInput) return 0.0f;

            return MovementState.Value switch
            {
                PlayerMovementState.Idle => 0.0f,
                PlayerMovementState.Walking => PlayerSpeedStatus.MoveSpeed * _specialItemModifiers.MoveSpeedMultiplier,
                PlayerMovementState.Running => PlayerSpeedStatus.MoveSpeed * PlayerSpeedStatus.RunSpeedMultiplier * _specialItemModifiers.MoveSpeedMultiplier,
                PlayerMovementState.Crouching => PlayerSpeedStatus.MoveSpeed * PlayerSpeedStatus.CrouchSpeedMultiplier * _specialItemModifiers.MoveSpeedMultiplier,
                _ => 0.0f
            };
        }

        /// <summary>
        /// 移動入力と方向ベクトルから、水平方向の速度を計算して更新する
        /// </summary>
        public void CalculateVelocity(
            Vector2 input,
            Vector3 right,
            Vector3 forward,
            Vector3 groundNormal,
            bool isGrounded,
            float deltaTime)
        {
            var speedStatus = this.PlayerSpeedStatus;
            Vector3 currentVel = Velocity.Value;
            Vector3 currentHorizontalVel = new Vector3(currentVel.x, 0.0f, currentVel.z);
            Vector3 targetHorizontalVel = Vector3.zero;
            bool hasInput = HasMovementInput(input);

            // スティックのデッドゾーン処理
            if (hasInput)
            {
                // デッドゾーン以降の入力強度を保持し、アナログ入力の微速移動を可能にする
                float inputMagnitude = Mathf.InverseLerp(
                    speedStatus.InputDeadZone,
                    1.0f,
                    Mathf.Clamp01(input.magnitude));
                Vector2 normalizedInput = input.normalized;
                float targetSpeed = GetTargetSpeed(true) * inputMagnitude;

                // 方向ペナルティ倍率の計算
                float penalty = 1.0f;
                if (normalizedInput.y < 0.0f)
                {
                    // 後退時ペナルティ：真後ろ（-1）に近づくほど強くかかる
                    float backwardWeight = Mathf.Abs(normalizedInput.y);
                    penalty = Mathf.Lerp(speedStatus.StrafeSpeedMultiplier, speedStatus.BackwardSpeedMultiplier, backwardWeight);
                }
                else
                {
                    // 横移動ペナルティ：真横に近づくほど強くかかる
                    float strafeWeight = Mathf.Abs(normalizedInput.x);
                    penalty = Mathf.Lerp(1.0f, speedStatus.StrafeSpeedMultiplier, strafeWeight);
                }
                targetSpeed *= penalty;

                // カメラのピッチ角の影響を排除して平面投影
                Vector3 projForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
                Vector3 projRight = Vector3.ProjectOnPlane(right, Vector3.up).normalized;

                // 入力に応じた絶対的な目標方向
                Vector3 targetDirection = (projRight * normalizedInput.x + projForward * normalizedInput.y).normalized;
                if (isGrounded && groundNormal.sqrMagnitude > 0.5f)
                {
                    targetDirection = Vector3.ProjectOnPlane(targetDirection, groundNormal).normalized;
                }

                targetHorizontalVel = targetDirection * targetSpeed;
            }

            float rate = hasInput ? speedStatus.AccelerationRate : speedStatus.DecelerationRate;

            // 大きく反対方向へ入力された場合は、方向を瞬時に反転させず一度制動する
            if (hasInput && currentHorizontalVel.sqrMagnitude > 0.01f && targetHorizontalVel.sqrMagnitude > 0.01f)
            {
                float directionDot = Vector3.Dot(currentHorizontalVel.normalized, targetHorizontalVel.normalized);
                if (directionDot < speedStatus.ReversalDotThreshold && currentHorizontalVel.magnitude > 0.1f)
                {
                    targetHorizontalVel = Vector3.zero;
                    rate = speedStatus.DirectionChangeDecelerationRate;
                }
            }

            if (!isGrounded)
            {
                rate *= speedStatus.AirControlMultiplier;
            }

            Vector3 newHorizontalVel = Vector3.MoveTowards(
                currentHorizontalVel,
                targetHorizontalVel,
                rate * deltaTime);

            Vector3 newVelocity = currentVel;
            newVelocity.x = newHorizontalVel.x;
            newVelocity.z = newHorizontalVel.z;

            Velocity.Value = newVelocity;
        }

        /// <summary>
        /// 重力を適用
        /// </summary>
        public void ApplyGravity(bool isGrounded, float deltaTime)
        {
            Vector3 newVelocity = Velocity.Value;
            if (!isGrounded)
            {
                newVelocity.y += Physics.gravity.y * deltaTime;
            }
            else
            {
                newVelocity.y = PlayerSpeedStatus.GroundStickVelocity;
            }
            Velocity.Value = newVelocity;
        }

        public bool HasMovementInput(Vector2 input)
        {
            float deadZone = Mathf.Max(0.0f, PlayerSpeedStatus.InputDeadZone);
            return input.sqrMagnitude > deadZone * deadZone;
        }

        /// <summary>
        /// コライダーの高さを計算して更新
        /// </summary>
        public void UpdateHeight(float deltaTime)
        {
            float targetHeight = (MovementState.Value == PlayerMovementState.Crouching) 
                ? PlayerCrouchStatus.CrouchingHeight 
                : PlayerCrouchStatus.StandingHeight;

            CurrentHeight.Value = Mathf.Lerp(CurrentHeight.Value, targetHeight, deltaTime * PlayerCrouchStatus.HeightChangeRate);
        }
    }
}
