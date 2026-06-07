using UnityEngine;

namespace Shinzui.View
{
    public class PlayerView : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private CapsuleCollider playerCollider;
        [SerializeField] private Camera mainCamera;

        [Header("Movement Settings")]
        [SerializeField] private float rotationSpeed = 270.0f; // 度/秒 (旋回速度)

        public Vector3 CameraForward => mainCamera != null ? mainCamera.transform.forward : transform.forward;
        public Vector3 CameraRight => mainCamera != null ? mainCamera.transform.right : transform.right;
        
        public bool IsGrounded => characterController != null && characterController.isGrounded;


        /// <summary>
        /// 物理的な移動を実行します。
        /// </summary>
        public void Move(Vector3 velocity)
        {
            if (characterController != null)
            {
                characterController.Move(velocity * Time.deltaTime);
            }
        }

        /// <summary>
        /// コライダーの高さを更新します。
        /// </summary>
        public void SetHeight(float height)
        {
            if (playerCollider != null)
            {
                playerCollider.height = height;
            }
            if (characterController != null)
            {
                characterController.height = height;
            }
        }

        /// <summary>
        /// カメラのY軸回転に合わせてプレイヤーのY軸回転を調整します。
        /// </summary>
        public void AlignYRotationWithCamera()
        {
            if (mainCamera != null)
            {
                float cameraYAngle = mainCamera.transform.eulerAngles.y;
                Quaternion targetRotation = Quaternion.Euler(0.0f, cameraYAngle, 0.0f);
                
                // なめらかにカメラ方向へ回転
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, 
                    targetRotation, 
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }
}
