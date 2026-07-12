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
        public void CalculateVelocity(Vector2 input, Vector3 right, Vector3 forward, float deltaTime)
        {
            var speedStatus = this.PlayerSpeedStatus;
            Vector3 currentVel = Velocity.Value;
            Vector3 currentHorizontalVel = new Vector3(currentVel.x, 0.0f, currentVel.z);
            Vector3 targetHorizontalVel = Vector3.zero;

            // スティックのデッドゾーン処理
            if (input.sqrMagnitude > 0.01f)
            {
                // 入力ベクトルを正規化
                Vector2 normalizedInput = input.normalized;
                float targetSpeed = GetTargetSpeed(true);

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
                Vector3 finalDirection = targetDirection;

                // 動き出しのガタつきを消すスムーズな旋回慣性
                if (currentHorizontalVel.sqrMagnitude > 0.001f)
                {
                    Vector3 currentDir = currentHorizontalVel.normalized;
                    float dot = Vector3.Dot(currentDir, targetDirection);

                    // 真後ろへの入力時のSlerpフリーズを防止
                    if (dot < -0.99f)
                    {
                        // 確実に直交する右ベクトルをブレンドして回転のきっかけを作る
                        Vector3 escapeAxis = Vector3.Cross(currentDir, Vector3.up).normalized;
                        if (escapeAxis.sqrMagnitude < 0.01f) escapeAxis = projRight;
                        currentDir = (currentDir + escapeAxis * 0.1f).normalized;
                    }

                    // 速度が乗るほど慣性が強く効き、静止時はクイッと曲がるようにブレンド率を調整
                    float currentSpeedRatio = Mathf.Clamp01(currentHorizontalVel.magnitude / speedStatus.MoveSpeed);
                    float actualRotationRate = Mathf.Lerp(speedStatus.AccelerationRate * 3f, speedStatus.AccelerationRate, currentSpeedRatio);

                    finalDirection = Vector3.Slerp(currentDir, targetDirection, deltaTime * actualRotationRate);
                }

                targetHorizontalVel = finalDirection.normalized * targetSpeed;
            }

            // 加速と減速のメリハリを付けて Lerp で速度合成
            bool hasInput = input.sqrMagnitude > 0.01f;
            float rate = hasInput ? speedStatus.AccelerationRate : speedStatus.DecelerationRate;

            Vector3 newHorizontalVel = Vector3.Lerp(
                currentHorizontalVel, 
                targetHorizontalVel, 
                deltaTime * rate
            );

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
                newVelocity.y = -1.0f; // 接地時は軽く地面に押し付ける
            }
            Velocity.Value = newVelocity;
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
