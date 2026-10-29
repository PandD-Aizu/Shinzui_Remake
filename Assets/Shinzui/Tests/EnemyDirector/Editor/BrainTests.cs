using System;
using System.Linq;
using NUnit.Framework;
using Shinzui.Application.AI.Enemy;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Tests.EnemyDirector
{
    /// <summary>敵の独立判断と知覚境界を検証</summary>
    public sealed class BrainTests
    {
        /// <summary>既定の脳を作成</summary>
        /// <returns>新しい脳</returns>
        private static EnemyBrain Brain() => new(new EnemyBrainSettings(), new EnemyDirectorSettings());

        /// <summary>近距離の可視サンプルを作成</summary>
        /// <param name="position">視認位置</param>
        /// <returns>視覚入力</returns>
        private static EnemyBrainInput Seen(Vector3 position)
            => new(Vector3.zero, new SightSample(true, 2f, 0f, 1f, position, Vector3.forward), default);

        /// <summary>無刺激やヒントだけでは追跡せずヒント領域を巡回</summary>
        [Test] public void NoSensesNeverChases()
        {
            var brain = Brain();
            var input = new EnemyBrainInput(Vector3.zero, default, default);
            Assert.That(brain.Tick(input, default, .1f).Type, Is.EqualTo(EnemyIntentType.Wander));
            Vector3 center = new(10, 0, 10);
            var intent = brain.Tick(input, new DirectorDirective(DirectorDirectiveType.HintArea, center, 5f), .1f);
            Assert.That(intent.Type, Is.EqualTo(EnemyIntentType.MoveTo));
            Assert.That(Vector3.Distance(intent.Target, center), Is.LessThanOrEqualTo(5f));
            Assert.That(brain.ActivePath, Does.Contain("FollowHint"));
        }

        /// <summary>音の方向へ向いた後に歩いて調べ、音だけでは追跡しない</summary>
        [Test] public void NoiseInvestigatesAndSequenceAdvances()
        {
            var brain = Brain();
            Vector3 impact = Vector3.right * 8f;
            var input = new EnemyBrainInput(Vector3.zero, default, default, .4f, new NoiseEvent(NoiseKind.Impact, impact, 10f));
            Assert.That(brain.Tick(input, default, .1f).Type, Is.EqualTo(EnemyIntentType.LookAround));
            var silent = new EnemyBrainInput(Vector3.zero, default, default);
            EnemyIntent intent = default;
            for (int i = 0; i < 12; i++) intent = brain.Tick(silent, default, .1f);
            Assert.That(intent.Type, Is.EqualTo(EnemyIntentType.MoveTo));
            Assert.That(intent.Target, Is.EqualTo(impact));
            Assert.That(intent.SpeedMultiplier, Is.EqualTo(.5f));
            Assert.That(brain.Awareness, Is.Not.EqualTo(EnemyAwarenessLevel.Engaged));
        }

        /// <summary>見失った後は最終目撃地点を使い、新しいヒントで追跡先を変えない</summary>
        [Test] public void LostSightUsesLastSeenThenSearches()
        {
            var brain = Brain();
            Vector3 seen = Vector3.forward * 5f;
            brain.Tick(Seen(seen), default, .1f);
            var hint = new DirectorDirective(DirectorDirectiveType.HintArea, Vector3.one * 100f, 5f);
            var silent = new EnemyBrainInput(Vector3.zero, default, default);
            var grace = brain.Tick(silent, hint, .5f);
            Assert.That(grace.Type, Is.EqualTo(EnemyIntentType.Chase));
            Assert.That(grace.Target, Is.EqualTo(seen));
            var search = brain.Tick(silent, hint, 1.1f);
            Assert.That(search.Type, Is.EqualTo(EnemyIntentType.MoveTo));
            Assert.That(search.Target, Is.EqualTo(seen));
            Assert.That(brain.ActivePath, Does.Contain("Search"));
        }

        /// <summary>発見中は退場指示より自分の視認を優先</summary>
        [Test] public void EngagedIgnoresBackstageDirective()
        {
            var brain = Brain();
            var intent = brain.Tick(Seen(Vector3.forward * 2f), new DirectorDirective(DirectorDirectiveType.GoBackstage), .1f);
            Assert.That(intent.Type, Is.EqualTo(EnemyIntentType.Chase));
            Assert.That(brain.Blackboard.IsBackstage, Is.False);
            var exit = new DirectorDirective(DirectorDirectiveType.GoBackstage, Vector3.zero);
            var silent = new EnemyBrainInput(Vector3.zero, default, default);
            Assert.That(brain.Tick(silent, exit, .5f).Type, Is.EqualTo(EnemyIntentType.Chase));
            var visible = new EnemyBrainInput(Vector3.zero, default, default, isVisibleToPlayer: true);
            Assert.That(brain.Tick(visible, exit, 1.1f).Type, Is.EqualTo(EnemyIntentType.GoBackstage));
            Assert.That(brain.Tick(silent, exit, .1f).Type, Is.EqualTo(EnemyIntentType.Hidden));
            brain.ConfirmBackstage();
            var hidden = new EnemyBrainInput(Vector3.zero, default, default, 1f,
                new NoiseEvent(NoiseKind.Alarm, Vector3.one, 30f), isReady: false, isBackstage: true);
            Assert.That(brain.Tick(hidden, default, .1f).Type, Is.EqualTo(EnemyIntentType.Hidden));
            Assert.That(brain.Blackboard.HasClue, Is.False);
            Assert.That(brain.Tick(hidden, new DirectorDirective(DirectorDirectiveType.EmergeAt, Vector3.right * 20f), .1f).Type,
                Is.EqualTo(EnemyIntentType.Emerge));
            brain.ConfirmEmerged(Vector3.right * 10f);
            Assert.That(brain.Awareness, Is.EqualTo(EnemyAwarenessLevel.Alerted));
        }

        /// <summary>ストロボ停止が追跡へ割り込み、解除後の視認で再開</summary>
        [Test] public void StunPreemptsChase()
        {
            var brain = Brain();
            brain.Tick(Seen(Vector3.forward * 2f), default, .1f);
            var stopped = new EnemyBrainInput(Vector3.zero, default, default, isStunned: true);
            Assert.That(brain.Tick(stopped, default, .2f).Type, Is.EqualTo(EnemyIntentType.Idle));
            Assert.That(brain.Tick(Seen(Vector3.forward * 2f), default, .1f).Type, Is.EqualTo(EnemyIntentType.Chase));
        }

        /// <summary>捜索は再発見されなければ期限で終了する</summary>
        [Test] public void SearchTimesOut()
        {
            var brain = Brain();
            brain.Tick(Seen(Vector3.forward * 5f), default, .1f);
            var silent = new EnemyBrainInput(Vector3.zero, default, default);
            brain.Tick(silent, default, 1.6f);
            EnemyIntent result = default;
            for (int i = 0; i < 21; i++) result = brain.Tick(silent, default, 1f);
            Assert.That(result.Type, Is.EqualTo(EnemyIntentType.Wander));
            Assert.That(brain.Blackboard.HasClue, Is.False);
        }

        /// <summary>捜索中も新しい視認が先頭評価から割り込む</summary>
        [Test] public void SightPreemptsSearch()
        {
            var brain = Brain();
            var noise = new EnemyBrainInput(Vector3.zero, default, default, .8f,
                new NoiseEvent(NoiseKind.Alarm, Vector3.right * 10f, 30f));
            Assert.That(brain.Tick(noise, default, .1f).Type, Is.EqualTo(EnemyIntentType.MoveTo));
            Assert.That(brain.Tick(Seen(Vector3.forward * 2f), default, .1f).Type, Is.EqualTo(EnemyIntentType.Chase));
        }

        /// <summary>予測が解放された場合も観測済み速度だけを使う</summary>
        [Test] public void PredictiveGateUsesObservedVelocity()
        {
            var brain = Brain();
            Vector3 seen = Vector3.forward * 5f;
            brain.Tick(Seen(seen), default, .1f);
            brain.Blackboard.Unlocked = EnemyBehaviorFlags.PredictiveChase;
            var result = brain.Tick(new EnemyBrainInput(Vector3.zero, default, default), default, .1f);
            Assert.That(result.Target, Is.EqualTo(seen + Vector3.forward * 1.5f));
        }

        /// <summary>自身の位置以外に直接座標を受け取る入力や全知の依存を持たない</summary>
        [Test] public void BrainInputHasNoDirectPlayerPosition()
        {
            var vectors = typeof(EnemyBrainInput).GetProperties().Where(p => p.PropertyType == typeof(Vector3)).ToArray();
            Assert.That(vectors.Select(p => p.Name), Is.EqualTo(new[] { "EnemyPosition" }));
            Assert.That(typeof(EnemyBrainInput).GetProperties().Any(p => p.PropertyType == typeof(EnemyWorldState)), Is.False);
            Assert.That(typeof(EnemyBrain).GetConstructors().SelectMany(c => c.GetParameters())
                .Any(p => p.ParameterType == typeof(EnemyWorldState)), Is.False);
        }

        /// <summary>通常更新とデバッグパスの参照では割り当てを発生させない</summary>
        [Test] public void TickDoesNotAllocate()
        {
            var brain = Brain();
            var input = new EnemyBrainInput(Vector3.zero, default, default, .8f,
                new NoiseEvent(NoiseKind.Alarm, Vector3.right * 10f, 30f));
            brain.Tick(input, default, .1f);
            var silent = new EnemyBrainInput(Vector3.zero, default, default);
            brain.Tick(silent, default, .1f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 60; i++)
            {
                brain.Tick(silent, default, .1f);
                _ = brain.ActivePath;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }
    }
}
