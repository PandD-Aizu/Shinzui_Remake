using UnityEngine;
using Shinzui.View.Interaction;

namespace Shinzui.Temp
{
    /// <summary>
    /// シーン上に配置するスタミナ無限化アイテム。インタラクト時にインベントリへ追加される。
    /// </summary>
    public class TempInfiniteStaminaItem : InteractableComponent
    {
        [Header("Item Config")]
        [SerializeField] private string itemId = "potion_stamina"; // 追加するアイテムID

        public override bool RequiresHold => true;

        // 通常の取得処理へ統一し、追加失敗時はワールド上に残す
        public override string InteractableId => $"ItemTest_{itemId}";
    }
}
