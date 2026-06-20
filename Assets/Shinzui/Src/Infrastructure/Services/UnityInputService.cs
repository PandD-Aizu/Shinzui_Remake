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
        private readonly InputAction _flashlightToggleAction;
        private readonly InputAction _attackAction;

        private bool _isBlocked;

        public Vector2 MoveInput => !_isBlocked && _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        public bool SprintPressed => !_isBlocked && _sprintAction != null && _sprintAction.IsPressed();
        public bool CrouchPressed => !_isBlocked && _crouchAction != null && _crouchAction.IsPressed();

        public bool InventoryTogglePressed
        {
            get
            {
                // UI等によるTabキーの消費や動的InputActionの不具合を回避するため、直接デバイス入力をフォールバックとしてチェック
                bool keyboardTab = Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
                bool gamepadSelect = Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
                bool actionPressed = _inventoryToggleAction != null && (_inventoryToggleAction.triggered || _inventoryToggleAction.WasPressedThisFrame());
                return keyboardTab || gamepadSelect || actionPressed;
            }
        }

        public bool ItemUsePressed
        {
            get
            {
                if (_isBlocked) return false;
                bool keyboardE = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
                bool gamepadSouth = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
                bool actionPressed = _itemUseAction != null && (_itemUseAction.triggered || _itemUseAction.WasPressedThisFrame());
                return keyboardE || gamepadSouth || actionPressed;
            }
        }

        public bool FlashlightTogglePressed
        {
            get
            {
                if (_isBlocked) return false;
                bool keyboardF = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
                bool gamepadNorth = Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
                bool actionPressed = _flashlightToggleAction != null && (_flashlightToggleAction.triggered || _flashlightToggleAction.WasPressedThisFrame());
                return keyboardF || gamepadNorth || actionPressed;
            }
        }

        public bool AttackPressed
        {
            get
            {
                if (_isBlocked) return false;
                bool mouseLeft = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
                bool gamepadTrigger = Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame;
                bool actionPressed = _attackAction != null && (_attackAction.triggered || _attackAction.WasPressedThisFrame());
                return mouseLeft || gamepadTrigger || actionPressed;
            }
        }

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

            // FlashlightToggleアクション (Button) の作成とバインディング
            _flashlightToggleAction = playerMap.AddAction("FlashlightToggle", type: InputActionType.Button);
            _flashlightToggleAction.AddBinding("<Keyboard>/f");
            _flashlightToggleAction.AddBinding("<Gamepad>/buttonNorth");

            // Attackアクション (Button) の作成とバインディング
            _attackAction = playerMap.AddAction("Attack", type: InputActionType.Button);
            _attackAction.AddBinding("<Mouse>/leftButton");
            _attackAction.AddBinding("<Gamepad>/rightTrigger");

            // インプット制御を有効化
            _actionAsset.Enable();
        }

        /// <summary>
        /// 移動やカメラ視点移動の入力をブロック制御
        /// </summary>
        public void SetBlocked(bool blocked)
        {
            _isBlocked = blocked;

            // グローバルな PlayerInput コンポーネントのアクション制御
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

            // Cinemachine の視点移動入力を停止
            try
            {
                // Cinemachine v3 (Unity.Cinemachine) と Cinemachine v2 (Cinemachine) のカメラ制御・入力コンポーネントを無効化/有効化
                string[] cinemachineTypes = new string[]
                {
                    "Unity.Cinemachine.CinemachinePanTilt",
                    "Unity.Cinemachine.CinemachineInputProvider",
                    "Cinemachine.CinemachineInputProvider",
                    "Cinemachine.CinemachinePOV"
                };

                foreach (var typeName in cinemachineTypes)
                {
                    var compType = FindType(typeName);
                    if (compType != null)
                    {
                        var components = UnityEngine.Object.FindObjectsByType(compType, UnityEngine.FindObjectsSortMode.None);
                        foreach (var comp in components)
                        {
                            if (comp is MonoBehaviour behaviour)
                            {
                                behaviour.enabled = !blocked;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityInputService] Cinemachine入力コンポーネントの制御に失敗しました: {ex.Message}");
            }

            // アクションアセットの直接検索による無効化
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

            // マウスカーソルの状態制御
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

        private static Type FindType(string fullName)
        {
            var type = Type.GetType(fullName);
            if (type != null) return type;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(fullName);
                if (type != null) return type;
            }
            return null;
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
