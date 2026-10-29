using NUnit.Framework;
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

        /// <summary>各テストで統括状態を独立させる</summary>
        [SetUp] public void SetUp()
        {
            _settings = new EnemyDirectorSettings();
            _sound = new SoundSpy();
            _director = new EnemyDirectorUseCase(_sound, _settings);
        }
        /// <summary>通知を解放</summary>
        [TearDown] public void TearDown() => _director.Dispose();

        /// <summary>敵の観測状態を作成</summary>
        /// <param name="id">識別子</param>
        /// <param name="awareness">警戒段階</param>
        /// <param name="visible">プレイヤーに見えるか</param>
        /// <param name="active">動作可能か</param>
        /// <returns>報告</returns>
        private static EnemyReport Report(int id = 0, EnemyAwarenessLevel awareness = EnemyAwarenessLevel.Unaware,
            bool visible = true, bool active = true)
            => new(id, Vector3.forward * (id + 5f), default, active, visible, awareness);

        /// <summary>統括時間を進める</summary>
        /// <param name="dt">経過秒数</param>
        /// <param name="position">プレイヤーの位置</param>
        /// <param name="reports">敵の報告</param>
        /// <returns>指示</returns>
        private DirectorDirective[] Step(float dt, Vector3 position, params EnemyReport[] reports)
            => _director.DecideDirectives(new EnemyWorldState(position, reports, false, default, default), dt);

        /// <summary>近いだけでは発見も直接追跡命令も生まれない</summary>
        [Test] public void ProximityOnlyProducesHints()
        {
            for (int i = 0; i < 30; i++)
            {
                var type = Step(.5f, Vector3.zero, Report())[0].Type;
                Assert.That(type == DirectorDirectiveType.None || type == DirectorDirectiveType.HintArea, Is.True);
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

        /// <summary>Pressure上限でも追跡はBrainに任せ、喪失後だけ休息へ移る</summary>
        [Test] public void PressureDoesNotOverrideEngagement()
        {
            _settings.chasePressurePerSecond = 2f;
            var directives = Step(1f, Vector3.zero, Report(awareness: EnemyAwarenessLevel.Engaged));
            Assert.That(_director.Pressure, Is.EqualTo(1f));
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Hunt));
            Assert.That(directives[0].Type, Is.EqualTo(DirectorDirectiveType.None));
            Step(.1f, Vector3.zero, Report(awareness: EnemyAwarenessLevel.Alerted));
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Recovery));
        }

        /// <summary>捜索を観測するだけで音源やプレイヤーの位置を命令しない</summary>
        [Test] public void SearchIsObservedWithoutDirectingMovement()
        {
            var result = Step(.5f, Vector3.one * 100f, Report(awareness: EnemyAwarenessLevel.Alerted));
            Assert.That(result[0].Type, Is.EqualTo(DirectorDirectiveType.None));
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Search));
            Step(.5f, Vector3.zero, Report());
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Recovery));
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
            var hint = Step(12f, Vector3.zero, Report())[0];
            Assert.That(hint.Type, Is.EqualTo(DirectorDirectiveType.HintArea));
            Assert.That(float.IsNaN(hint.Position.x) || float.IsInfinity(hint.Position.x), Is.False);
        }

        private sealed class SoundSpy : IEnemyEncounterFeedback
        {
            public int Count;
            /// <summary>演出回数を記録</summary>
            public void OnPlayerFound() => Count++;
        }
    }
}
