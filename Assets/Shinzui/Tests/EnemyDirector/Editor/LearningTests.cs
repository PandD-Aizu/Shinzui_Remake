using System;
using NUnit.Framework;
using Shinzui.Application.AI.Enemy;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces.Enemy;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Tests.EnemyDirector
{
    /// <summary>学習条件と解放前後の行動差を確認</summary>
    public sealed class LearningTests
    {
        /// <summary>視認済みの近距離サンプルを作成</summary>
        /// <returns>追跡を確定する入力</returns>
        private static EnemyBrainInput Seen() => new(Vector3.zero,
            new SightSample(true, 2f, 0f, 1f, Vector3.forward * 5f, Vector3.forward), default);

        /// <summary>知覚のない入力を作成</summary>
        /// <param name="position">敵の位置</param>
        /// <returns>入力</returns>
        private static EnemyBrainInput Silent(Vector3 position = default) => new(position, default, default);

        /// <summary>同じ戦術三回の閾値で対応する行動だけを解放</summary>
        /// <param name="tactic">戦術</param>
        /// <param name="behavior">解放対象</param>
        [TestCase(PlayerTacticKind.DecoyThrow, EnemyBehaviorFlags.DecoyWariness)]
        [TestCase(PlayerTacticKind.StrobeHitEnemy, EnemyBehaviorFlags.StrobeResistance)]
        [TestCase(PlayerTacticKind.BrokeSightAtCorner, EnemyBehaviorFlags.PredictiveChase)]
        public void RepeatedTacticUnlocksItsBehavior(PlayerTacticKind tactic, EnemyBehaviorFlags behavior)
        {
            var tracker = new PlayerTacticTracker(new BehaviorUnlockSettings());
            tracker.Record(tactic);
            tracker.Record(tactic);
            tracker.Tick(.1f, false, default, Array.Empty<EnemyReport>(), 1);
            Assert.That(tracker.State.Unlocked, Is.EqualTo(EnemyBehaviorFlags.None));
            tracker.Record(tactic);
            tracker.Tick(.1f, false, default, Array.Empty<EnemyReport>(), 1);
            Assert.That(tracker.State.Unlocked, Is.EqualTo(behavior));
        }

        /// <summary>既定ルールの閾値と共有クールダウン、待機中の条件を確認</summary>
        [Test] public void UnlocksOneBehaviorAtATimeAndKeepsPendingRules()
        {
            var tracker = new PlayerTacticTracker(new BehaviorUnlockSettings());
            tracker.Record(PlayerTacticKind.EscapedSearch);
            tracker.Tick(.1f, false, default, Array.Empty<EnemyReport>(), 1);
            Assert.That(tracker.State.Unlocked, Is.EqualTo(EnemyBehaviorFlags.None));
            tracker.Record(PlayerTacticKind.EscapedSearch);
            tracker.Tick(.1f, false, default, Array.Empty<EnemyReport>(), 1);
            Assert.That(tracker.State.Unlocked, Is.EqualTo(EnemyBehaviorFlags.SearchSideRooms));
            tracker.Record(PlayerTacticKind.EscapedSearch);
            tracker.Tick(59f, false, default, Array.Empty<EnemyReport>(), 2);
            Assert.That(tracker.State.Unlocked, Is.EqualTo(EnemyBehaviorFlags.SearchSideRooms));
            tracker.Tick(1f, false, default, Array.Empty<EnemyReport>(), 2);
            Assert.That(tracker.State.Unlocked, Is.EqualTo(EnemyBehaviorFlags.SearchSideRooms | EnemyBehaviorFlags.Backtrack));
            tracker.Tick(60f, false, default, Array.Empty<EnemyReport>(), 2);
            Assert.That(tracker.State.Unlocked.HasFlag(EnemyBehaviorFlags.AmbushFromAbove), Is.True);
            Assert.That(new PlayerTacticTracker(new BehaviorUnlockSettings()).State.Unlocked, Is.EqualTo(EnemyBehaviorFlags.None));
        }

        /// <summary>累積しゃがみ時間を敵数で増幅せず、警戒中や舞台裏を除外</summary>
        [Test] public void SneakingAccumulatesOnlyEligibleTimeOncePerFrame()
        {
            var tracker = new PlayerTacticTracker(new BehaviorUnlockSettings());
            var nearby = new EnemyReport(0, Vector3.right * 7f, default, true);
            var reports = new[] { nearby, nearby };
            tracker.Tick(5f, true, default, reports, 1);
            tracker.Tick(20f, false, default, reports, 1);
            tracker.Tick(5f, true, default, reports, 1);
            Assert.That(tracker.State.Count(PlayerTacticKind.SneakNearEnemy), Is.EqualTo(1));
            tracker.Tick(20f, true, default, new[]
            {
                new EnemyReport(0, default, default, true, awareness: EnemyAwarenessLevel.Alerted),
                new EnemyReport(1, default, default, true, isBackstage: true),
                new EnemyReport(2, Vector3.right * 9f, default, true)
            }, 1);
            Assert.That(tracker.State.Count(PlayerTacticKind.SneakNearEnemy), Is.EqualTo(1));
            tracker.Tick(20f, true, default, reports, 1);
            Assert.That(tracker.State.Unlocked, Is.EqualTo(EnemyBehaviorFlags.SlowStalk));
        }

        /// <summary>同じ石への反応を重複集計せず、解放後だけ飛来方向を逆に辿る</summary>
        [Test] public void DecoyReactionIsSharedAndReverseRouteIsGated()
        {
            var tracker = new PlayerTacticTracker(new BehaviorUnlockSettings());
            var hub = new NoiseEventHub();
            hub.Emit(new NoiseEvent(NoiseKind.Impact, Vector3.right * 8f, 10f, Vector3.right));
            var noise = hub.Drain()[0];
            var settings = new EnemyBrainSettings { investigateTurnDuration = .1f };
            var normal = new EnemyBrain(settings, new EnemyDirectorSettings(), tactics: tracker);
            var wary = new EnemyBrain(settings, new EnemyDirectorSettings(), tactics: tracker);
            wary.Blackboard.Unlocked = EnemyBehaviorFlags.DecoyWariness;
            var heard = new EnemyBrainInput(Vector3.zero, default, default, .4f, noise);
            normal.Tick(heard, default, .1f);
            wary.Tick(heard, default, .1f);
            Assert.That(tracker.State.Count(PlayerTacticKind.DecoyThrow), Is.EqualTo(1));
            var atImpact = Silent(noise.Position);
            Assert.That(normal.Tick(atImpact, default, .1f).Type, Is.EqualTo(EnemyIntentType.LookAround));
            var reverse = wary.Tick(atImpact, default, .1f);
            Assert.That(reverse.Type, Is.EqualTo(EnemyIntentType.MoveTo));
            Assert.That(reverse.Target, Is.EqualTo(Vector3.right * 3f));
        }

        /// <summary>近距離の視線切れから捜索へ進んだ時だけ角での逃走を記録</summary>
        [Test] public void CornerRequiresSightLossAndSearchRatherThanStunOrReacquisition()
        {
            var tracker = new PlayerTacticTracker(new BehaviorUnlockSettings());
            var brain = new EnemyBrain(new EnemyBrainSettings(), new EnemyDirectorSettings(), tactics: tracker);
            brain.Tick(Seen(), default, .1f);
            brain.Tick(Silent(), default, .5f);
            brain.Tick(Seen(), default, .1f);
            Assert.That(tracker.State.Count(PlayerTacticKind.BrokeSightAtCorner), Is.Zero);
            brain.Tick(new EnemyBrainInput(default, default, default, isStunned: true), default, 2f);
            brain.Tick(Silent(), default, .1f);
            Assert.That(tracker.State.Count(PlayerTacticKind.BrokeSightAtCorner), Is.Zero);
            brain.Tick(Seen(), default, .1f);
            brain.Tick(Silent(), default, 1.6f);
            Assert.That(tracker.State.Count(PlayerTacticKind.BrokeSightAtCorner), Is.EqualTo(1));
        }

        /// <summary>再発見による中断は逃走に含めず、期限切れ後の戻りを一回だけ行う</summary>
        [Test] public void SearchExpiryCountsOnceAndBacktrackDoesNotRestartSearch()
        {
            var tracker = new PlayerTacticTracker(new BehaviorUnlockSettings());
            var brain = new EnemyBrain(new EnemyBrainSettings { searchDuration = 3f }, new EnemyDirectorSettings(), tactics: tracker);
            brain.Blackboard.Unlocked = EnemyBehaviorFlags.Backtrack;
            brain.Tick(Seen(), default, .1f);
            brain.Tick(Silent(), default, 1.6f);
            brain.Tick(Seen(), default, .1f);
            Assert.That(tracker.State.Count(PlayerTacticKind.EscapedSearch), Is.Zero);
            brain.Tick(Silent(), default, 1.6f);
            brain.Tick(Silent(), default, 1.5f);
            Assert.That(tracker.State.Count(PlayerTacticKind.EscapedSearch), Is.EqualTo(1));
            var back = brain.Tick(Silent(), default, 1.6f);
            Assert.That(brain.ActivePath, Does.Contain("Backtrack"));
            Assert.That(back.Target, Is.EqualTo(Vector3.forward * 5f));
            brain.Tick(Silent(back.Target), default, .1f);
            Assert.That(brain.Tick(Silent(), default, 10f).Type, Is.EqualTo(EnemyIntentType.Wander));
            Assert.That(tracker.State.Count(PlayerTacticKind.EscapedSearch), Is.EqualTo(1));
        }

        /// <summary>小部屋の優先と低速、聴取中の停止を解放後だけ使う</summary>
        [Test] public void SearchUsesProviderAndSlowStalkPauses()
        {
            var points = new SearchPoints();
            var settings = new EnemyBrainSettings { lookAroundDuration = .1f, searchDuration = 30f };
            var brain = new EnemyBrain(settings, new EnemyDirectorSettings(), points: points);
            brain.ConfirmEmerged(Vector3.zero);
            brain.Tick(Silent(), default, .1f);
            Assert.That(points.IncludeRooms, Is.False);
            Assert.That(brain.Tick(Silent(), default, .2f).SpeedMultiplier, Is.EqualTo(.8f));
            brain.Tick(Silent(Vector3.right * 5f), default, .1f);
            Assert.That(brain.Tick(Silent(Vector3.right * 5f), default, .05f).Type, Is.EqualTo(EnemyIntentType.LookAround));
            brain.ConfirmEmerged(Vector3.zero);
            brain.Blackboard.Unlocked = EnemyBehaviorFlags.SlowStalk | EnemyBehaviorFlags.SearchSideRooms;
            brain.Tick(Silent(), default, .1f);
            Assert.That(points.IncludeRooms, Is.True);
            Assert.That(brain.Tick(Silent(), default, .2f).SpeedMultiplier, Is.EqualTo(.4f));
            Assert.That(brain.Tick(Silent(), default, 3.1f).Type, Is.EqualTo(EnemyIntentType.LookAround));
        }

        /// <summary>聴覚半径と着弾への疑いだけを解放条件で変える</summary>
        [Test] public void LearningChangesHearingAndOnlyImpactSuspicion()
        {
            var settings = new EnemyBrainSettings();
            var impact = new NoiseEvent(NoiseKind.Impact, default, 10f);
            Assert.That(EnemyPerception.NoiseGain(impact, 11f, true, settings), Is.Zero);
            Assert.That(EnemyPerception.NoiseGain(impact, 11f, true, settings, EnemyBehaviorFlags.SlowStalk), Is.GreaterThan(0f));
            Assert.That(EnemyPerception.NoiseGain(impact, 0f, true, settings, EnemyBehaviorFlags.DecoyWariness), Is.EqualTo(.3f));
            Assert.That(EnemyPerception.NoiseGain(new NoiseEvent(NoiseKind.Alarm, default, 10f), 0f, true,
                settings, EnemyBehaviorFlags.DecoyWariness), Is.EqualTo(.6f));
        }

        /// <summary>耐性解放後だけ命中時の光源を調査し、新しい視認が優先される</summary>
        [Test] public void StrobeInvestigationIsGatedAndYieldsToSight()
        {
            var brain = new EnemyBrain(new EnemyBrainSettings(), new EnemyDirectorSettings());
            Vector3 source = Vector3.right * 7f;
            brain.InvestigateStrobe(source);
            Assert.That(brain.StrobeDuration(4f, true), Is.EqualTo(4f));
            Assert.That(brain.Tick(Silent(), default, .1f).Type, Is.EqualTo(EnemyIntentType.Wander));
            brain.Blackboard.Unlocked = EnemyBehaviorFlags.StrobeResistance;
            Assert.That(brain.StrobeDuration(4f, true), Is.EqualTo(2f));
            Assert.That(brain.StrobeDuration(4f, false), Is.EqualTo(4f));
            brain.InvestigateStrobe(source);
            Assert.That(brain.Tick(Silent(), default, .1f).Target, Is.EqualTo(source));
            Assert.That(brain.Tick(Seen(), default, .1f).Type, Is.EqualTo(EnemyIntentType.Chase));
        }

        private sealed class SearchPoints : IEnemySearchPointProvider
        {
            public bool IncludeRooms;
            /// <summary>小部屋の要求を記録して一つだけ候補を返す</summary>
            /// <param name="center">中心</param>
            /// <param name="radius">半径</param>
            /// <param name="includeSideRooms">小部屋を優先するか</param>
            /// <param name="buffer">格納先</param>
            /// <returns>候補数</returns>
            public int GetSearchPoints(Vector3 center, float radius, bool includeSideRooms, Span<Vector3> buffer)
            { IncludeRooms = includeSideRooms; buffer[0] = Vector3.right * 5f; return 1; }
            /// <summary>退出候補は使わない</summary>
            /// <param name="from">始点</param>
            /// <param name="buffer">格納先</param>
            /// <returns>候補なし</returns>
            public int GetExitPoints(Vector3 from, Span<Vector3> buffer) => 0;
            /// <summary>出現候補は使わない</summary>
            /// <param name="playerPosition">中心</param>
            /// <param name="minDistance">最短距離</param>
            /// <param name="maxDistance">最長距離</param>
            /// <param name="includeUpperFloor">上階を含むか</param>
            /// <param name="isVisibleToPlayer">視認判定</param>
            /// <param name="point">結果</param>
            /// <returns>候補なし</returns>
            public bool TryFindEmergePoint(Vector3 playerPosition, float minDistance, float maxDistance,
                bool includeUpperFloor, Func<Vector3, bool> isVisibleToPlayer, out Vector3 point)
            { point = default; return false; }
        }
    }
}
