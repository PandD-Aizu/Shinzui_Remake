namespace Shinzui.Application.DTOs.Inventory
{
    public record SpecialItemSlotDto(
        bool HasItem,
        string ItemId,
        string ItemName,
        string Description,
        string IconAssetAddress,
        bool PreventDeathOnce);
}
