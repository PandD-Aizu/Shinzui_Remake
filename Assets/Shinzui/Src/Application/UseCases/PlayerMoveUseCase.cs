using UnityEngine;
using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Domain.Entities;

namespace Shinzui.Application.UseCases
{
    public class PlayerMoveUseCase
    {
        private readonly PlayerEntity _playerEntityEntity; // プレイヤーのエンティティ
        private readonly IInputService _inputService;      // Inputサービス

        public ReadOnlyReactiveProperty<Vector3> Velocity => _playerEntityEntity.Velocity;
        public ReadOnlyReactiveProperty<float> CurrentHeight => _playerEntityEntity.CurrentHeight;

        public Vector2 MoveInput => _inputService.MoveInput;
        public bool SprintPressed => _inputService.SprintPressed;
        public bool CrouchPressed => _inputService.CrouchPressed;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="playerEntityEntity"></param>
        /// <param name="inputService"></param>
        public PlayerMoveUseCase(PlayerEntity playerEntityEntity, IInputService inputService)
        {
            _playerEntityEntity = playerEntityEntity;
            _inputService = inputService;
        }

        /// <summary>
        /// プレイヤーの移動および重力、コライダー高さの更新処理を調整
        /// </summary>
        public void Move(
            bool forceCrouch,
            bool isGrounded,
            Vector3 right,
            Vector3 forward,
            float deltaTime)
        {
            Vector2 input = _inputService.MoveInput;
            bool isRunningRequested = _inputService.SprintPressed;
            bool isCrouchingRequested = _inputService.CrouchPressed;

            bool actualCrouching = isCrouchingRequested || forceCrouch;
            bool hasInput = input != Vector2.zero;

            // プレイヤー状態の移動状態を判定
            _playerEntityEntity.UpdateState(hasInput, isRunningRequested, actualCrouching);

            // 水平速度の更新
            _playerEntityEntity.CalculateVelocity(input, right, forward, deltaTime);
            
            // 重力の適用
            _playerEntityEntity.ApplyGravity(isGrounded, deltaTime);

            // コライダーの高さ更新
            _playerEntityEntity.UpdateHeight(deltaTime);
        }
    }
}