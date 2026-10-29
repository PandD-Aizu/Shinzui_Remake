using NUnit.Framework;
using Shinzui.Application.UseCases.Interaction;
using Shinzui.View.Interaction;
using UnityEngine;

namespace Shinzui.Tests.Interaction
{
    /// <summary>長押し取得と輪郭マーカーの回帰確認</summary>
    public sealed class ItemPickupTests
    {
        /// <summary>短い押下では取得せず、満了時だけ一度確定する</summary>
        [Test]
        public void HoldCommitsExactlyOnce()
        {
            var pickup = new ItemPickupUseCase();
            pickup.Begin(1f);
            pickup.Tick(.5f, true, true);
            Assert.That(pickup.TryCommit(), Is.False);
            pickup.Tick(.5f, true, true);
            Assert.That(pickup.TryCommit(), Is.True);
            Assert.That(pickup.TryCommit(), Is.False);
        }

        /// <summary>ボタン解放と対象消失では取得を確定しない</summary>
        /// <param name="held">ボタン保持状態</param>
        /// <param name="valid">対象の有効性</param>
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void InterruptedHoldReturnsWithoutCommit(bool held, bool valid)
        {
            var pickup = new ItemPickupUseCase();
            pickup.Begin(1f);
            pickup.Tick(.6f, true, true);
            pickup.Tick(.01f, held, valid);
            Assert.That(pickup.State, Is.EqualTo(ItemPickupState.Returning));
            Assert.That(pickup.TryCommit(), Is.False);
            pickup.Tick(1f, false, false);
            Assert.That(pickup.IsActive, Is.False);
        }

        /// <summary>演出待ちは進まず、再開後のみ取得できる</summary>
        [Test]
        public void EffectWaitRequiresResume()
        {
            var pickup = new ItemPickupUseCase();
            pickup.Begin(1f);
            pickup.Tick(.65f, true, true);
            pickup.Pause();
            pickup.Tick(10f, true, true);
            Assert.That(pickup.Progress, Is.EqualTo(.65f));
            Assert.That(pickup.TryCommit(), Is.False);
            pickup.Resume();
            pickup.Tick(1f, true, true);
            Assert.That(pickup.TryCommit(), Is.True);
        }

        /// <summary>演出待ちでも解放すればキャンセルできる</summary>
        [Test]
        public void ReleaseDuringEffectCancels()
        {
            var pickup = new ItemPickupUseCase();
            pickup.Begin(1f);
            pickup.Tick(.65f, true, true);
            pickup.Pause();
            pickup.Tick(.01f, false, true);
            pickup.Resume();
            Assert.That(pickup.State, Is.EqualTo(ItemPickupState.Returning));
        }

        /// <summary>確定開始後の解放では結果を巻き戻さず、取得失敗も正常終了する</summary>
        /// <param name="success">取得の結果</param>
        [TestCase(true)]
        [TestCase(false)]
        public void CommitFinishesAtomically(bool success)
        {
            var pickup = new ItemPickupUseCase();
            pickup.Begin(1f);
            pickup.Tick(1f, true, true);
            pickup.TryCommit();
            pickup.Tick(.1f, false, false);
            Assert.That(pickup.State, Is.EqualTo(ItemPickupState.Committing));
            pickup.Finish(success);
            Assert.That(pickup.Succeeded, Is.EqualTo(success));
            pickup.Tick(1f, false, false);
            Assert.That(pickup.IsActive, Is.False);
        }

        /// <summary>動的に作った輪郭マーカーにも描画コンポーネントが付く</summary>
        [Test]
        public void RingHasRendererAndTransparentCenter()
        {
            var root = new GameObject("RingProbe", typeof(RectTransform), typeof(InteractionRingGraphic));
            var helper = new UnityEngine.UI.VertexHelper();
            var mesh = new Mesh();
            try
            {
                var ring = root.GetComponent<InteractionRingGraphic>();
                ring.rectTransform.sizeDelta = Vector2.one * 24f;
                Assert.That(root.GetComponent<CanvasRenderer>(), Is.Not.Null);
                typeof(InteractionRingGraphic).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly).Invoke(ring, new object[] { helper });
                helper.FillMesh(mesh);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                foreach (var vertex in mesh.vertices) Assert.That(vertex.magnitude, Is.GreaterThan(6f));
            }
            finally
            {
                helper.Dispose();
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
        }
    }
}
