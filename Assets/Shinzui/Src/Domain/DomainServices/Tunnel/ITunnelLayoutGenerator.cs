using Shinzui.Domain.ValueObjects.Tunnel;

namespace Shinzui.Domain.DomainServices.Tunnel
{
    /// <summary>
    /// 設定情報とシード値に基づき、連結トンネル・通常通路・小部屋・ワープ通路の
    /// 幾何配置を決定論的に計算するドメインサービスのインターフェース。
    /// 完全なPOCOであり、UnityEngineに依存しない。
    /// </summary>
    public interface ITunnelLayoutGenerator
    {
        TunnelLayoutResult GenerateLayout(TunnelGenerationConfig config);
    }
}
