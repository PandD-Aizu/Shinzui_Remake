using Shinzui.Application.AI.BehaviorTree;
using Shinzui.Domain.ValueObjects.Enemy;

namespace Shinzui.Application.AI.Enemy
{
    /// <summary>知覚に基づく行動の優先順位を構築</summary>
    public static class EnemyBrainTreeFactory
    {
        /// <summary>停止、追跡、捜索、調査、巡回の順でツリーを生成</summary>
        /// <param name="brain">行動と記憶の所有者</param>
        /// <returns>初期化後に割り当てを行わないツリー</returns>
        public static BtNode Create(EnemyBrain brain)
        {
            // 未解放の追加行動はGateで通過させない
            return new BtSelector("Root",
                new BtAction("Backstage", brain.Backstage),
                new BtAction("Stunned", brain.Stunned),
                new BtAction("WitnessedLocker", brain.OpenWitnessedLocker),
                new BtSelector("Chase",
                    new BtGate(EnemyBehaviorFlags.PredictiveChase,
                        new BtAction("Predict", brain.PredictiveChase), () => brain.Blackboard.Unlocked),
                    new BtAction("LastSeen", brain.Chase)),
                new BtGate(EnemyBehaviorFlags.CheckHidingSpots,
                    new BtAction("InspectLocker", brain.InspectLocker), () => brain.Blackboard.Unlocked),
                new BtGuard("Search", () => brain.CanSearch,
                    new BtTimeLimit(brain.SearchDuration,
                        new BtSequence("Route",
                            new BtAction("Approach", brain.SearchApproach, brain.ResetSearchMove),
                            new BtAction("LookAround", brain.SearchLook, brain.ResetSearchLook),
                            new BtSelector("Patrol",
                                new BtGate(EnemyBehaviorFlags.SlowStalk,
                                    new BtAction("ListenAndWalk", brain.SlowStalk, brain.ResetSlowStalk), () => brain.Blackboard.Unlocked),
                                new BtAction("Walk", brain.SearchPatrol, brain.ResetPatrol))), brain.SearchExpired)),
                new BtGuard("Investigate", () => brain.CanInvestigate,
                    new BtSequence("Inspect",
                        new BtAction("Turn", brain.TurnToClue, brain.ResetTurn),
                        new BtAction("Approach", brain.InvestigateApproach, brain.ResetMove),
                        new BtSelector("Decoy",
                            new BtGate(EnemyBehaviorFlags.DecoyWariness,
                                new BtAction("Reverse", brain.ReverseDecoy, brain.ResetReverse), () => brain.Blackboard.Unlocked),
                            new BtAction("Skip", brain.Skip)),
                        new BtAction("LookAround", brain.InvestigateLook, brain.ResetLook),
                        new BtAction("Finish", brain.FinishInvestigation))),
                new BtGate(EnemyBehaviorFlags.Backtrack,
                    new BtAction("Backtrack", brain.Backtrack), () => brain.Blackboard.Unlocked),
                new BtGate(EnemyBehaviorFlags.LingerAfterSearch,
                    new BtAction("Linger", brain.Linger), () => brain.Blackboard.Unlocked),
                new BtAction("FollowHint", brain.FollowHint, brain.ResetHint),
                new BtAction("Wander", brain.Wander));
        }
    }
}
