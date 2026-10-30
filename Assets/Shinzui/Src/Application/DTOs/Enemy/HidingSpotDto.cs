using UnityEngine;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>占有情報を含まないロッカーの地形情報</summary>
    public readonly struct HidingSpotDto
    {
        public readonly int Id;
        public readonly Vector3 Position, StandPosition, Forward;
        /// <summary>ロッカーの位置を保持</summary>
        /// <param name="id">識別子</param>
        /// <param name="position">本体位置</param>
        /// <param name="standPosition">敵が立つ位置</param>
        /// <param name="forward">正面方向</param>
        public HidingSpotDto(int id, Vector3 position, Vector3 standPosition, Vector3 forward)
        { Id = id; Position = position; StandPosition = standPosition; Forward = forward; }
    }

    /// <summary>目撃した入口と、検査中に感じた気配だけを脳へ渡す</summary>
    public readonly struct HidingSense
    {
        public readonly int WitnessedId, PresenceId;
        /// <summary>識別子だけの知覚を作成</summary>
        /// <param name="witnessedId">入る瞬間を目撃したロッカー、なしは0</param>
        /// <param name="presenceId">気配を感じた検査対象、なしは0</param>
        public HidingSense(int witnessedId, int presenceId) { WitnessedId = witnessedId; PresenceId = presenceId; }
    }
}
