using System;
using System.Linq;
using NUnit.Framework;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases.Enemy;
using UnityEngine;

namespace Shinzui.Tests.EnemyDirector
{
    public sealed class DirectorTests
    {
        private EnemyDirectorSettings _settings;
        private EnemyDirectorUseCase _director;
        private SoundSpy _sound;
        [SetUp] public void SetUp()
        {
            _settings = new EnemyDirectorSettings();
            _sound = new SoundSpy();
            _director = new EnemyDirectorUseCase(_sound, _settings);
        }
        [TearDown] public void TearDown() => _director.Dispose();
        private static EnemyReport Report(int id = 0, bool visible = false, bool active = true, float z = 5f,
            EnemyCommand command = default) => new(id, new Vector3(0, 0, z), command, 16f, active, visible);
        private EnemyCommand[] Step(float dt, Vector3 player, params EnemyReport[] reports)
            => _director.DecideCommands(new EnemyWorldState(player, reports, false, default, default), dt);

        [Test] public void ProximityAloneNeverStartsChase()
        {
            for (int i = 0; i < 100; i++)
                Assert.That(Step(.5f, Vector3.zero, Report())[0].Type, Is.Not.EqualTo(EnemyCommandType.ChasePlayer));
            Assert.That(_director.IsPlayerFound.CurrentValue, Is.False);
            Assert.That(_sound.OneShots, Is.Zero);
        }
        [Test] public void SightRequiresContinuousConfirmation()
        {
            Assert.That(Step(.1f, Vector3.zero, Report(visible: true))[0].Type, Is.Not.EqualTo(EnemyCommandType.ChasePlayer));
            Step(.1f, Vector3.zero, Report());
            Assert.That(Step(.2f, Vector3.zero, Report(visible: true))[0].Type, Is.Not.EqualTo(EnemyCommandType.ChasePlayer));
            Assert.That(Step(.2f, Vector3.zero, Report(visible: true))[0].Type, Is.EqualTo(EnemyCommandType.ChasePlayer));
        }
        [Test] public void LostSightSearchesLastKnownPositionNotMovingPlayer()
        {
            Vector3 seen = new(2, 0, 3);
            Step(.5f, seen, Report(visible: true));
            var command = Step(.5f, new Vector3(80, 0, 70), Report())[0];
            Assert.That(command.Type, Is.EqualTo(EnemyCommandType.InvestigatePosition));
            Assert.That(command.TargetPosition, Is.EqualTo(seen));
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Search));
            Assert.That(_director.IsPlayerFound.CurrentValue, Is.False);
        }
        [Test] public void SearchExpiresAndRecoverySuppressesHints()
        {
            _settings.searchDuration = 1f;
            Step(.5f, Vector3.zero, Report(visible: true));
            Step(.5f, Vector3.zero, Report());
            var result = Step(.5f, Vector3.zero, Report())[0];
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Recovery));
            Assert.That(result.Type, Is.EqualTo(EnemyCommandType.Retreat));
            for (int i = 0; i < 10; i++)
                Assert.That(Step(.5f, Vector3.zero, Report())[0].Type, Is.EqualTo(EnemyCommandType.Retreat));
        }
        [Test] public void RecoveryStillAllowsGenuineVisualDetection()
        {
            _settings.buildUpDuration = .5f;
            _settings.ambientDuration = .5f;
            Step(.5f, Vector3.zero, Report());
            Step(.5f, Vector3.zero, Report());
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Recovery));
            Assert.That(Step(.5f, Vector3.zero, Report(visible: true))[0].Type, Is.EqualTo(EnemyCommandType.ChasePlayer));
        }
        [Test] public void PressureDoesNotMakeEnemyForgetVisiblePlayer()
        {
            _settings.chasePressurePerSecond = 2f;
            for (int i = 0; i < 8; i++)
                Assert.That(Step(.5f, Vector3.zero, Report(visible: true))[0].Type, Is.EqualTo(EnemyCommandType.ChasePlayer));
            Assert.That(_director.Pressure, Is.EqualTo(1f));
            Step(.5f, Vector3.zero, Report());
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Recovery));
        }
        [Test] public void OnlyOneHunterAndOwnershipRemainsStable()
        {
            Step(.5f, Vector3.zero, Report(10, true, z: 5), Report(42, true, z: 7));
            var commands = Step(.5f, Vector3.zero, Report(42, true, z: 2), Report(10, true, z: 5));
            Assert.That(commands.Count(c => c.Type == EnemyCommandType.ChasePlayer), Is.EqualTo(1));
            Assert.That(_director.FocusEnemyId, Is.EqualTo(10));
            Assert.That(commands[1].Type, Is.EqualTo(EnemyCommandType.ChasePlayer));
        }
        [Test] public void InactiveEnemyCannotDetectOrOwnChase()
        {
            var commands = Step(.5f, Vector3.zero, Report(0, true, false, 1), Report(1, true));
            Assert.That(commands[0].Type, Is.EqualTo(EnemyCommandType.Idle));
            Assert.That(_director.FocusEnemyId, Is.EqualTo(1));
        }
        [Test] public void RemovedHunterDoesNotLeaveStalePlayerFoundState()
        {
            Step(.5f, Vector3.zero, Report(visible: true));
            Assert.That(Step(.5f, Vector3.zero), Is.Empty);
            Assert.That(_director.FocusEnemyId, Is.EqualTo(-1));
            Assert.That(_director.IsPlayerFound.CurrentValue, Is.False);
        }
        [Test] public void SoundInvestigatesOnlyWithinAudibleRadius()
        {
            var reports = new[] { Report(0, z: 3f), Report(1, z: 30f) };
            var commands = _director.DecideCommands(new EnemyWorldState(Vector3.zero, reports, false, default, default, 4f), .5f);
            Assert.That(commands[0].Type, Is.EqualTo(EnemyCommandType.InvestigatePosition));
            Assert.That(commands[1].Type, Is.EqualTo(EnemyCommandType.Wander));
            Assert.That(_director.IsPlayerFound.CurrentValue, Is.False);
        }
        [Test] public void ContinuousNoiseDoesNotResetSearchBudget()
        {
            _settings.searchDuration = 2f;
            var world = new EnemyWorldState(Vector3.zero, new[] { Report() }, false, default, default, 10f);
            for (int i = 0; i < 4; i++) _director.DecideCommands(world, .5f);
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Recovery));
        }
        [Test] public void HintsAreImpreciseAndHeldBetweenUpdates()
        {
            _settings.ambientDuration = .5f;
            Vector3 position = new(1, 0, 1);
            Vector3 hint = Step(.5f, position, Report())[0].TargetPosition;
            Assert.That(hint, Is.Not.EqualTo(position));
            Assert.That(Step(.5f, new Vector3(100, 0, 100), Report())[0].TargetPosition, Is.EqualTo(hint));
        }
        [Test] public void PausedTimeDoesNotAdvancePacingOrDiscoverPlayer()
        {
            Step(0f, Vector3.zero, Report(visible: true));
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.Ambient));
            Assert.That(_sound.OneShots, Is.Zero);
        }
        [Test] public void DiscoverySoundIsNotRepeatedDuringFlickeringSight()
        {
            Step(.5f, Vector3.zero, Report(visible: true));
            Step(.1f, Vector3.zero, Report());
            Step(.5f, Vector3.zero, Report(visible: true));
            Assert.That(_sound.OneShots, Is.EqualTo(1));
        }
        [Test] public void InvalidDurationsUseFiniteDefaults()
        {
            _settings.ambientDuration = float.NaN;
            _settings.hintCellSize = float.PositiveInfinity;
            for (int i = 0; i < 26; i++) Step(.5f, Vector3.zero, Report());
            var command = Step(.5f, Vector3.zero, Report())[0];
            Assert.That(_director.Phase, Is.EqualTo(EnemyDirectorPhase.BuildUp));
            Assert.That(float.IsNaN(command.TargetPosition.x), Is.False);
        }

        private sealed class SoundSpy : IEnemyEncounterFeedback
        {
            public int OneShots;
            public void OnPlayerFound() => OneShots++;
        }
    }
}
