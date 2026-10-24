using Shinzui.View.Interaction;

namespace Shinzui.Temp
{
    /// <summary>
    /// マップに落ちてる脱出用の鍵。拾うとインベントリに入る
    /// </summary>
    public class TempEscapeKeyItem : InteractableComponent
    {
        public override string DisplayName => TempMessages.EscapeKey.Prompt;
        // アイテムIDにしておけばInteractionUseCaseがインベントリに入れてくれる
        public override string InteractableId => TempEscapeKey.ItemId;
        public override string InteractMessage => TempMessages.EscapeKey.PickedUp;

        public override void ExecuteInteractEffect()
        {
            base.ExecuteInteractEffect();
            Destroy(gameObject);
        }
    }
}
