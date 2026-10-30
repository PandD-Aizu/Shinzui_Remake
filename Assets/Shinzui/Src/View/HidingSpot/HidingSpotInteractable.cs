using Shinzui.View.Interaction;
using UnityEngine;

namespace Shinzui.View
{
    /// <summary>通常のインタラクトからロッカーの出入りを要求</summary>
    public sealed class HidingSpotInteractable : InteractableComponent
    {
        public override string DisplayName => GetComponent<HidingLockerView>().Occupied ? "[E] ロッカーから出る" : "[E] ロッカーに隠れる";
        public override string InteractableId => "hiding_locker";
        public override string InteractMessage => "";
        public override bool RequiresHold => false;
        /// <summary>専用Presenterへ操作を渡す</summary>
        public override void ExecuteInteractEffect() => GetComponent<HidingLockerView>().RequestInteraction();
    }
}
