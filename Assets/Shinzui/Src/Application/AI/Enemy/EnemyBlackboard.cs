using Shinzui.Application.DTOs.Enemy;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Application.AI.Enemy
{
    /// <summary>知覚から得た記憶と固定容量の捜索点</summary>
    public sealed class EnemyBlackboard
    {
        public Vector3 LastSeenPosition { get; internal set; }
        public Vector3 LastSeenVelocity { get; internal set; }
        public float LastSeenTime { get; internal set; } = float.NegativeInfinity;
        public Vector3 InvestigateTarget { get; internal set; }
        public NoiseEvent LastHeardNoise { get; internal set; }
        public bool HasClue { get; internal set; }
        public bool IsBackstage { get; internal set; }
        public DirectorDirective Directive { get; internal set; }
        public EnemyBehaviorFlags Unlocked { get; set; }
        internal readonly Vector3[] SearchPoints = new Vector3[8];
    }
}
