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

        public UnityInputService(InputActionAsset actionAsset)
        {
            _actionAsset = actionAsset;
            
            // アクションをアセット内から名前検索してマッピング
            _moveAction = _actionAsset.FindAction("Move");
            _sprintAction = _actionAsset.FindAction("Sprint");
            _crouchAction = _actionAsset.FindAction("Crouch");

            // インプット制御を有効化
            _actionAsset.Enable();
        }

        public void Dispose()
        {
            if (_actionAsset != null)
            {
                _actionAsset.Disable();
            }
        }
    }
}
