using System;
using NUnit.Framework;
using Shinzui.Application.AI.Enemy;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.Application.UseCases.Player;
using Shinzui.Domain.Entities;
using Shinzui.Domain.ValueObjects.Enemy;
using Shinzui.Domain.ValueObjects.Player;
using UnityEngine;

namespace Shinzui.Tests.EnemyDirector
{
    /// <summary>隠れ状態を直接知らせずに目撃と気配だけで検査することを確認</summary>
    public sealed class HidingTests
    {
        /// <summary>一つのロッカーを登録</summary>
        /// <returns>地形一覧</returns>
        private static HidingSpotRegistry Spots()
        {
            var spots = new HidingSpotRegistry();
            spots.Register(new HidingSpotDto(1, Vector3.zero, Vector3.forward, Vector3.forward));
            return spots;
        }
        /// <summary>入口正面から知覚識別子だけを与える</summary>
        /// <param name="witness">目撃した入口</param>
        /// <param name="presence">気配の対象</param>
        /// <returns>知覚入力</returns>
        private static EnemyBrainInput Sense(int witness = 0, int presence = 0)
            => new(Vector3.forward, default, default, hidingSense: new HidingSense(witness, presence));
        /// <summary>スタミナを持つ初期状態のプレイヤーを作成</summary>
        /// <returns>プレイヤー</returns>
        private static PlayerEntity Player() => new(new PlayerSpeedStatus(), new PlayerCrouchStatus(), new PlayerStamina());
        /// <summary>息止めはスタミナを消費し、使い切ると二割回復するまで止められない</summary>
        [Test] public void BreathHoldConsumesStaminaAndWaitsForRecovery()
        {
            var player = Player();
            var hiding = new PlayerHidingUseCase(Spots(), new NoiseEventHub(), player);
            hiding.Enter(1, false);
            hiding.Tick(true, .1f);
            player.UpdateStamina(1f);
            Assert.That(hiding.IsHoldingBreath, Is.True);
            Assert.That(player.CurrentStamina.Value, Is.EqualTo(75f));
            player.UpdateStamina(3f);
            hiding.Tick(true, .1f);
            Assert.That(hiding.IsHoldingBreath, Is.False);
            player.UpdateStamina(1f);
            hiding.Tick(true, .1f);
            Assert.That(hiding.IsHoldingBreath, Is.False);
            player.UpdateStamina(1f);
            hiding.Tick(true, .1f);
            Assert.That(hiding.IsHoldingBreath, Is.True);
        }
        /// <summary>走る入室、スタミナ切れ、退出だけが音になり停止時間はスタミナを消費しない</summary>
        [Test] public void HidingLifecycleEmitsOnlyRequiredNoises()
        {
            var hub = new NoiseEventHub();
            var player = Player();
            var hiding = new PlayerHidingUseCase(Spots(), hub, player);
            Assert.That(hiding.Enter(9, true), Is.False);
            Assert.That(hiding.Enter(1, false), Is.True);
            Assert.That(hub.Drain(), Is.Empty);
            Assert.That(hiding.IsEntering, Is.True);
            hiding.Tick(true, 0f);
            player.UpdateStamina(0f);
            Assert.That(player.CurrentStamina.Value, Is.EqualTo(100f));
            for (int i = 0; i < 5; i++) { hiding.Tick(true, 1f); player.UpdateStamina(1f); }
            Assert.That(hiding.IsEntering, Is.False);
            Assert.That(hub.Drain()[0].Kind, Is.EqualTo(NoiseKind.Breath));
            hiding.Tick(true, .1f);
            Assert.That(hub.Drain(), Is.Empty);
            hiding.Exit();
            Assert.That(hiding.IsHidden, Is.False);
            Assert.That(hub.Drain()[0].Radius, Is.EqualTo(4f));
            hiding.Enter(1, true);
            Assert.That(hub.Drain()[0].Radius, Is.EqualTo(8f));
        }
        /// <summary>未解放では気配が渡っても検査せず、目撃した入口だけ開ける</summary>
        [Test] public void LockedBrainNeverInspectsUnlessEntryWasWitnessed()
        {
            var brain = new EnemyBrain(new EnemyBrainSettings(), new EnemyDirectorSettings(), hidingSpots: Spots());
            brain.ConfirmEmerged(Vector3.zero);
            for (int i = 0; i < 5; i++)
                Assert.That(brain.Tick(Sense(presence: 1), default, .5f).Type, Is.Not.EqualTo(EnemyIntentType.OpenHidingSpot));
            Assert.That(brain.InspectingSpotId, Is.Zero);
            var opened = brain.Tick(Sense(witness: 1), new DirectorDirective(DirectorDirectiveType.GoBackstage), .1f);
            Assert.That(opened.Type, Is.EqualTo(EnemyIntentType.OpenHidingSpot));
            Assert.That(opened.HidingSpotId, Is.EqualTo(1));
            brain.FinishHidingInspection();
            Assert.That(brain.Blackboard.WitnessedHidingSpotId, Is.Zero);
        }
        /// <summary>息止め時は気配で開かず、検査のクールダウンを守る</summary>
        [Test] public void BreathHoldingAndCooldownPreventImmediateReinspection()
        {
            var brain = new EnemyBrain(new EnemyBrainSettings { blindOpenChance = 0f }, new EnemyDirectorSettings(), hidingSpots: Spots());
            brain.Blackboard.Unlocked = EnemyBehaviorFlags.CheckHidingSpots;
            brain.ConfirmEmerged(Vector3.zero);
            Assert.That(brain.Tick(Sense(), default, .1f).Type, Is.EqualTo(EnemyIntentType.InspectHidingSpot));
            brain.Tick(Sense(), default, 3f);
            Assert.That(brain.InspectingSpotId, Is.Zero);
            Assert.That(brain.Tick(Sense(), default, .1f).Type, Is.Not.EqualTo(EnemyIntentType.InspectHidingSpot));
        }
        /// <summary>検査対象の気配だけが蓄積し開扉につながる</summary>
        [Test] public void PresenceAndBlindOpeningAreIndependentReasons()
        {
            var settings = new EnemyBrainSettings { blindOpenChance = 0f, inspectDuration = 4f };
            var brain = new EnemyBrain(settings, new EnemyDirectorSettings(), hidingSpots: Spots());
            brain.Blackboard.Unlocked = EnemyBehaviorFlags.CheckHidingSpots;
            brain.ConfirmEmerged(Vector3.zero);
            brain.Tick(Sense(), default, .1f);
            Assert.That(brain.Tick(Sense(presence: 2), default, .1f).Type, Is.EqualTo(EnemyIntentType.InspectHidingSpot));
            Assert.That(brain.Tick(Sense(presence: 1), default, 3f).Type, Is.EqualTo(EnemyIntentType.OpenHidingSpot));
            var blind = new EnemyBrain(new EnemyBrainSettings { blindOpenChance = 1f }, new EnemyDirectorSettings(), hidingSpots: Spots());
            blind.Blackboard.Unlocked = EnemyBehaviorFlags.CheckHidingSpots;
            blind.ConfirmEmerged(Vector3.zero);
            Assert.That(blind.Tick(Sense(), default, 3f).Type, Is.EqualTo(EnemyIntentType.OpenHidingSpot));
        }
        /// <summary>二回の隠れ逃走で検査、四回で待ち伏せを間隔を空けて解放</summary>
        [Test] public void HidingEscapesUnlockInspectionThenLingering()
        {
            var tracker = new PlayerTacticTracker(new BehaviorUnlockSettings());
            tracker.Record(PlayerTacticKind.HidDuringSearch);
            tracker.Tick(.1f, false, default, Array.Empty<EnemyReport>(), 1);
            Assert.That(tracker.State.Unlocked, Is.EqualTo(EnemyBehaviorFlags.None));
            tracker.Record(PlayerTacticKind.EscapedSearch); tracker.Record(PlayerTacticKind.EscapedSearch);
            tracker.Record(PlayerTacticKind.HidDuringSearch);
            tracker.Tick(.1f, false, default, Array.Empty<EnemyReport>(), 1);
            Assert.That(tracker.State.Unlocked, Is.EqualTo(EnemyBehaviorFlags.CheckHidingSpots));
            tracker.Record(PlayerTacticKind.HidDuringSearch); tracker.Record(PlayerTacticKind.HidDuringSearch);
            tracker.Tick(60f, false, default, Array.Empty<EnemyReport>(), 1);
            Assert.That(tracker.State.Unlocked.HasFlag(EnemyBehaviorFlags.LingerAfterSearch), Is.True);
        }
        /// <summary>マップ破棄後の古い目撃先では開扉しない</summary>
        [Test] public void RemovedLockerAndEmptyRegistryRemainSafe()
        {
            var spots = Spots(); spots.Remove(1);
            var brain = new EnemyBrain(new EnemyBrainSettings(), new EnemyDirectorSettings(), hidingSpots: spots);
            brain.Blackboard.Unlocked = EnemyBehaviorFlags.CheckHidingSpots;
            Assert.That(brain.Tick(Sense(witness: 1), default, .1f).Type, Is.EqualTo(EnemyIntentType.Wander));
            Assert.That(brain.Blackboard.WitnessedHidingSpotId, Is.Zero);
            Assert.That(new PlayerHidingUseCase(spots, new NoiseEventHub(), Player()).Enter(1, false), Is.False);
        }
    }
}
