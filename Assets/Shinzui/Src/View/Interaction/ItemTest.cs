using UnityEngine;

namespace Shinzui.View.Interaction
{
    /// <summary>
    /// Eキー押下でインベントリにアイテムを追加するテストインタラクトオブジェクト
    /// </summary>
    public class ItemTest : InteractableComponent
    {
        public override string InteractableId => $"ItemTest_{base.InteractableId}";
    }
}
