using UnityEngine;
using TMPro;

namespace Shinzui.View.Interaction
{
    /// <summary>
    /// プレイヤーのカメラからRaycastを飛ばし、正面にあるインタラクト可能なオブジェクトを検知・UI表示するViewコンポーネント。
    /// </summary>
    public class PlayerInteractionView : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float interactRange = 2.0f;
        [SerializeField] private LayerMask interactLayerMask = ~0;

        [Header("Interaction Markers")]
        [SerializeField, Min(0f)] private float markerRange = 5f;
        [SerializeField, Min(1f)] private float markerDiameter = 14f;
        [SerializeField, Min(1f)] private float interactableMarkerDiameter = 24f;

        private InteractionMarkerDisplay _markerDisplay;
        private PlayerBodyView _body;
        private Transform _owner;
        private float _pickupProgress;
        private bool _holdProgressVisible;
        private bool _pickupSucceeded;
        private HoldInteractionProgressView _holdProgressView;
        private InteractableComponent _holdTarget;
        [Header("Hold Progress")]
        [SerializeField, Min(32f)] private float holdProgressDiameter = 72f;
        public bool IsPickupActive { get; private set; }

        [Header("UI Components")]
        [SerializeField] private GameObject reticleDot;
        [SerializeField] private TMP_Text interactionPromptText;

        private InteractableComponent _currentInteractable;
        private Collider[] _tutorialColliders = new Collider[32];

        /// <summary>近くに壁で遮られていない未取得アイテムがあるか調べる</summary>
        /// <param name="origin">接近距離の基準位置</param>
        /// <param name="range">検出する距離</param>
        /// <returns>取得可能なアイテムが範囲内にあるか</returns>
        public bool HasNearbyPickup(Vector3 origin, float range)
        {
            if (!isActiveAndEnabled || playerCamera == null) return false;

            // 視線やしゃがみ状態に依存せず、周囲のアイテムを検出する
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(origin, range, _tutorialColliders,
                interactLayerMask, QueryTriggerInteraction.Collide)) == _tutorialColliders.Length)
                System.Array.Resize(ref _tutorialColliders, _tutorialColliders.Length * 2);

            for (int i = 0; i < count; i++)
            {
                var target = _tutorialColliders[i].GetComponentInParent<InteractableComponent>();
                if (target == null || !target.isActiveAndEnabled || !target.CanInteract || !target.RequiresHold
                    || target.gameObject.scene != gameObject.scene) continue;
                if ((target.PickupPosition - origin).sqrMagnitude > range * range) continue;
                if (InteractionVisibility.IsVisible(playerCamera.transform.position, target.PickupPosition, target, _owner))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 現在視線が捉えているインタラクト対象。
        /// </summary>
        public InteractableComponent CurrentInteractable => _currentInteractable;

        /// <summary>
        /// カメラと対象マーカーの表示を初期化
        /// </summary>
        private void Start()
        {
            var player = GetComponentInParent<PlayerView>();
            _owner = player != null ? player.transform : transform;
            _body = _owner.GetComponentInChildren<PlayerBodyView>();
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (interactionPromptText != null)
            {
                interactionPromptText.gameObject.SetActive(false);
            }

            _markerDisplay = new InteractionMarkerDisplay(transform);
            _holdProgressView = HoldInteractionProgressView.Create(transform,
                interactionPromptText != null ? interactionPromptText.font : TMP_Settings.defaultFontAsset,
                Vector2.zero, holdProgressDiameter);
        }

        /// <summary>
        /// 正面の操作可能な対象を検出
        /// </summary>
        private void Update()
        {
            _holdProgressView?.UpdateInputDevice();
            // カメラがStart後に生成された場合や切り替わった場合にも再取得
            if (playerCamera == null || !playerCamera.isActiveAndEnabled) playerCamera = Camera.main;
            if (playerCamera == null) return;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, interactRange, interactLayerMask, QueryTriggerInteraction.Collide))
            {
                // ヒットしたオブジェクト、またはその親から InteractableComponent を取得
                var interactable = hit.collider.GetComponentInParent<InteractableComponent>();
                if (interactable != null && interactable.isActiveAndEnabled && interactable.CanInteract)
                {
                    _currentInteractable = interactable;
                    UpdateUI(true, interactable.DisplayName);
                    return;
                }
            }

            // 何も捉えていない場合
            _currentInteractable = null;
            UpdateUI(false, string.Empty);
        }

        /// <summary>
        /// カメラ更新後の位置で近傍の対象マーカーを表示
        /// </summary>
        private void LateUpdate()
        {
            if (playerCamera == null || !playerCamera.isActiveAndEnabled) playerCamera = Camera.main;
            _markerDisplay?.Refresh(playerCamera, _currentInteractable,
                Mathf.Max(markerRange, interactRange), interactLayerMask,
                markerDiameter, Mathf.Max(markerDiameter, interactableMarkerDiameter));

            // 対象を外しても操作継続中のゲージを維持し、戻す動きは進捗へ逆流させない
            bool available = !IsPickupActive && _currentInteractable != null
                && _currentInteractable.RequiresHold && CanReachPickup(_currentInteractable);
            if (_holdProgressView == null) return;
            if (Time.timeScale <= 0f)
            {
                _holdProgressView.Hide();
                return;
            }

            // 取得中は開始時の対象へ追従し、完了後は最後の位置で成立表示を残す
            bool completed = _holdProgressView.IsShowingCompletion;
            var target = _holdProgressVisible || completed ? _holdTarget : available ? _currentInteractable : null;
            bool replaced = _markerDisplay.TryReplaceWithHold(target, out Vector2 screenPosition);
            if (replaced) _holdProgressView.SetScreenPosition(screenPosition);
            _holdProgressView.Refresh(replaced, _holdProgressVisible ? _pickupProgress : 0f);
        }

        /// <summary>保持中の操作対象を距離・視野・遮蔽から検証</summary>
        /// <param name="target">開始時に確定した対象</param>
        /// <returns>取得を継続できるか</returns>
        public bool CanContinuePickup(InteractableComponent target)
        {
            if (!isActiveAndEnabled || playerCamera == null || target == null || !target.isActiveAndEnabled || !target.CanInteract)
                return false;
            Vector3 offset = target.PickupPosition - playerCamera.transform.position;
            return offset.magnitude <= interactRange && Vector3.Angle(playerCamera.transform.forward, offset) < 30f
                && CanReachPickup(target)
                && InteractionVisibility.IsVisible(playerCamera.transform.position, target.PickupPosition, target, _owner);
        }

        /// <summary>右手の届く範囲に対象があるか判定</summary>
        /// <param name="target">操作対象</param>
        /// <returns>取得を開始できるか</returns>
        public bool CanReachPickup(InteractableComponent target)
        {
            if (_body == null && _owner != null) _body = _owner.GetComponentInChildren<PlayerBodyView>();

            // 乾電池とぼたもちは腕の到達距離に余裕を持たせ、床付近でも取得しやすくする
            float reachTolerance = target is FlashlightBattery ? .5f : 0f;
            if (target != null && target.InteractableId == "ItemTest_potion_botamochi") reachTolerance = .2f;

            // 全アイテムの取得距離を20cm広げる
            reachTolerance += .2f;

            return target != null && _body != null && playerCamera != null
                && _body.CanReachPickup(target.GetPickupContact(playerCamera.transform.position),
                    target.GetPickupRotation(playerCamera.transform.forward), reachTolerance);
        }

        /// <summary>取得中の案内と他のアイテム操作の排他状態を更新</summary>
        /// <param name="active">取得操作中か</param>
        /// <param name="progress">長押しの進捗</param>
        /// <param name="showHold">長押しまたは演出待ちを表示するか</param>
        /// <param name="succeeded">取得が成立したか</param>
        /// <param name="target">開始時に確定した操作対象</param>
        public void SetPickupProgress(bool active, float progress, bool showHold = false, bool succeeded = false,
            InteractableComponent target = null)
        {
            if (active && !IsPickupActive) _holdProgressView?.Hide();
            IsPickupActive = active;
            _pickupProgress = progress;
            _holdProgressVisible = showHold;
            if (succeeded && !_pickupSucceeded) _holdProgressView?.ShowCompleted();
            _pickupSucceeded = succeeded;
            if (target != null) _holdTarget = target;
            if (!active && !succeeded) _holdTarget = null;
        }

        /// <summary>
        /// 無効化時に対象と表示を解除
        /// </summary>
        private void OnDisable()
        {
            _currentInteractable = null;
            _markerDisplay?.Hide();
            _holdProgressView?.Hide();
            UpdateUI(false, string.Empty);
        }

        /// <summary>
        /// 生成したマーカー表示を解放
        /// </summary>
        private void OnDestroy()
        {
            _markerDisplay?.Dispose();
        }

        /// <summary>
        /// 操作対象に応じて照準と案内を更新
        /// </summary>
        /// <param name="isTargeting">操作対象を捉えているか</param>
        /// <param name="displayName">操作案内</param>
        private void UpdateUI(bool isTargeting, string displayName)
        {
            if (reticleDot != null)
            {
                // ターゲット時はレティクルの大きさを少し変えるなど
                reticleDot.SetActive(!isTargeting);
            }

            if (interactionPromptText != null)
            {
                if (isTargeting)
                {
                    interactionPromptText.text = displayName;
                    if (_currentInteractable != null && _currentInteractable.RequiresHold)
                        interactionPromptText.text = IsPickupActive
                            ? "拾う"
                            : CanReachPickup(_currentInteractable) ? "長押しで拾う" : "近づく／しゃがんで拾う";
                    interactionPromptText.gameObject.SetActive(true);
                }
                else
                {
                    interactionPromptText.gameObject.SetActive(false);
                }
            }
        }
    }
}
