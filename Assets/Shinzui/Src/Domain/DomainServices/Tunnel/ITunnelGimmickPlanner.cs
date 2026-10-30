using System.Collections.Generic;
using Shinzui.Domain.ValueObjects.Tunnel;

namespace Shinzui.Domain.DomainServices.Tunnel
{
    /// <summary>レイアウトからギミックの配置を計画する</summary>
    public interface ITunnelGimmickPlanner
    {
        /// <summary>同一シードで再現可能な配置を作る</summary>
        /// <param name="layout">生成済みレイアウト</param>
        /// <param name="config">配置設定</param>
        /// <param name="goalTunnelId">ゴールのトンネル番号</param>
        /// <param name="goalEndSign">ゴール側の端の符号</param>
        /// <returns>制約を満たす配置スロット</returns>
        IReadOnlyList<TunnelGimmickPlacement> Plan(TunnelLayoutResult layout, TunnelGenerationConfig config,
            int? goalTunnelId, int goalEndSign);
    }
}
