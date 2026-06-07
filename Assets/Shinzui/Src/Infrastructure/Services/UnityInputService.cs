using System;
using Shinzui.Application.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shinzui.Infrastructure.Services
{
    public class UnityInputService : IInputService, IDisposable
    {
        private readonly InputActionAsset _actionAsset;
        private readonly InputAction _moveAction;
        private readonly InputAction _sprintAction;
        private readonly InputAction _crouchAction;

        public Vector2 MoveInput => _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        public bool SprintPressed => _sprintAction != null && _sprintAction.IsPressed();
        public bool CrouchPressed => _crouchAction != null && _crouchAction.IsPressed();

        public UnityInputService()
        {
            // 動的にInputActionAssetを作成
            _actionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            
            // アクションマップを作成してアセットに追加
            var playerMap = new InputActionMap("Player");
            _actionAsset.AddActionMap(playerMap);
            
            // Moveアクション (Vector2) の作成とバインディング
            _moveAction = playerMap.AddAction("Move", type: InputActionType.Value, expectedControlLayout: "Vector2");
            // キーボード (WASD)
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            // コントローラー (左スティック)
            _moveAction.AddBinding("<Gamepad>/leftStick");

            // Sprintアクション (Button) の作成とバインディング
            _sprintAction = playerMap.AddAction("Sprint", type: InputActionType.Button);
            // キーボード (左Shift)
            _sprintAction.AddBinding("<Keyboard>/leftShift");
            // コントローラー (L2/左トリガー & 左スティック押し込み)
            _sprintAction.AddBinding("<Gamepad>/leftTrigger");
            _sprintAction.AddBinding("<Gamepad>/leftStickPress");

            // Crouchアクション (Button) の作成とバインディング
            _crouchAction = playerMap.AddAction("Crouch", type: InputActionType.Button);
            // キーボード (C)
            _crouchAction.AddBinding("<Keyboard>/c");
            // コントローラー (B/○ボタン)
            _crouchAction.AddBinding("<Gamepad>/buttonEast");

            // インプット制御を有効化
            _actionAsset.Enable();
        }

        public void Dispose()
        {
            if (_actionAsset != null)
            {
                _actionAsset.Disable();
                UnityEngine.Object.Destroy(_actionAsset);
            }
        }
    }
}
