using System.Runtime.CompilerServices;
using UnityEngine;
using R3;
using Shinzui.Application.Interfaces;
using Shinzui.Domain.Entities;
using Shinzui.Domain.ValueObjects.Player;

namespace Shinzui.Application.UseCases
{
    public class PlayerMoveUseCase
    {
        private readonly PlayerEntity _playerEntityEntity; // プレイヤーのエンティティ
        private readonly IInputService _inputService;      // Inputサービス

        public ReadOnlyReactiveProperty<Vector3> Velocity => _playerEntityEntity.Velocity;
        public ReadOnlyReactiveProperty<float> CurrentHeight => _playerEntityEntity.CurrentHeight;
        public ReadOnlyReactiveProperty<float> StaminaRatio => _playerEntityEntity.StaminaRatio;

        public Vector2 MoveInput => _inputService.MoveInput;
        public bool SprintPressed => _inputService.SprintPressed;
        public bool CrouchPressed => _inputService.CrouchPressed;
        public bool IsRunning => _playerEntityEntity.MovementState.Value == PlayerMovementState.Running;
        
        private Ray ray;                                          //現在いるトンネルを取得するために足元に飛ばすray
        [HideInInspector] public GameObject currentTunnelStart;   //現在いるトンネルのスタート地点
        [HideInInspector] public GameObject currentTunnelEnd;     //現在いるトンネルのゴール地点

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
        /// しゃがみ時の高さ倍率と変化速度を設定
        /// </summary>
        public void SetCrouchRatio(float crouchRatio, float heightChangeRate)
        {
            if (crouchRatio <= 0.0f) return;
            float standingH = _playerEntityEntity.PlayerCrouchStatus.StandingHeight;
            _playerEntityEntity.PlayerCrouchStatus = new Shinzui.Domain.ValueObjects.Player.PlayerCrouchStatus(
                standingH,
                crouchRatio,
                heightChangeRate
            );
        }

        /// <summary>
        /// プレイヤーの移動および重力、コライダー高さの更新処理を調整
        /// </summary>
        public void Move(
            bool forceCrouch,
            bool isGrounded,
            Vector3 right,
            Vector3 forward,
            Vector3 groundNormal,
            float deltaTime)
        {
            Vector2 input = _inputService.MoveInput;
            bool isRunningRequested = _inputService.SprintPressed;
            bool isCrouchingRequested = _inputService.CrouchPressed;

            bool actualCrouching = isCrouchingRequested || forceCrouch;
            bool hasInput = _playerEntityEntity.HasMovementInput(input);

            // プレイヤー状態の移動状態を判定
            _playerEntityEntity.UpdateState(hasInput, isRunningRequested, actualCrouching);

            // スタミナの更新
            _playerEntityEntity.UpdateStamina(deltaTime);

            // 水平速度の更新
            _playerEntityEntity.CalculateVelocity(input, right, forward, groundNormal, isGrounded, deltaTime);
            
            // 重力の適用
            _playerEntityEntity.ApplyGravity(isGrounded, deltaTime);

            // コライダーの高さ更新
            _playerEntityEntity.UpdateHeight(deltaTime);
        }

        /// <summary>
        /// プレイヤーの下方向にrayを飛ばし、現在いるトンネルオブジェクトを取得する
        /// </summary>
        /// <param name="position">プレイヤーの座標</param>
        public void CheckCurrentTunnel(Vector3 position)
        { 
            float rayDistance = 5.0f;
            ray = new Ray(position, Vector3.down);
            RaycastHit hit;
            LayerMask stageMask = LayerMask.GetMask("Stage");
            if (Physics.Raycast(ray, out hit, rayDistance, stageMask))
            {
                Transform tunnelRoot = hit.collider.transform.parent;
                Transform tunnelStart = tunnelRoot != null ? tunnelRoot.Find("TunnelStart") : null;
                Transform tunnelEnd = tunnelRoot != null ? tunnelRoot.Find("TunnelEnd") : null;
                if (tunnelStart == null || tunnelEnd == null)
                {
                    currentTunnelStart = null;
                    currentTunnelEnd = null;
                    return;
                }

                currentTunnelStart = tunnelStart.gameObject;
                currentTunnelEnd = tunnelEnd.gameObject;
            }
        }
    }
}
