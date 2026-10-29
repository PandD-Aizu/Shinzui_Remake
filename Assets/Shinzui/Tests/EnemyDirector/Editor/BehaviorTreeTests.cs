using NUnit.Framework;
using Shinzui.Application.AI.BehaviorTree;
using Shinzui.Domain.ValueObjects.Enemy;

namespace Shinzui.Tests.EnemyDirector
{
    /// <summary>ツリーの割り込みと時間制約を検証</summary>
    public sealed class BehaviorTreeTests
    {
        /// <summary>未解放時は次の子へ進み、解放後は実行中の低優先を中断</summary>
        [Test] public void GateUnlockPreemptsRunningChild()
        {
            EnemyBehaviorFlags flags = EnemyBehaviorFlags.None;
            int interrupted = 0;
            var tree = new BtSelector("Root",
                new BtGate(EnemyBehaviorFlags.PredictiveChase, new BtAction("High", _ => BtStatus.Running), () => flags),
                new BtAction("Low", _ => BtStatus.Running, () => interrupted++));
            tree.Tick(.1f);
            Assert.That(tree.ActivePath, Is.EqualTo("Root/Low"));
            flags = EnemyBehaviorFlags.PredictiveChase;
            tree.Tick(.1f);
            Assert.That(tree.ActivePath, Does.EndWith("/High"));
            Assert.That(interrupted, Is.EqualTo(1));
        }

        /// <summary>Sequenceは実行中の子を記憶し中断時だけ先頭へ戻る</summary>
        [Test] public void SequenceResumesThenResets()
        {
            int started = 0;
            var sequence = new BtSequence("Sequence", new BtCondition("Start", () => { started++; return true; }),
                new BtAction("Wait", _ => BtStatus.Running));
            sequence.Tick(.1f);
            sequence.Tick(.1f);
            Assert.That(started, Is.EqualTo(1));
            sequence.Reset();
            sequence.Tick(.1f);
            Assert.That(started, Is.EqualTo(2));
        }

        /// <summary>クールダウン中は子を再実行しない</summary>
        [Test] public void CooldownBlocksUntilElapsed()
        {
            int runs = 0;
            var node = new BtCooldown(1f, new BtAction("Run", _ => { runs++; return BtStatus.Success; }));
            node.Tick(.1f);
            node.Reset();
            Assert.That(node.Tick(.4f), Is.EqualTo(BtStatus.Failure));
            Assert.That(node.Tick(.7f), Is.EqualTo(BtStatus.Success));
            Assert.That(runs, Is.EqualTo(2));

            // 高優先の行動に割り込まれている間も待ち時間を進める
            bool high = false;
            var root = new BtSelector("Root", new BtGuard("High", () => high,
                new BtAction("Wait", _ => BtStatus.Running)), node);
            root.Tick(3f);
            high = true;
            root.Tick(2f);
            high = false;
            Assert.That(root.Tick(.1f), Is.EqualTo(BtStatus.Success));
            Assert.That(runs, Is.EqualTo(4));
        }

        /// <summary>制限時間で実行中の子を中断して通知</summary>
        [Test] public void TimeLimitAbortsRunningChild()
        {
            int expired = 0;
            int reset = 0;
            var node = new BtTimeLimit(1f, new BtAction("Wait", _ => BtStatus.Running, () => reset++), () => expired++);
            Assert.That(node.Tick(.4f), Is.EqualTo(BtStatus.Running));
            Assert.That(node.Tick(.7f), Is.EqualTo(BtStatus.Failure));
            Assert.That(expired, Is.EqualTo(1));
            Assert.That(reset, Is.EqualTo(1));
        }
    }
}
