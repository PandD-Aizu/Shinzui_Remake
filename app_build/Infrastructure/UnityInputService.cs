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
        private readonly InputAction _inventoryToggleAction;
        private readonly InputAction _itemUseAction;

        private bool _isBlocked;

        public Vector2 MoveInput => !_isBlocked && _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        public bool SprintPressed => !_isBlocked && _sprintAction != null && _sprintAction.IsPressed();
        public bool CrouchPressed => !_isBlocked && _crouchAction != null && _crouchAction.IsPressed();
        public bool InventoryTogglePressed => _inventoryToggleAction != null && _inventoryToggleAction.WasPressedThisFrame(); // 開閉入力はブロック中でも受け付ける
        public bool ItemUsePressed => !_isBlocked && _itemUseAction != null && _itemUseAction.WasPressedThisFrame();

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

            // InventoryToggleアクション (Button) の作成とバインディング
            _inventoryToggleAction = playerMap.AddAction("InventoryToggle", type: InputActionType.Button);
            _inventoryToggleAction.AddBinding("<Keyboard>/tab");
            _inventoryToggleAction.AddBinding("<Gamepad>/select");

            // ItemUseアクション (Button) の作成とバインディング
            _itemUseAction = playerMap.AddAction("ItemUse", type: InputActionType.Button);
            _itemUseAction.AddBinding("<Keyboard>/e");
            _itemUseAction.AddBinding("<Gamepad>/buttonSouth");

            // インプット制御を有効化
            _actionAsset.Enable();
        }

        /// <summary>
        /// 移動やカメラ視点移動の入力をブロック制御します。
        /// </summary>
        public void SetBlocked(bool blocked)
        {
            _isBlocked = blocked;

            // 1. グローバルな PlayerInput コンポーネントのアクション制御
            try
            {
                var playerInputs = UnityEngine.Object.FindObjectsByType<UnityEngine.InputSystem.PlayerInput>(UnityEngine.FindObjectsSortMode.None);
                foreach (var input in playerInputs)
                {
                    if (blocked)
                    {
                        input.DeactivateInput();
                    }
                    else
                    {
                        input.ActivateInput();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityInputService] PlayerInputの有効無効切り替えに失敗しました: {ex.Message}");
            }

            // 2. Cinemachine の視点移動を停止（リフレクションを用いてアセンブリ参照なしに制御）
            try
            {
                // CinemachineBrain を探して無効化/有効化する
                var brainType = Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine") 
                                ?? Type.GetType("Cinemachine.CinemachineBrain, Cinemachine");
                if (brainType != null)
                {
                    var brains = UnityEngine.Object.FindObjectsByType(brainType, UnityEngine.FindObjectsSortMode.None);
                    foreach (var brain in brains)
                    {
                        if (brain is MonoBehaviour behaviour)
                        {
                            behaviour.enabled = !blocked;
                        }
                    }
                }

                // CinemachineInputProvider も無効化/有効化する
                var providerType = Type.GetType("Unity.Cinemachine.CinemachineInputProvider, Unity.Cinemachine") 
                                   ?? Type.GetType("Cinemachine.CinemachineInputProvider, Cinemachine");
                if (providerType != null)
                {
                    var providers = UnityEngine.Object.FindObjectsByType(providerType, UnityEngine.FindObjectsSortMode.None);
                    foreach (var provider in providers)
                    {
                        if (provider is MonoBehaviour behaviour)
                        {
                            behaviour.enabled = !blocked;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityInputService] Cinemachineコンポーネントの制御に失敗しました: {ex.Message}");
            }

            // 3. アクションアセットの直接検索による無効化
            try
            {
                var playerMap = InputSystem.actions?.FindActionMap("Player");
                if (playerMap != null)
                {
                    if (blocked) playerMap.Disable();
                    else playerMap.Enable();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityInputService] ActionMap 'Player' の切り替えに失敗しました: {ex.Message}");
            }

            // 4. マウスカーソルの状態制御
            if (blocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
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
