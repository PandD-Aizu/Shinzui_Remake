using UnityEngine;

namespace Shinzui.View.Interaction
{
    /// <summary>
    /// シーン上のインタラクト可能なオブジェクトにアタッチするView
    /// </summary>
    public class InteractableComponent : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private string displayName = "[E] Examine";
        [SerializeField] private string interactableId = "InteractionTest";
        [SerializeField] private bool isOneTime = false;
        [SerializeField] private bool destroyOnInteract = false;

        [Header("Display Text Settings")]
        [SerializeField, TextArea(3, 5)] private string interactMessage = "There is a creepy figurine here. It seems covered in dust.";

        private bool _canInteract = true;

        public string DisplayName => displayName;
        public virtual string InteractableId => interactableId;
        public bool IsOneTime => isOneTime;
        public bool DestroyOnInteract => destroyOnInteract;
        public string InteractMessage => interactMessage;

        public bool CanInteract
        {
            get => _canInteract;
            set => _canInteract = value;
        }

        /// <summary>
        /// インタラクト時のエフェクトを実行
        /// </summary>
        public void ExecuteInteractEffect()
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
