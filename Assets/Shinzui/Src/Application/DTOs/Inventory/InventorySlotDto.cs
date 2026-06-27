namespace Shinzui.Application.DTOs.Inventory
{
    public record InventorySlotDto(
        int SlotIndex,
        bool HasItem,
        string ItemId,
        string ItemName,
        string Description,
        string IconAssetAddress,
        int Quantity,
        int MaxStackSize,
        bool IsConsumable,
        bool IsEquipment
    );
}
