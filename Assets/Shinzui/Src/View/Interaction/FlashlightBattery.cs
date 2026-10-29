using UnityEngine;

namespace Shinzui.View.Interaction
{
    /// <summary>
    /// Eキー押下でインベントリにアイテムを追加するテストインタラクトオブジェクト
    /// </summary>
    public class FlashlightBattery : InteractableComponent
    {
        public override bool RequiresHold => true;
        public override string InteractableId => "FlashlightBattery";
        
    }
}
