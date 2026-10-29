using UnityEngine;

namespace Shinzui.Application.Interfaces
{
    public interface IEnemyEncounterFeedback
    {
        void OnPlayerFound();
        /// <summary>舞台裏の抽象位置で移動音を通知</summary>
        /// <param name="position">音源位置</param>
        void OnEnemyBackstageMove(Vector3 position);
        /// <summary>敵の出現音を通知</summary>
        /// <param name="position">出現位置</param>
        void OnEnemyEmerge(Vector3 position);
    }
}
