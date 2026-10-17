using System.Reflection;
using UnityEngine;
using Shinzui.View.Interaction;

namespace Shinzui.Temp
{
    /// <summary>
    /// マップに落ちてる特殊アイテム。調べると拾えて消える
    /// 持ってると彼女との会話で選択肢が増える。1個しか持てない
    /// </summary>
    public class TempSpecialItem : InteractableComponent
    {
        private void Awake()
        {
            // 親のフィールドがprivateなのでリフレクションで。スポナーから生成したとき用
            SetPrivateFieldIfDefault("displayName", "[E] 調べる", TempMessages.SpecialItem.Prompt);
            SetPrivateFieldIfDefault("interactMessage",
                "There is a creepy figurine here. It seems covered in dust.",
                TempMessages.SpecialItem.Examine);
        }

        public override void ExecuteInteractEffect()
        {
            base.ExecuteInteractEffect();

            if (!TempSpecialItemState.TryCollect()) return;

            var messageView = FindAnyObjectByType<InteractionMessageView>();
            if (messageView != null)
            {
                messageView.ShowMessage(TempMessages.SpecialItem.PickedUp);
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// 親クラスのprivateなフィールドが既定値のままなら、newValueに差し替える
        /// </summary>
        private void SetPrivateFieldIfDefault(string fieldName, string defaultValue, string newValue)
        {
            FieldInfo field = typeof(InteractableComponent).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return;

            if (field.GetValue(this) is string current && current == defaultValue)
            {
                field.SetValue(this, newValue);
            }
        }
    }
}
