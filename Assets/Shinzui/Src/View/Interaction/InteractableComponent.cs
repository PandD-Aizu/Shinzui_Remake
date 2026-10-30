using UnityEngine;

namespace Shinzui.View.Interaction
{
    /// <summary>
    /// シーン上のインタラクト可能なオブジェクトにアタッチするView
    /// </summary>
    public class InteractableComponent : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private string displayName = "[E] 調べる";
        [SerializeField] private string interactableId = "InteractionTest";
        [SerializeField] private bool isOneTime = false;
        [SerializeField] private bool destroyOnInteract = false;

        [Header("Display Text Settings")]
        [SerializeField, TextArea(3, 5)] private string interactMessage = "There is a creepy figurine here. It seems covered in dust.";

        private bool _canInteract = true;

        [Header("Pickup")]
        [SerializeField] private bool holdToPickup;
        [SerializeField, Min(.05f)] private float pickupDuration = 1f;
        [SerializeField] private Transform pickupGrip;
        private Collider _pickupCollider;

        public virtual bool RequiresHold => holdToPickup;
        /// <summary>長押し不要で右手を伸ばして押す動作を再生するか</summary>
        public virtual bool PlaysPressAnimation => false;
        public float PickupDuration => pickupDuration;
        public Transform PickupGrip => pickupGrip;

        /// <summary>実行時に決まる掴み位置を設定</summary>
        /// <param name="grip">手を合わせる位置と向き</param>
        protected void SetPickupGrip(Transform grip)
        {
            pickupGrip = grip;
        }

        /// <summary>取得アイテムの判定を操作検出専用にする</summary>
        private void Awake()
        {
            if (!RequiresHold) return;

            // 子モデルを含めて物理衝突をなくし、足場になることを防ぐ
            foreach (Collider itemCollider in GetComponentsInChildren<Collider>(true))
            {
                if (itemCollider.GetComponentInParent<InteractableComponent>() != this) continue;
                if (itemCollider is MeshCollider mesh) mesh.convex = true;
                itemCollider.isTrigger = true;
            }
        }

        /// <summary>明示した掴み位置、または対象のコライダー中心</summary>
        public Vector3 PickupPosition
        {
            get
            {
                if (pickupGrip != null) return pickupGrip.position;
                if (_pickupCollider == null) _pickupCollider = GetComponentInChildren<Collider>();
                return _pickupCollider != null ? _pickupCollider.bounds.center : transform.position;
            }
        }

        /// <summary>明示した掴み位置、または掌で覆うコライダー上面を返す</summary>
        /// <param name="origin">手を伸ばす側の視点</param>
        /// <returns>掌を合わせる位置</returns>
        public Vector3 GetPickupContact(Vector3 origin)
        {
            Vector3 center = PickupPosition;
            if (pickupGrip != null || _pickupCollider == null) return center;

            // 側面をなでず、対象の上から掌を被せる
            Vector3 above = center + Vector3.up * (_pickupCollider.bounds.extents.y + .1f);
            Vector3 surface = _pickupCollider is MeshCollider mesh && !mesh.convex
                ? mesh.bounds.ClosestPoint(above) : _pickupCollider.ClosestPoint(above);
            return surface + Vector3.up * .025f;
        }

        /// <summary>掌を下に向け、指を対象の奥へ伸ばす手首姿勢を返す</summary>
        /// <param name="forward">プレイヤーの前方</param>
        /// <returns>掴み位置の回転</returns>
        public Quaternion GetPickupRotation(Vector3 forward)
        {
            if (pickupGrip != null) return pickupGrip.rotation;

            // 見下ろす角度を手首へ伝えず、掌を上面に沿わせる
            Vector3 direction = Vector3.ProjectOnPlane(forward, Vector3.up);
            return Quaternion.LookRotation(direction.sqrMagnitude > .0001f ? direction : Vector3.forward, Vector3.up);
        }

        public virtual string DisplayName => displayName;
        public virtual string InteractableId => interactableId;
        public bool IsOneTime => isOneTime;
        public bool DestroyOnInteract => destroyOnInteract;
        public virtual string InteractMessage => interactMessage;

        public bool CanInteract
        {
            get => _canInteract;
            set => _canInteract = value;
        }

        /// <summary>
        /// インタラクト時のエフェクトを実行
        /// </summary>
        public virtual void ExecuteInteractEffect()
        {
            if (isOneTime)
            {
                _canInteract = false;
            }

            if (destroyOnInteract)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 操作したプレイヤーを受け取ってインタラクト時のエフェクトを実行
        /// </summary>
        /// <param name="actor">操作したプレイヤー</param>
        public virtual void ExecuteInteractEffect(PlayerView actor)
        {
            ExecuteInteractEffect();
        }
    }
}
