using Shinzui.Application.DTOs.Tunnel;

namespace Shinzui.Application.Interfaces.Tunnel
{
    /// <summary>
    /// トンネルランダム生成ユースケースのインターフェース。
    /// 対象シーン開始時に一度だけ実行され、重複生成を防止する。
    /// </summary>
    public interface IGenerateTunnelUseCase
    {
        /// <summary>
        /// 既に生成済みであるかどうかを取得する。
        /// </summary>
        bool HasGenerated { get; }

        /// <summary>
        /// 一度だけトンネル生成を実行し、生成結果のDTOを返す。
        /// 既に生成済みの場合はfalseを返し、キャッシュされたDTOを返す。
        /// </summary>
        /// <param name="request">生成パラメータDTO</param>
        /// <param name="mapDto">生成されたマップDTO</param>
        /// <returns>初回生成が成功した場合はtrue、二重実行でスキップされた場合はfalse</returns>
        bool GenerateOnce(TunnelGenerationRequestDto request, out TunnelMapDto mapDto);
    }
}
