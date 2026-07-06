using UnityEngine;
using Shinzui.View.Interaction;
using Shinzui.DI;
using Shinzui.Application.UseCases.Inventory;
using VContainer;

namespace Shinzui.Temp
{
    /// <summary>
    /// シーン上に配置するスタミナ無限化アイテム。インタラクト時にインベントリへ追加される。
    /// </summary>
    public class TempInfiniteStaminaItem : InteractableComponent
    {
        [Header("Item Config")]
        [SerializeField] private string itemId = "potion_stamina"; // 追加するアイテムID

        public override void ExecuteInteractEffect()
        {
            base.ExecuteInteractEffect();

            var playerScope = FindFirstObjectByType<PlayerLifetimeScope>();
            if (playerScope != null && playerScope.Container != null)
            {
                var inventoryUseCase = playerScope.Container.Resolve<InventoryUseCase>();
                _ = inventoryUseCase.AddItemAsync(itemId, 1);
            }
            // エラー処理
            else
            {
                Debug.LogWarning("[TempInfiniteStaminaItem] PlayerLifetimeScope or VContainer container not found. Cannot add item.");
            }
        }
    }
}
