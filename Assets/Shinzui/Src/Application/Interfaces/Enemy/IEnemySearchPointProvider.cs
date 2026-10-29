using System;
using UnityEngine;

namespace Shinzui.Application.Interfaces.Enemy
{
    /// <summary>地形から到達可能な捜索点と出入り地点を取得</summary>
    public interface IEnemySearchPointProvider
    {
        /// <summary>捜索候補を取得</summary>
        /// <param name="center">中心位置</param>
        /// <param name="radius">範囲</param>
        /// <param name="includeSideRooms">小部屋を優先するか</param>
        /// <param name="buffer">格納先</param>
        /// <returns>格納した点数</returns>
        int GetSearchPoints(Vector3 center, float radius, bool includeSideRooms, Span<Vector3> buffer);
        /// <summary>接続された退出候補を取得</summary>
        /// <param name="from">敵の位置</param>
        /// <param name="buffer">格納先</param>
        /// <returns>格納した点数</returns>
        int GetExitPoints(Vector3 from, Span<Vector3> buffer);
        /// <summary>プレイヤーと接続された不可視の再出現地点を探す</summary>
        /// <param name="playerPosition">プレイヤーの位置</param>
        /// <param name="minDistance">最小距離</param>
        /// <param name="maxDistance">最大距離</param>
        /// <param name="includeUpperFloor">上階を含めるか</param>
        /// <param name="isVisibleToPlayer">カメラからの視認判定</param>
        /// <param name="point">見つかった地点</param>
        /// <returns>候補が見つかったか</returns>
        bool TryFindEmergePoint(Vector3 playerPosition, float minDistance, float maxDistance,
            bool includeUpperFloor, Func<Vector3, bool> isVisibleToPlayer, out Vector3 point);
    }
}
