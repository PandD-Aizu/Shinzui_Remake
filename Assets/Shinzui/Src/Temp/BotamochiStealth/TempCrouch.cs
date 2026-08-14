using UnityEngine;
using UnityEngine.InputSystem;

namespace Shinzui.Temp
{
    /// <summary>
    /// コントロールキー押下で視点（カメラ）とコライダーの高さが下がるシンプルなしゃがみコンポーネント。
    /// インスペクターで設定された参照を操作します。
    /// </summary>
    public class TempCrouch : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private CapsuleCollider playerCollider;
        [SerializeField] private Transform cameraTarget;

        [Header("Crouch Settings")]
        [SerializeField] private float standingHeight = 2.0f;
        [SerializeField] private float crouchingHeight = 1.0f;
        [SerializeField] private float heightChangeSpeed = 10.0f;

        private float _currentHeight;
        private Vector3 _standingCharacterCenter;
        private Vector3 _standingColliderCenter;
        private Vector3 _standingCameraLocalPos;

        private void Awake()
        {
            if (characterController != null)
            {
                standingHeight = characterController.height;
                _standingCharacterCenter = characterController.center;
            }
            else if (playerCollider != null)
            {
                standingHeight = playerCollider.height;
                _standingColliderCenter = playerCollider.center;
            }

            if (cameraTarget == null)
            {
                Transform foundTarget = transform.Find("PlayerHead");
                if (foundTarget == null)
                {
                    foundTarget = transform.Find("CameraTarget");
                }
                if (foundTarget != null)
                {
                    cameraTarget = foundTarget;
                }
            }

            if (cameraTarget != null)
            {
                _standingCameraLocalPos = cameraTarget.localPosition;
            }

            _currentHeight = standingHeight;
        }

        private void Update()
        {
            bool isCrouchPressed = IsCrouchKeyPressed();
            float targetHeight = isCrouchPressed ? crouchingHeight : standingHeight;

            // スムーズに高さを補間
            _currentHeight = Mathf.Lerp(_currentHeight, targetHeight, Time.deltaTime * heightChangeSpeed);

            ApplyHeight(_currentHeight);
        }

        private bool IsCrouchKeyPressed()
        {
            if (Keyboard.current != null)
            {
                return Keyboard.current.leftCtrlKey.isPressed ||
                       Keyboard.current.rightCtrlKey.isPressed ||
                       Keyboard.current.ctrlKey.isPressed ||
                       Keyboard.current.cKey.isPressed;
            }

            return false;
        }

        private void ApplyHeight(float height)
        {
            float ratio = standingHeight > 0.0f ? height / standingHeight : 1.0f;

            // CharacterControllerの高さと中心点の調整（足元を固定）
            if (characterController != null)
            {
                characterController.height = height;
                Vector3 center = _standingCharacterCenter;
                center.y -= (standingHeight - height) * 0.5f;
                characterController.center = center;
            }

            // CapsuleColliderの高さと中心点の調整
            if (playerCollider != null)
            {
                playerCollider.height = height;
                Vector3 center = _standingColliderCenter;
                center.y -= (standingHeight - height) * 0.5f;
                playerCollider.center = center;
            }

            // カメラ位置（視点）の下降・上昇
            if (cameraTarget != null)
            {
                Vector3 cameraPos = _standingCameraLocalPos;
                cameraPos.y = _standingCameraLocalPos.y * ratio;
                cameraTarget.localPosition = cameraPos;
            }
        }
    }
}
