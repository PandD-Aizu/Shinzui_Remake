using NUnit.Framework;
using Shinzui.Domain.Entities.Enemy;

namespace Shinzui.Tests.EnemyDirector
{
    /// <summary>緊張度と最短休息時間を検証</summary>
    public sealed class MenaceTests
    {
        /// <summary>緊張度が0から1の範囲で上昇、減衰する</summary>
        [Test] public void MenaceRisesAndDecaysWithinRange()
        {
            var gauge = new MenaceGauge();
            gauge.Tick(.1f, 5f);
            Assert.That(gauge.Value, Is.EqualTo(.5f).Within(.001f));
            gauge.Tick(.1f, 20f);
            Assert.That(gauge.Value, Is.EqualTo(1f));
            gauge.Tick(-.12f, 20f);
            Assert.That(gauge.Value, Is.Zero);
        }

        /// <summary>緊張度が下がっても全退避者の最短時間が終わるまで復帰しない</summary>
        [Test] public void BackstageWaitsForDurationAndLowMenace()
        {
            var gauge = new MenaceGauge();
            gauge.Tick(1f, 1f);
            gauge.BeginBackstage(20f);
            gauge.Tick(-.12f, 10f);
            Assert.That(gauge.CanEmerge, Is.False);
            gauge.BeginBackstage(25f);
            gauge.Tick(0f, 24f);
            Assert.That(gauge.CanEmerge, Is.False);
            gauge.Tick(0f, 1f);
            Assert.That(gauge.CanEmerge, Is.True);
            gauge.Tick(.5f, 1f);
            Assert.That(gauge.CanEmerge, Is.False);
            gauge.EndBackstage();
            Assert.That(gauge.IsRecovering, Is.False);
        }
    }
}
