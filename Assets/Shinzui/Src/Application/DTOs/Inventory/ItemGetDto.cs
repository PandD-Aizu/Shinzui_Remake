namespace Shinzui.Application.DTOs.Inventory
{
    public class ItemGetDto
    {
        public string ItemName { get; }
        public string IconAssetAddress { get; }

        public ItemGetDto(string itemName, string iconAssetAddress)
        {
            ItemName = itemName;
            IconAssetAddress = iconAssetAddress;
        }
    }
}
