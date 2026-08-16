using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Application.Interfaces.ResourceNeed
{
    public interface IItemCategoryClassifier
    {
        ResourceCategory? ClassifyItem(string itemId);
        int GetItemUnitCount(string itemId);
    }
}
