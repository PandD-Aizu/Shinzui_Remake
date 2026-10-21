using System;

namespace Shinzui.Application.UseCases.Enemy
{
    public enum SpiderMovementState { Idle, Patrol, Chase, Search, Hold }

    /// <summary>Movement decisions only. All perception and navigation are supplied by the engine adapter.</summary>
    public sealed class SpiderMovementUseCase
    {
        private float unseenSeconds;
        private float patrolWait;
        private bool hasSeenTarget;
        public SpiderMovementState State { get; private set; }
        public int PatrolIndex { get; private set; }

        public SpiderMovementState Tick(float deltaTime, bool targetExists, bool targetVisible,
            float targetDistance, float stopDistance, float memorySeconds, int waypointCount,
            bool arrived, bool routeBlocked)
        {
            float dt = Math.Max(0f, deltaTime);
            if (targetExists && targetVisible)
            {
                hasSeenTarget = true;
                unseenSeconds = 0f;
                patrolWait = 0f;
                float threshold = stopDistance + (State == SpiderMovementState.Hold ? 0.05f : 0f);
                return State = targetDistance <= threshold ? SpiderMovementState.Hold : SpiderMovementState.Chase;
            }
            if (targetExists && hasSeenTarget)
            {
                unseenSeconds += dt;
                if (unseenSeconds < Math.Max(0f, memorySeconds)) return State = SpiderMovementState.Search;
            }
            hasSeenTarget = false;
            if (waypointCount <= 0) return State = SpiderMovementState.Idle;
            PatrolIndex %= waypointCount;
            if (State == SpiderMovementState.Patrol && (arrived || routeBlocked))
            {
                patrolWait += dt;
                if (patrolWait >= 1f)
                {
                    PatrolIndex = (PatrolIndex + 1) % waypointCount;
                    patrolWait = 0f;
                }
            }
            else patrolWait = 0f;
            return State = SpiderMovementState.Patrol;
        }
    }
}
