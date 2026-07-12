using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

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

        [Header("Stamina UI")] 
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private Image staminaFillImage;
        [SerializeField] private Gradient staminaColorGradient = CreateDefaultStaminaGradient();

        private CanvasGroup _staminaCanvasGroup;
        private float _lastStaminaValue = -1.0f;
        private float _staminaChangeTimer = 0.0f;
        private const float StaminaFadeDelay = 2.0f;
        private const float StaminaFadeInSpeed = 10.0f;
        private const float StaminaFadeOutSpeed = 1.0f;

        public Vector3 CameraForward => mainCamera != null ? mainCamera.transform.forward : transform.forward;
        public Vector3 CameraRight => mainCamera != null ? mainCamera.transform.right : transform.right;
        public Vector3 CameraPosition => mainCamera != null ? mainCamera.transform.position : transform.position;
        public float CameraNearClipPlane => mainCamera != null ? mainCamera.nearClipPlane : 0.3f;
        public Vector3 CameraNearPosition => CameraPosition + CameraForward * CameraNearClipPlane;
        
        public bool IsGrounded => characterController != null && characterController.isGrounded;

        /// <summary>
        /// プレイヤーのコライダーを取得します。
        /// </summary>
        public Collider PlayerCollider => playerCollider;

        /// <summary>
        /// 演出用のスタミナFill画像を取得します。
        /// </summary>
        public Image StaminaFillImage => staminaFillImage;

        /// <summary>
        /// メインカメラを取得します。
        /// </summary>
        public Camera MainCamera => mainCamera;

        /// <summary>
        /// プレイヤーの現在の移動速度を取得します。
        /// </summary>
        public Vector3 CurrentVelocity => characterController != null ? characterController.velocity : Vector3.zero;

        private void Awake()
        {
            if (staminaFillImage == null && staminaSlider != null && staminaSlider.fillRect != null)
            {
                staminaFillImage = staminaSlider.fillRect.GetComponent<Image>();
            }

            if (staminaSlider != null)
            {
                _staminaCanvasGroup = staminaSlider.GetComponent<CanvasGroup>();
                if (_staminaCanvasGroup == null)
                {
                    _staminaCanvasGroup = staminaSlider.gameObject.AddComponent<CanvasGroup>();
                }
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer != -1)
            {
                SetLayerRecursive(gameObject, playerLayer);
            }
        }

        private void SetLayerRecursive(GameObject obj, int newLayer)
        {
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursive(child.gameObject, newLayer);
            }
        }

        private void Update()
        {
            if (staminaSlider == null || _staminaCanvasGroup == null) return;

            float currentValue = staminaSlider.value;

            // 値に変動があったかチェック
            if (!Mathf.Approximately(currentValue, _lastStaminaValue))
            {
                if (_lastStaminaValue >= 0f)
                {
                    _staminaChangeTimer = StaminaFadeDelay;
                }
                _lastStaminaValue = currentValue;
            }

            // タイマー稼働時はフェードイン、ゼロになったらフェードアウト
            if (_staminaChangeTimer > 0.0f)
            {
                _staminaChangeTimer -= Time.deltaTime;
                _staminaCanvasGroup.alpha = Mathf.MoveTowards(_staminaCanvasGroup.alpha, 1.0f, StaminaFadeInSpeed * Time.deltaTime);
            }
            else
            {
                _staminaCanvasGroup.alpha = Mathf.MoveTowards(_staminaCanvasGroup.alpha, 0.0f, StaminaFadeOutSpeed * Time.deltaTime);
            }
        }

        private static Gradient CreateDefaultStaminaGradient()
        {
            var gradient = new Gradient();
            gradient.colorKeys = new GradientColorKey[]
            {
                new GradientColorKey(Color.red, 0.0f),
                new GradientColorKey(Color.white, 1.0f)
            };
            gradient.alphaKeys = new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(1.0f, 1.0f)
            };
            return gradient;
        }

        /// <summary>
        /// プレイヤーの位置をワープ
        /// CharacterControllerを一時的に無効化して位置を変更
        /// </summary>
        public void Warp(Vector3 offset)
        {
            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position += offset;
                characterController.enabled = true;
            }
            else
            {
                transform.position += offset;
            }

            // カメラ自体も即座にワープさせることで、同フレーム内での他スクリプトによるカメラ座標参照のズレを防ぐ
            if (mainCamera != null)
            {
                mainCamera.transform.position += offset;
            }

            // Cinemachineの追従遅れによる一瞬の空の映り込みを防ぐため、ワープを通知する
            CinemachineCore.OnTargetObjectWarped(transform, offset);
        }

        /// <summary>
        /// 物理的な移動を実行
        /// </summary>
        public void Move(Vector3 velocity)
        {
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(velocity * Time.deltaTime);
            }
        }

        /// <summary>
        /// コライダーの高さを更新
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
        /// カメラのY軸回転に合わせてプレイヤーのY軸回転を調整
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

        /// <summary>
        /// スタミナスライダーの値を更新
        /// </summary>
        /// <param name="value">スタミナの割合 (0.0f - 1.0f)</param>
        public void ChangeStaminaSlider(float value)
        {
            if (staminaSlider != null)
            {
                staminaSlider.value = value;
            }

            if (staminaFillImage != null && staminaColorGradient != null)
            {
                staminaFillImage.color = staminaColorGradient.Evaluate(value);
            }
        }
    }
}