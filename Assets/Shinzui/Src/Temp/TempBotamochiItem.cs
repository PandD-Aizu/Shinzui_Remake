using UnityEngine;
using Shinzui.View.Interaction;
using Shinzui.DI;
using Shinzui.Application.UseCases.Inventory;
using VContainer;

namespace Shinzui.Temp
{
    /// <summary>
    /// ぼたもち（アイテム）用クラス。インタラクト時にインベントリへ追加される。
    /// </summary>
    public class TempBotamochiItem : InteractableComponent
    {
        [Header("Item Config")]
        [SerializeField] private string itemId = "potion_botamochi"; // 追加するアイテムID

        public override void ExecuteInteractEffect()
        {
            base.ExecuteInteractEffect();

            var playerScope = FindFirstObjectByType<PlayerLifetimeScope>();
            if (playerScope != null && playerScope.Container != null)
            {
                var inventoryUseCase = playerScope.Container.Resolve<InventoryUseCase>();
                _ = inventoryUseCase.AddItemAsync(itemId, 1);
            }
            else
            {
                Debug.LogWarning("[TempBotamochiItem] PlayerLifetimeScope or VContainer container not found. Cannot add item.");
            }
        }
    }
}
