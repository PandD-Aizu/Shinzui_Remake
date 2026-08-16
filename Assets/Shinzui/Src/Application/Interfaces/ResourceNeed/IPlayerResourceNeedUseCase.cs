using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Application.Interfaces.ResourceNeed
{
    public interface IPlayerResourceNeedUseCase
    {
        PlayerResourceSnapshot CaptureSnapshot();
        PlayerResourceNeedEvaluationResult Evaluate();
        PlayerResourceNeedEvaluationResult Evaluate(PlayerResourceSnapshot snapshot);
        float GetNeedWeight(ResourceCategory category);
        float GetNeedWeight(ResourceCategory category, PlayerResourceSnapshot snapshot);
        ResourceRatioBreakdown GetRatios();
        ResourceRatioBreakdown GetRatios(PlayerResourceSnapshot snapshot);
    }
}
