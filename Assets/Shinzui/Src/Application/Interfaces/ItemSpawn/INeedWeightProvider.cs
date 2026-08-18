using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Application.Interfaces.ItemSpawn
{
    /// <summary>
    /// リソースカテゴリに応じたNeedWeight（必要度重み）を提供するインターフェース。
    /// 未注入・無効時のニュートラルウェイト（1.0f）フォールバックを許容する。
    /// </summary>
    public interface INeedWeightProvider
    {
        float GetNeedWeight(ResourceCategory category);
    }
}
