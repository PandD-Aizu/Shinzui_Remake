using UnityEngine;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>敵自身が選んだ行動</summary>
    public enum EnemyIntentType { Idle, Wander, MoveTo, Chase, LookAround, GoBackstage, Hidden, Emerge, InspectHidingSpot, OpenHidingSpot }

    /// <summary>知覚から決定した移動先と速度</summary>
    public readonly struct EnemyIntent
    {
        public EnemyIntentType Type { get; }
        public Vector3 Target { get; }
        public float SpeedMultiplier { get; }
        public int HidingSpotId { get; }

        /// <summary>実行する行動を作成</summary>
        /// <param name="type">行動の種類</param>
        /// <param name="target">知覚またはヒント由来の目標</param>
        /// <param name="speedMultiplier">基準速度に対する倍率</param>
        public EnemyIntent(EnemyIntentType type, Vector3 target = default, float speedMultiplier = 1f, int hidingSpotId = 0)
        {
            Type = type;
            Target = target;
            SpeedMultiplier = speedMultiplier;
            HidingSpotId = hidingSpotId;
        }
    }
}
