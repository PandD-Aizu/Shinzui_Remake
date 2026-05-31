using R3;
using Shinzui.Domain.ValueObjects.Player;
using UnityEngine;

namespace Shinzui.Domain.Entities
{
    public class PlayerEntity
    {
        public PlayerSpeedStatus PlayerSpeedStatus { get; set; }
        public PlayerCrouchStatus PlayerCrouchStatus { get; set; }
        
        public ReactiveProperty<PlayerMovementState> MovementState { get; } = new (PlayerMovementState.Idle);
        public ReactiveProperty<Vector3> Velocity { get; } = new (Vector3.zero);
        public ReactiveProperty<float> CurrentHeight { get; }

        public PlayerEntity(
            PlayerSpeedStatus playerSpeedStatus,
            PlayerCrouchStatus playerCrouchStatus)
        {
            PlayerSpeedStatus = playerSpeedStatus;
            PlayerCrouchStatus = playerCrouchStatus;
            CurrentHeight = new ReactiveProperty<float>(playerCrouchStatus.StandingHeight);
        }

        /// <summary>
        /// 入力の有無、走る要求、しゃがむ要求に基づいて移動状態を更新する
        /// </summary>
        /// <param name="hasInput">入力があるかどうか</param>
        /// <param name="isRunningRequested">true: 走っている</param>
        /// <param name="isCrouchingRequested">true: しゃがんでいる</param>
        public void UpdateState(bool hasInput, bool isRunningRequested, bool isCrouchingRequested)
        {
            if (isCrouchingRequested)
                MovementState.Value = PlayerMovementState.Crouching;
            else if (isRunningRequested && hasInput)
                MovementState.Value = PlayerMovementState.Running;
            else if (hasInput)
                MovementState.Value = PlayerMovementState.Walking;
            else
                MovementState.Value = PlayerMovementState.Idle;
        }

        /// <summary>
        /// 現在の移動状態と入力の有無に基づいて、目標速度を計算して返す
        /// </summary>
        /// <param name="hasInput">true: 入力がある</param>
        /// <returns>目標速度(スカラー)</returns>
        public float GetTargetSpeed(bool hasInput)
        {
            if (!hasInput) return 0.0f;

            return MovementState.Value switch
            {
                PlayerMovementState.Idle => 0.0f,
                PlayerMovementState.Walking => PlayerSpeedStatus.MoveSpeed,
                PlayerMovementState.Running => PlayerSpeedStatus.MoveSpeed * PlayerSpeedStatus.RunSpeedMultiplier,
                PlayerMovementState.Crouching => PlayerSpeedStatus.MoveSpeed * PlayerSpeedStatus.CrouchSpeedMultiplier,
                _ => 0.0f
            };
        }

        /// <summary>
        /// 移動入力と方向ベクトルから、水平方向の速度を計算して更新します。
        /// </summary>
        public void CalculateVelocity(Vector2 input, Vector3 right, Vector3 forward, float deltaTime)
        {
            bool hasInput = input != Vector2.zero;
            float targetSpeed = GetTargetSpeed(hasInput);
            Vector3 currentVel = Velocity.Value;
            
            // 水平方向の現在の速さを取得
            float currentHorizontalSpeed = new Vector3(currentVel.x, 0.0f, currentVel.z).magnitude;
            
            // 目標速度に向けて補間
            float newHorizontalSpeed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, deltaTime * PlayerSpeedStatus.SpeedChangeRate);

            Vector3 newVelocity = currentVel;
            if (input != Vector2.zero)
            {
                Vector3 inputDirection = new Vector3(input.x, 0.0f, input.y).normalized;
                Vector3 targetDirection = (right * inputDirection.x + forward * inputDirection.z).normalized;
                
                Vector3 horizontalMovement = targetDirection * newHorizontalSpeed;
                newVelocity.x = horizontalMovement.x;
                newVelocity.z = horizontalMovement.z;
            }
            else
            {
                // 入力がない場合は現在の進行方向を維持したまま減速する
                Vector3 horizontalDir = new Vector3(currentVel.x, 0.0f, currentVel.z).normalized;
                Vector3 horizontalMovement = horizontalDir * newHorizontalSpeed;
                newVelocity.x = horizontalMovement.x;
                newVelocity.z = horizontalMovement.z;
            }

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