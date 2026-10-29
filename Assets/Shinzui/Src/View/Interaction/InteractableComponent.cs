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
        public float PickupDuration => pickupDuration;
        public Transform PickupGrip => pickupGrip;

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

        /// <summary>明示した掴み位置、または視点に近いコライダー表面を返す</summary>
        /// <param name="origin">手を伸ばす側の視点</param>
        /// <returns>指先が触れる位置</returns>
        public Vector3 GetPickupContact(Vector3 origin)
        {
            Vector3 center = PickupPosition;
            if (pickupGrip != null || _pickupCollider == null) return center;
            if (_pickupCollider is MeshCollider mesh && !mesh.convex) return mesh.bounds.ClosestPoint(origin);
            return _pickupCollider.ClosestPoint(origin);
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
    }
}
