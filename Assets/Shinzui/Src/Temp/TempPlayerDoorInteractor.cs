using UnityEngine;
using UnityEngine.InputSystem;
using Shinzui.View.Interaction;

namespace DoorMove.Temp
{

    public class TempPlayerDoorInteractor : MonoBehaviour
    {
        [Header("PlayerInteractionView")]
        [SerializeField] private PlayerInteractionView interactionView;

        private void Start()
        {
            // interactionViewがない場合探す
            if (interactionView == null)
            {
                interactionView = GetComponentInParent<PlayerInteractionView>();
                if (interactionView == null)
                {
                    interactionView = GetComponent<PlayerInteractionView>();
                }
                if (interactionView == null)
                {
                    interactionView = FindFirstObjectByType<PlayerInteractionView>();
                }
            }
        }

        private void Update()
        {
            if (interactionView == null) return;

            // PlayerInteractionViewが現在捉えているインタラクト対象を取得
            InteractableComponent current = interactionView.CurrentInteractable;

            if (current != null && current is TempDoor door)
            {
                if (IsInteractKeyPressed())
                {
                    door.Interact();
                }
            }
        }

        /// <summary>
        /// Eキーによるインタラクト入力判定
        /// </summary>
        private bool IsInteractKeyPressed()
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                return true;
            }
            return false;
        }
    }
}
