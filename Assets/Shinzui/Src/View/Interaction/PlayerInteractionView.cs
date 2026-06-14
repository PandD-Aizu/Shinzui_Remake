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

        [Header("UI Components")]
        [SerializeField] private GameObject reticleDot;
        [SerializeField] private TMP_Text interactionPromptText;

        private InteractableComponent _currentInteractable;

        /// <summary>
        /// 現在視線が捉えているインタラクト対象。
        /// </summary>
        public InteractableComponent CurrentInteractable => _currentInteractable;

        private void Start()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (interactionPromptText != null)
            {
                interactionPromptText.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (playerCamera == null) return;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, interactRange, interactLayerMask))
            {
                // ヒットしたオブジェクト、またはその親から InteractableComponent を取得
                var interactable = hit.collider.GetComponentInParent<InteractableComponent>();
                if (interactable != null && interactable.CanInteract)
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

        private void UpdateUI(bool isTargeting, string displayName)
        {
            if (reticleDot != null)
            {
                // ターゲット時はレティクルの大きさを少し変えるなど
                reticleDot.transform.localScale = isTargeting ? new Vector3(1.5f, 1.5f, 1.5f) : Vector3.one;
            }

            if (interactionPromptText != null)
            {
                if (isTargeting)
                {
                    interactionPromptText.text = displayName;
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
