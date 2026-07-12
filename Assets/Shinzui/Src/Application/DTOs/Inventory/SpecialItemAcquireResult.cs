namespace Shinzui.Application.DTOs.Inventory
{
    /// <summary>ReplacedItemIdは取得により消滅した旧アイテム。</summary>
    public record SpecialItemAcquireResult(bool Succeeded, string AcquiredItemId, string ReplacedItemId);
}
