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
        [SerializeField] private Transform cameraTarget;

        [Header("Movement Settings")]
        [SerializeField] private float rotationSpeed = 220.0f;
        [SerializeField] private float movingRotationSmoothTime = 0.12f;
        [SerializeField] private float idleRotationSmoothTime = 0.2f;
        [SerializeField] private float bodyFreeLookAngle = 8.0f;
        [SerializeField] private float groundProbeDistance = 0.25f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Stamina UI")] 
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private Image staminaFillImage;
        [SerializeField] private Gradient staminaColorGradient = CreateDefaultStaminaGradient();

        private CanvasGroup _staminaCanvasGroup;
        private float _lastStaminaValue = -1.0f;
        private float _staminaChangeTimer = 0.0f;
        private float _rotationVelocity;
        private float _standingCharacterHeight;
        private Vector3 _standingCharacterCenter;
        private float _standingColliderHeight;
        private Vector3 _standingColliderCenter;
        private Vector3 _standingCameraLocalPosition;
        private PlayerCameraMotionExtension _cameraMotion;
        private const float StaminaFadeDelay = 2.0f;
        private const float StaminaFadeInSpeed = 10.0f;
        private const float StaminaFadeOutSpeed = 1.0f;

        public Vector3 CameraForward => mainCamera != null ? mainCamera.transform.forward : transform.forward;
        public Vector3 CameraRight => mainCamera != null ? mainCamera.transform.right : transform.right;
        public Vector3 CameraPosition => mainCamera != null ? mainCamera.transform.position : transform.position;
        public float CameraNearClipPlane => mainCamera != null ? mainCamera.nearClipPlane : 0.3f;
        public Vector3 CameraNearPosition => CameraPosition + CameraForward * CameraNearClipPlane;
        
        public bool IsGrounded => TryGetGroundNormal(out _);
        public bool IsControllerGrounded => characterController != null && characterController.isGrounded;
        public Vector3 GroundNormal => TryGetGroundNormal(out Vector3 normal) ? normal : Vector3.up;

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
            if (characterController != null)
            {
                _standingCharacterHeight = characterController.height;
                _standingCharacterCenter = characterController.center;
            }

            if (playerCollider != null)
            {
                _standingColliderHeight = playerCollider.height;
                _standingColliderCenter = playerCollider.center;
            }

            if (_standingCharacterHeight <= 0.0f)
            {
                _standingCharacterHeight = _standingColliderHeight > 0.0f ? _standingColliderHeight : 2.0f;
            }

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
                _standingCameraLocalPosition = cameraTarget.localPosition;
                if (mainCamera != null && mainCamera.transform.parent == transform)
                {
                    mainCamera.transform.SetParent(cameraTarget, false);
                    mainCamera.transform.localPosition = Vector3.zero;
                    mainCamera.transform.localRotation = Quaternion.identity;
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

        private void Start()
        {
            EnsureCameraMotionExtension();
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
            _rotationVelocity = 0.0f;
            _cameraMotion?.ResetMotion();
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
                playerCollider.center = GetBottomAnchoredCenter(
                    _standingColliderCenter,
                    _standingColliderHeight,
                    height);
            }
            if (characterController != null)
            {
                characterController.height = height;
                characterController.center = GetBottomAnchoredCenter(
                    _standingCharacterCenter,
                    _standingCharacterHeight,
                    height);
            }

            float standingH = _standingCharacterHeight > 0.0f ? _standingCharacterHeight : 2.0f;
            if (cameraTarget != null)
            {
                float ratio = height / standingH;
                Vector3 targetPos = _standingCameraLocalPosition;
                targetPos.y = _standingCameraLocalPosition.y * ratio;
                cameraTarget.localPosition = targetPos;
            }
        }

        public bool CanStand()
        {
            if (characterController == null || _standingCharacterHeight <= 0.0f)
            {
                return true;
            }

            if (characterController.height >= _standingCharacterHeight - 0.01f)
            {
                return true;
            }

            Vector3 desiredCenter = GetBottomAnchoredCenter(
                _standingCharacterCenter,
                _standingCharacterHeight,
                _standingCharacterHeight);
            float radius = Mathf.Max(0.01f, characterController.radius - characterController.skinWidth);
            float halfSegment = Mathf.Max(0.0f, _standingCharacterHeight * 0.5f - radius);
            Vector3 worldCenter = transform.TransformPoint(desiredCenter);
            Vector3 bottom = worldCenter - transform.up * halfSegment;
            Vector3 top = worldCenter + transform.up * halfSegment;

            int playerLayer = LayerMask.NameToLayer("Player");
            int collisionMask = playerLayer >= 0 ? ~(1 << playerLayer) : Physics.AllLayers;
            return !Physics.CheckCapsule(bottom, top, radius, collisionMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// カメラのY軸回転に合わせてプレイヤーのY軸回転を調整
        /// </summary>
        public void AlignYRotationWithCamera(Vector3 velocity)
        {
            if (mainCamera != null)
            {
                float cameraYAngle = mainCamera.transform.eulerAngles.y;
                float currentYAngle = transform.eulerAngles.y;
                float angleDelta = Mathf.DeltaAngle(currentYAngle, cameraYAngle);
                if (Mathf.Abs(angleDelta) <= bodyFreeLookAngle)
                {
                    return;
                }

                float targetYAngle = cameraYAngle - Mathf.Sign(angleDelta) * bodyFreeLookAngle;
                float horizontalSpeed = new Vector3(velocity.x, 0.0f, velocity.z).magnitude;
                float smoothTime = horizontalSpeed > 0.1f
                    ? movingRotationSmoothTime
                    : idleRotationSmoothTime;
                float smoothedYAngle = Mathf.SmoothDampAngle(
                    currentYAngle,
                    targetYAngle,
                    ref _rotationVelocity,
                    smoothTime,
                    rotationSpeed,
                    Time.deltaTime);
                transform.rotation = Quaternion.Euler(0.0f, smoothedYAngle, 0.0f);
            }
        }

        public void UpdateCameraMotion(
            Vector3 velocity,
            bool isGrounded,
            bool isRunning,
            float landingSpeed)
        {
            if (_cameraMotion == null)
            {
                EnsureCameraMotionExtension();
            }

            if (_cameraMotion == null)
            {
                return;
            }

            _cameraMotion.SetMotionState(velocity, isGrounded, isRunning, Time.deltaTime);
            if (landingSpeed > 0.0f)
            {
                _cameraMotion.RegisterLanding(landingSpeed);
            }
        }

        private void EnsureCameraMotionExtension()
        {
            if (_cameraMotion != null)
            {
                return;
            }

            CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (CinemachineCamera camera in cameras)
            {
                Transform follow = camera.Follow;
                if (follow != null && follow != transform && !follow.IsChildOf(transform))
                {
                    continue;
                }

                if ((follow == null || follow == transform) && cameraTarget != null)
                {
                    camera.Follow = cameraTarget;
                }

                _cameraMotion = camera.GetComponent<PlayerCameraMotionExtension>();
                if (_cameraMotion == null)
                {
                    _cameraMotion = camera.gameObject.AddComponent<PlayerCameraMotionExtension>();
                }
                _cameraMotion.Configure(transform);
                break;
            }
        }

        private bool TryGetGroundNormal(out Vector3 normal)
        {
            normal = Vector3.up;
            if (characterController == null || !characterController.enabled)
            {
                return false;
            }

            Vector3 worldCenter = transform.TransformPoint(characterController.center);
            float radius = Mathf.Max(0.01f, characterController.radius - characterController.skinWidth);
            float bottomOffset = Mathf.Max(0.0f, characterController.height * 0.5f - radius);
            Vector3 origin = worldCenter - transform.up * bottomOffset + transform.up * 0.05f;

            int playerLayer = LayerMask.NameToLayer("Player");
            int selfMask = playerLayer >= 0 ? ~(1 << playerLayer) : Physics.AllLayers;
            int collisionMask = groundLayers.value & selfMask;
            if (Physics.SphereCast(
                    origin,
                    radius,
                    -transform.up,
                    out RaycastHit hit,
                    groundProbeDistance + 0.05f,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
                return true;
            }

            return characterController.isGrounded;
        }

        private static Vector3 GetBottomAnchoredCenter(
            Vector3 standingCenter,
            float standingHeight,
            float currentHeight)
        {
            Vector3 center = standingCenter;
            center.y -= (standingHeight - currentHeight) * 0.5f;
            return center;
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
