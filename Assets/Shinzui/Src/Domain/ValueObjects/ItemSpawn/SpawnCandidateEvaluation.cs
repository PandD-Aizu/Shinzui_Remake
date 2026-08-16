using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// 各アイテムの抽選重み評価結果（BaseWeight * NeedWeight）
    /// </summary>
    public sealed class SpawnCandidateEvaluation
    {
        public ItemSpawnDefinition Definition { get; }
        public float BaseWeight { get; }
        public float NeedWeight { get; }
        public float EffectiveWeight { get; }
        public ResourceCategory Category => Definition.Category;

        public SpawnCandidateEvaluation(ItemSpawnDefinition definition, float needWeight)
        {
            Definition = definition;
            BaseWeight = definition.BaseWeight;
            NeedWeight = needWeight > 0f ? needWeight : 0f;
            EffectiveWeight = BaseWeight * NeedWeight;
        }
    }
}
