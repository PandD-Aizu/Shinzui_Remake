using System;
using NUnit.Framework;
using Shinzui.Application.Interfaces.Enemy;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Tests.EnemyDirector
{
    /// <summary>統括AIのヒントと観測だけを検証</summary>
    public sealed class DirectorTests
    {
        private EnemyDirectorSettings _settings;
        private EnemyDirectorUseCase _director;
        private SoundSpy _sound;
        private MenaceSettings _menace;
        private bool _candidateVisible;

        /// <summary>各テストで統括状態を独立させる</summary>
        [SetUp] public void SetUp()
        {
            _settings = new EnemyDirectorSettings();
            _sound = new SoundSpy();
            _menace = new MenaceSettings { backstageMinDuration = 20f, backstageMaxDuration = 20f };
            _candidateVisible = false;
            _director = new EnemyDirectorUseCase(_sound, _settings, _menace, new PointStub());
        }
        /// <summary>通知を解放</summary>
        [TearDown] public void TearDown() => _director.Dispose();

        /// <summary>敵の観測状態を作成</summary>
        /// <param name="id">識別子</param>
        /// <param name="awareness">警戒段階</param>
        /// <param name="visible">プレイヤーに見えるか</param>
        /// <param name="active">動作可能か</param>
        /// <returns>報告</returns>
        /// <param name="backstage">舞台裏にいるか</param>
        /// <param name="pathDistance">経路長</param>
        private static EnemyReport Report(int id = 0, EnemyAwarenessLevel awareness = EnemyAwarenessLevel.Unaware,
            bool visible = true, bool active = true, bool backstage = false, float pathDistance = -1f)
            => new(id, Vector3.forward * (id + 5f), default, active, visible, awareness, backstage, pathDistance < 0f ? id + 5f : pathDistance);

        /// <summary>統括時間を進める</summary>
        /// <param name="dt">経過秒数</param>
        /// <param name="position">プレイヤーの位置</param>
        /// <param name="reports">敵の報告</param>
        /// <returns>指示</returns>
        private DirectorDirective[] Step(float dt, Vector3 position, params EnemyReport[] reports)
            => _director.DecideDirectives(new EnemyWorldState(position, reports, false, default, default, isVisibleToPlayer: _ => _candidateVisible), dt);

        /// <summary>近いだけでは発見も直接追跡命令も生まれない</summary>
        [Test] public void ProximityOnlyProducesHints()
        {
            for (int i = 0; i < 30; i++)
            {
                var type = Step(.5f, Vector3.zero, Report())[0].Type;
                Assert.That(type == DirectorDirectiveType.None || type == DirectorDirectiveType.HintArea || type == DirectorDirectiveType.GoBackstage, Is.True);
            }
            Assert.That(_director.IsPlayerFound.CurrentValue, Is.False);
            Assert.That(_sound.Count, Is.Zero);
        }

        /// <summary>ヒントは曖昧な領域で、更新間隔までは移動に追従しない</summary>
        [Test] public void HintIsImpreciseAndHeld()
        {
            _settings.ambientDuration = .5f;
            Vector3 player = new(1, 0, 1);
            var hint = Step(.5f, player, Report())[0];
            Assert.That(hint.Type, Is.EqualTo(DirectorDirectiveType.HintArea));
            Assert.That(hint.Position, Is.Not.EqualTo(player));
            Assert.That(hint.Radius, Is.GreaterThan(0f));
            Assert.That(Step(.1f, Vector3.one * 100f, Report())[0].Position, Is.EqualTo(hint.Position));
        }

        /// <summary>ヒント対象は一体で報告順が変わっても主役を維持</summary>
        [Test] public void HintHasSingleStableOwner()
        {
            _settings.ambientDuration = .1f;
            Step(.1f, Vector3.zero, Report(0), Report(1));
            var directives = Step(.1f, Vector3.zero, Report(1), Report(0));
            Assert.That(directives[0].Type, Is.EqualTo(DirectorDirectiveType.None));
            Assert.That(directives[1].Type, Is.EqualTo(DirectorDirectiveType.HintArea));
            Step(.1f, Vector3.zero, Report(0, pathDistance: 10f), Report(1, pathDistance: 1f));
            Assert.That(_director.FocusEnemyId, Is.EqualTo(0));
            Step(20f, Vector3.zero, Report(0, pathDistance: 10f), Report(1, pathDistance: 1f));
            Assert.That(_director.FocusEnemyId, Is.EqualTo(1));
            Step(.1f, Vector3.zero, Report(0, awareness: EnemyAwarenessLevel.Engaged), Report(1));
            Assert.That(_director.FocusEnemyId, Is.EqualTo(0));
        }

        /// <summary>通常の待ち時間より先に距離超過のヒントを出す</summary>
        [Test] public void DistantFeaturedGetsHintAfterLeashTime()
        {
            _settings.ambientDuration = 1000f;
            _menace.leashTime = 2f;
            Assert.That(Step(1f, Vector3.right * 100f, Report())[0].Type, Is.EqualTo(DirectorDirectiveType.None));
            Assert.That(Step(1f, Vector3.right * 100f, Report())[0].Type, Is.EqualTo(DirectorDirectiveType.HintArea));
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.BuildUp));
        }

        /// <summary>見えない追跡者では発見演出が発生せず、視認後に一度だけ鳴る</summary>
        [Test] public void DiscoveryRequiresSeeingEngagedEnemy()
        {
            Step(.5f, Vector3.zero, Report(awareness: EnemyAwarenessLevel.Engaged, visible: false), Report(1));
            Assert.That(_director.IsPlayerFound.CurrentValue, Is.True);
            Assert.That(_sound.Count, Is.Zero);
            Step(.5f, Vector3.zero, Report(awareness: EnemyAwarenessLevel.Engaged));
            Step(6f, Vector3.zero, Report(awareness: EnemyAwarenessLevel.Engaged, visible: false));
            Step(.5f, Vector3.zero, Report(awareness: EnemyAwarenessLevel.Engaged));
            Assert.That(_director.IsEncounterRecognized.CurrentValue, Is.True);
            Assert.That(_sound.Count, Is.EqualTo(1));
        }

        /// <summary>敵が消えた場合に発見状態を残さない</summary>
        [Test] public void RemovedAndInactiveEnemiesDoNotRemainFound()
        {
            Step(.5f, Vector3.zero, Report(awareness: EnemyAwarenessLevel.Engaged));
            Step(.5f, Vector3.zero, Report(awareness: EnemyAwarenessLevel.Engaged, active: false));
            Assert.That(_director.IsPlayerFound.CurrentValue, Is.False);
            Assert.That(Step(.5f, Vector3.zero), Is.Empty);
            Assert.That(_director.FocusEnemyId, Is.EqualTo(-1));
        }

        /// <summary>追跡後だけ退避し、最低時間と不可視条件を満たすまで復帰しない</summary>
        [Test] public void MenaceRetreatWaitsForLostSightAndSafeEmergence()
        {
            _menace.engagedPerSecond = 2f;
            var directives = Step(1f, Vector3.zero, Report(awareness: EnemyAwarenessLevel.Engaged));
            Assert.That(_director.Menace, Is.EqualTo(1f));
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Hunt));
            Assert.That(directives[0].Type, Is.EqualTo(DirectorDirectiveType.None));
            directives = Step(.1f, Vector3.zero, Report(awareness: EnemyAwarenessLevel.Alerted));
            Assert.That(directives[0].Type, Is.EqualTo(DirectorDirectiveType.GoBackstage));
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Search));

            var hidden = Report(visible: false, backstage: true);
            Assert.That(Step(19f, Vector3.zero, hidden)[0].Type, Is.EqualTo(DirectorDirectiveType.None));
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Recovery));
            Assert.That(_sound.BackstageMoves, Is.EqualTo(1));
            _candidateVisible = true;
            Assert.That(Step(1f, Vector3.zero, hidden)[0].Type, Is.EqualTo(DirectorDirectiveType.None));
            _candidateVisible = false;
            Assert.That(Step(2f, Vector3.zero, hidden)[0].Type, Is.EqualTo(DirectorDirectiveType.EmergeAt));
        }

        /// <summary>捜索を観測するだけで音源やプレイヤーの位置を命令しない</summary>
        [Test] public void SearchIsObservedWithoutDirectingMovement()
        {
            var result = Step(.5f, Vector3.one * 100f, Report(awareness: EnemyAwarenessLevel.Alerted));
            Assert.That(result[0].Type, Is.EqualTo(DirectorDirectiveType.None));
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Search));
            Step(.5f, Vector3.zero, Report());
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Ambient));
        }

        /// <summary>停止時間では統括状態と通知を進めない</summary>
        [Test] public void PausedTimeDoesNotDiscoverPlayer()
        {
            Step(0f, Vector3.zero, Report(awareness: EnemyAwarenessLevel.Engaged));
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Ambient));
            Assert.That(_sound.Count, Is.Zero);
        }

        /// <summary>無効な設定値でもヒントは有限値になる</summary>
        [Test] public void InvalidSettingsUseDefaults()
        {
            _settings.ambientDuration = float.NaN;
            _settings.hintCellSize = float.PositiveInfinity;
            var hint = Step(12f, Vector3.right * 100f, Report())[0];
            Assert.That(hint.Type, Is.EqualTo(DirectorDirectiveType.HintArea));
            Assert.That(float.IsNaN(hint.Position.x) || float.IsInfinity(hint.Position.x), Is.False);
        }

        /// <summary>可視判定をあえて行わずDirectorの再検証を確認するProvider</summary>
        private sealed class PointStub : IEnemySearchPointProvider
        {
            /// <summary>固定の退出点を格納</summary>
            /// <param name="from">現在位置</param>
            /// <param name="buffer">格納先</param>
            /// <returns>一点</returns>
            public int GetExitPoints(Vector3 from, Span<Vector3> buffer) { buffer[0] = Vector3.left * 10f; return 1; }
            /// <summary>捜索候補を格納</summary>
            /// <param name="center">中心</param>
            /// <param name="radius">範囲</param>
            /// <param name="sideRooms">小部屋を含むか</param>
            /// <param name="buffer">格納先</param>
            /// <returns>一点</returns>
            public int GetSearchPoints(Vector3 center, float radius, bool sideRooms, Span<Vector3> buffer)
                => GetExitPoints(center, buffer);
            /// <summary>視認判定を無視して固定候補を返す</summary>
            /// <param name="player">プレイヤー位置</param>
            /// <param name="min">最小距離</param>
            /// <param name="max">最大距離</param>
            /// <param name="upper">上階を含むか</param>
            /// <param name="visible">視認判定</param>
            /// <param name="point">候補</param>
            /// <returns>true</returns>
            public bool TryFindEmergePoint(Vector3 player, float min, float max, bool upper,
                Func<Vector3, bool> visible, out Vector3 point) { point = Vector3.right * 20f; return true; }
        }

        private sealed class SoundSpy : IEnemyEncounterFeedback
        {
            public int Count;
            public int BackstageMoves;
            /// <summary>舞台裏音の通知を数える</summary>
            /// <param name="position">音源位置</param>
            public void OnEnemyBackstageMove(Vector3 position) => BackstageMoves++;
            /// <summary>出現通知を受け取る</summary>
            /// <param name="position">出現位置</param>
            public void OnEnemyEmerge(Vector3 position) { }
            /// <summary>演出回数を記録</summary>
            public void OnPlayerFound() => Count++;
        }
    }
}
