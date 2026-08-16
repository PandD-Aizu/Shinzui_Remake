using System;

namespace Shinzui.Domain.ValueObjects.ResourceNeed
{
    public record NeedWeightCategoryConfig
    {
        public ResourceCategory Category { get; init; }
        public ICurveEvaluator Curve { get; init; }
        public float MinWeight { get; init; }
        public float MaxWeight { get; init; }
        public float DefaultWeight { get; init; }

        public NeedWeightCategoryConfig(
            ResourceCategory category,
            ICurveEvaluator curve = null,
            float minWeight = 0.5f,
            float maxWeight = 2.0f,
            float defaultWeight = 1.0f)
        {
            Category = category;
            Curve = curve ?? SampledPiecewiseCurveEvaluator.DefaultInverse();
            MinWeight = Math.Max(0f, minWeight);
            MaxWeight = Math.Max(MinWeight, maxWeight);
            DefaultWeight = Math.Clamp(defaultWeight, MinWeight, MaxWeight);
        }
    }
}
