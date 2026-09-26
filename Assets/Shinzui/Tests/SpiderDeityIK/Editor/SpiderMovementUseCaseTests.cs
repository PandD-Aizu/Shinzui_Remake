using NUnit.Framework;
using Shinzui.Application.UseCases.Enemy;

namespace Shinzui.Tests.SpiderDeityIK
{
    public sealed class SpiderMovementUseCaseTests
    {
        [Test]
        public void SightMemoryHoldHysteresisAndTargetRemoval()
        {
            var ai = new SpiderMovementUseCase();
            Assert.That(ai.Tick(.1f, true, true, 1f, .65f, 2f, 2, false, false), Is.EqualTo(SpiderMovementState.Chase));
            Assert.That(ai.Tick(.1f, true, true, .6f, .65f, 2f, 2, false, false), Is.EqualTo(SpiderMovementState.Hold));
            Assert.That(ai.Tick(.1f, true, true, .68f, .65f, 2f, 2, false, false), Is.EqualTo(SpiderMovementState.Hold));
            Assert.That(ai.Tick(.1f, true, false, 10f, .65f, 2f, 2, false, false), Is.EqualTo(SpiderMovementState.Search));
            Assert.That(ai.Tick(2f, true, false, 10f, .65f, 2f, 2, false, false), Is.EqualTo(SpiderMovementState.Patrol));
            ai.Tick(.1f, true, true, 1f, .65f, 2f, 2, false, false);
            Assert.That(ai.Tick(.1f, false, false, 1f, .65f, 2f, 0, false, false), Is.EqualTo(SpiderMovementState.Idle));
        }

        [Test]
        public void PatrolWaitsThenSkipsBlockedPointWithoutIndexOverflow()
        {
            var ai = new SpiderMovementUseCase();
            ai.Tick(.1f, false, false, 0, .65f, 2, 2, false, false);
            ai.Tick(.5f, false, false, 0, .65f, 2, 2, false, true);
            Assert.That(ai.PatrolIndex, Is.Zero);
            ai.Tick(.6f, false, false, 0, .65f, 2, 2, false, true);
            Assert.That(ai.PatrolIndex, Is.EqualTo(1));
            ai.Tick(.1f, false, false, 0, .65f, 2, 1, false, false);
            Assert.That(ai.PatrolIndex, Is.Zero);
        }
    }
}
