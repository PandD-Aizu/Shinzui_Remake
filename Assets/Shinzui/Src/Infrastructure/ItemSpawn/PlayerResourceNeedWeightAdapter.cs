using System;
using Shinzui.Application.Interfaces.ItemSpawn;
using Shinzui.Application.Interfaces.ResourceNeed;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Infrastructure.ItemSpawn
{
    /// <summary>
    /// 工程2の IPlayerResourceNeedUseCase を INeedWeightProvider に適応するアダプター。
    /// UseCaseが未注入（null）または無効な場合は安全にニュートラル重み（1.0f）を返す。
    /// </summary>
    public sealed class PlayerResourceNeedWeightAdapter : INeedWeightProvider
    {
        private readonly IPlayerResourceNeedUseCase _needUseCase;

        public PlayerResourceNeedWeightAdapter(IPlayerResourceNeedUseCase needUseCase = null)
        {
            _needUseCase = needUseCase;
        }

        public float GetNeedWeight(ResourceCategory category)
        {
            if (_needUseCase == null)
            {
                return 1.0f; // ニュートラルフォールバック
            }

            try
            {
                float weight = _needUseCase.GetNeedWeight(category);
                return Math.Max(0f, weight);
            }
            catch
            {
                return 1.0f; // 例外発生時もフォールバック
            }
        }
    }
}
