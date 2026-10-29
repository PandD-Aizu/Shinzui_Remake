using NUnit.Framework;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.Domain.Entities.Enemy;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Tests.EnemyDirector
{
    /// <summary>知覚と音の受け渡しの境界条件を検証</summary>
    public sealed class PerceptionTests
    {
        /// <summary>大音量や繰り返しの音だけでは追跡を確定しない</summary>
        [Test] public void NoiseNeverEngages()
        {
            var meter = new SuspicionMeter();
            for (int i = 0; i < 10; i++)
            {
                meter.AddNoise(2f);
                meter.Tick(.1f, true);
            }
            Assert.That(meter.Level, Is.EqualTo(EnemyAwarenessLevel.Alerted));
        }

        /// <summary>視線喪失後は保持時間までAlertedを下回らない</summary>
        [Test] public void LostSightHoldsAlertedThenDecays()
        {
            var meter = new SuspicionMeter();
            meter.ForceEngage();
            meter.Tick(.1f, true);
            meter.Tick(7.9f, false);
            Assert.That(meter.Level, Is.EqualTo(EnemyAwarenessLevel.Alerted));
            meter.Tick(.2f, false);
            Assert.That(meter.Value, Is.LessThan(.65f));
        }

        /// <summary>疑いの蓄積が視覚でのみ追跡確定に届く</summary>
        [Test] public void SightAccumulatesToEngaged()
        {
            var meter = new SuspicionMeter();
            meter.AddSight(.6f);
            meter.Tick(.2f, true);
            Assert.That(meter.Level, Is.EqualTo(EnemyAwarenessLevel.Suspicious));
            meter.AddSight(.4f);
            meter.Tick(.2f, true);
            Assert.That(meter.Level, Is.EqualTo(EnemyAwarenessLevel.Engaged));
        }

        /// <summary>遠距離のしゃがみ消灯は走行点灯より発見されにくい</summary>
        [Test] public void CrouchingInDarkReducesSightGain()
        {
            var sight = new SightSample(true, 12f, 45f, 1f, Vector3.forward * 12f, default);
            var settings = new EnemyBrainSettings();
            float crouch = EnemyPerception.SightGain(sight, new PlayerSignature(true, false, false, false), 16f, 120f, settings);
            float running = EnemyPerception.SightGain(sight, new PlayerSignature(false, true, true, true), 16f, 120f, settings);
            Assert.That(crouch, Is.GreaterThan(0f).And.LessThan(running));
        }

        /// <summary>遮蔽によって可聴範囲が短くなる</summary>
        [Test] public void OcclusionReducesAudibleRadius()
        {
            var noise = new NoiseEvent(NoiseKind.Impact, Vector3.zero, 10f);
            var settings = new EnemyBrainSettings();
            Assert.That(EnemyPerception.NoiseGain(noise, 7f, true, settings), Is.GreaterThan(0f));
            Assert.That(EnemyPerception.NoiseGain(noise, 7f, false, settings), Is.Zero);
        }

        /// <summary>不可視サンプルから対象の位置と速度が漏れない</summary>
        [Test] public void InvisibleSampleClearsPositionAndVelocity()
        {
            var sample = new SightSample(true, 5f, 0f, 0f, Vector3.one, Vector3.one);
            Assert.That(sample.SeenPosition, Is.EqualTo(Vector3.zero));
            Assert.That(sample.SeenVelocity, Is.EqualTo(Vector3.zero));
        }

        /// <summary>音を全敵で共有でき、次のフレームへ重複配信しない</summary>
        [Test] public void DrainDeliversEachEventOnce()
        {
            var hub = new NoiseEventHub();
            hub.Emit(new NoiseEvent(NoiseKind.Alarm, Vector3.one, 30f));
            var frame = hub.Drain();
            hub.Emit(new NoiseEvent(NoiseKind.Impact, Vector3.zero, 10f));
            Assert.That(frame.Count, Is.EqualTo(1));
            Assert.That(frame[0].Kind, Is.EqualTo(NoiseKind.Alarm));
            Assert.That(hub.Drain()[0].Kind, Is.EqualTo(NoiseKind.Impact));
            Assert.That(hub.Drain(), Is.Empty);
        }
    }
}
