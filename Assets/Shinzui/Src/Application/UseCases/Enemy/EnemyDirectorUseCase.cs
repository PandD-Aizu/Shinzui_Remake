using System;
using System.Collections.Generic;
using R3;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces;
using UnityEngine;

namespace Shinzui.Application.UseCases.Enemy
{
    /// <summary>
    /// Paces encounters without granting enemies omniscience. Only confirmed sight permits chase;
    /// sound and director hints permit investigation. Commands are in the same order as reports.
    /// </summary>
    public sealed class EnemyDirectorUseCase : IDisposable
    {
        private readonly IEnemyEncounterFeedback _feedback;
        private readonly EnemyDirectorSettings _settings;
        private readonly ReactiveProperty<bool> _isPlayerFound = new(false);
        private readonly Dictionary<int, float> _sightTimers = new();
        private readonly List<int> _staleIds = new();
        private EnemyCommand[] _commands = Array.Empty<EnemyCommand>();
        private float _phaseTime;
        private float _hintTime;
        private float _searchTime;
        private Vector3 _lastKnownPosition;
        private Vector3 _investigationTarget;
        private bool _hasClue;
        private int _hintSequence;
        private float _discoveryCooldown;

        public ReadOnlyReactiveProperty<bool> IsPlayerFound => _isPlayerFound;
        public EnemyDirectorPhase Phase { get; private set; }
        public float Pressure { get; private set; }
        public int FocusEnemyId { get; private set; } = -1;

        public EnemyDirectorUseCase(IEnemyEncounterFeedback feedback, EnemyDirectorSettings settings)
        {
            _feedback = feedback;
            _settings = settings ?? new EnemyDirectorSettings();
        }

        public EnemyCommand[] DecideCommands(EnemyWorldState world, float deltaTime)
        {
            ReadOnlySpan<EnemyReport> reports = world.EnemyReports.Span;
            if (_commands.Length != reports.Length) _commands = new EnemyCommand[reports.Length];
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return _commands;
            _phaseTime += deltaTime;
            _hintTime -= deltaTime;
            _discoveryCooldown -= deltaTime;
            _staleIds.Clear();
            foreach (int id in _sightTimers.Keys) _staleIds.Add(id);

            int focused = -1;
            int visible = -1;
            int heard = -1;
            float nearestVisible = float.MaxValue;
            float nearestHeard = float.MaxValue;
            float nearest = float.MaxValue;
            for (int i = 0; i < reports.Length; i++)
            {
                EnemyReport report = reports[i];
                _commands[i] = report.CanReceiveCommand ? EnemyCommand.Wander : EnemyCommand.Idle;
                _staleIds.Remove(report.Id);
                _sightTimers.TryGetValue(report.Id, out float seenFor);
                seenFor = report.CanReceiveCommand && report.CanSeePlayer ? seenFor + deltaTime : 0f;
                _sightTimers[report.Id] = seenFor;
                if (!report.CanReceiveCommand) continue;
                float distance = Vector3.Distance(report.Position, world.PlayerPosition);
                nearest = Mathf.Min(nearest, distance);
                if (report.Id == FocusEnemyId) focused = i;
                if (seenFor >= Positive(_settings.detectionTime, .3f) && distance < nearestVisible)
                {
                    visible = i;
                    nearestVisible = distance;
                }
                if (world.PlayerNoiseRadius > 0f && distance <= world.PlayerNoiseRadius && distance < nearestHeard)
                {
                    heard = i;
                    nearestHeard = distance;
                }
            }
            foreach (int id in _staleIds) _sightTimers.Remove(id);

            // Keep a confirmed observer selected to prevent frame-by-frame ownership oscillation.
            if (focused >= 0 && reports[focused].CanSeePlayer &&
                _sightTimers[FocusEnemyId] >= Positive(_settings.detectionTime, .3f)) visible = focused;
            bool found = visible >= 0;
            if (found && !_isPlayerFound.CurrentValue && _discoveryCooldown <= 0f)
            {
                _feedback?.OnPlayerFound();
                _discoveryCooldown = 5f;
            }
            _isPlayerFound.Value = found;
            float pressureRate = found ? Positive(_settings.chasePressurePerSecond, .075f)
                : Phase != EnemyDirectorPhase.Recovery && nearest < Positive(_settings.pressureDistance, 18f)
                    ? Positive(_settings.proximityPressurePerSecond, .025f)
                    : -Positive(_settings.pressureDecayPerSecond, .06f);
            Pressure = Mathf.Clamp01(Pressure + pressureRate * deltaTime);

            if (found)
            {
                FocusEnemyId = reports[visible].Id;
                _lastKnownPosition = world.PlayerPosition;
                _hasClue = true;
                _searchTime = 0f;
                SetPhase(EnemyDirectorPhase.Hunt);
                _commands[visible] = EnemyCommand.ChasePlayer(_lastKnownPosition);
                KeepOthersAway(world, reports, visible);
                return _commands;
            }

            if (focused < 0 && FocusEnemyId >= 0)
            {
                FocusEnemyId = -1;
                _hasClue = false;
                if (Phase == EnemyDirectorPhase.Hunt || Phase == EnemyDirectorPhase.Search) BeginRecovery();
            }
            if (Phase == EnemyDirectorPhase.Hunt)
            {
                SetPhase(EnemyDirectorPhase.Search);
                _searchTime = 0f;
                _hintTime = 0f;
            }
            // After a sustained encounter, relief starts only once visual contact is broken.
            if (Pressure >= 1f && Phase != EnemyDirectorPhase.Recovery) BeginRecovery();

            if (Phase == EnemyDirectorPhase.Recovery)
            {
                KeepOthersAway(world, reports, -1);
                if (_phaseTime >= Positive(_settings.recoveryDuration, 18f) && Pressure <= .35f)
                    SetPhase(EnemyDirectorPhase.Ambient);
                return _commands;
            }

            if (heard >= 0 && Phase != EnemyDirectorPhase.Search)
            {
                FocusEnemyId = reports[heard].Id;
                focused = heard;
                _lastKnownPosition = world.PlayerPosition; // A sound event is a clue, never a chase target.
                _hasClue = true;
                _searchTime = 0f;
                _hintTime = 0f;
                SetPhase(EnemyDirectorPhase.Search);
            }
            if (Phase == EnemyDirectorPhase.Search && focused >= 0 && _hasClue)
            {
                if (heard == focused)
                {
                    _lastKnownPosition = world.PlayerPosition;
                    _investigationTarget = _lastKnownPosition;
                    _hintTime = Positive(_settings.searchPointInterval, 3f);
                }
                _searchTime += deltaTime;
                if (_searchTime >= Positive(_settings.searchDuration, 14f))
                {
                    BeginRecovery();
                    KeepOthersAway(world, reports, -1);
                    return _commands;
                }
                if (_hintTime <= 0f)
                {
                    int step = (int)(_searchTime / Positive(_settings.searchPointInterval, 3f));
                    float angle = step * 2.399963f;
                    float radius = step == 0 ? 0f : Mathf.Min(step * 2f, Positive(_settings.searchRadius, 7f));
                    _investigationTarget = _lastKnownPosition + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    _hintTime = Positive(_settings.searchPointInterval, 3f);
                }
                _commands[focused] = EnemyCommand.InvestigatePosition(_investigationTarget);
                KeepOthersAway(world, reports, focused);
                return _commands;
            }

            if (Phase == EnemyDirectorPhase.Ambient && _phaseTime >= Positive(_settings.ambientDuration, 12f))
            {
                SetPhase(EnemyDirectorPhase.BuildUp);
                _hintTime = 0f;
            }
            if (Phase == EnemyDirectorPhase.BuildUp)
            {
                if (_phaseTime >= Positive(_settings.buildUpDuration, 35f))
                {
                    BeginRecovery();
                    KeepOthersAway(world, reports, -1);
                    return _commands;
                }
                if (focused < 0)
                {
                    float best = float.MaxValue;
                    for (int i = 0; i < reports.Length; i++)
                    {
                        float distance = (reports[i].Position - world.PlayerPosition).sqrMagnitude;
                        if (!reports[i].CanReceiveCommand || distance >= best) continue;
                        focused = i;
                        best = distance;
                    }
                    if (focused >= 0) FocusEnemyId = reports[focused].Id;
                }
                if (focused >= 0)
                {
                    if (_hintTime <= 0f)
                    {
                        float cell = Positive(_settings.hintCellSize, 12f);
                        Vector3 p = world.PlayerPosition;
                        float angle = ++_hintSequence * 2.399963f;
                        _investigationTarget = new Vector3(Mathf.Floor(p.x / cell) * cell + cell * .5f, p.y,
                            Mathf.Floor(p.z / cell) * cell + cell * .5f)
                            + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * cell * .5f;
                        _hintTime = Positive(_settings.hintInterval, 8f);
                    }
                    _commands[focused] = EnemyCommand.InvestigatePosition(_investigationTarget);
                    KeepOthersAway(world, reports, focused);
                }
            }
            return _commands;
        }

        private void KeepOthersAway(EnemyWorldState world, ReadOnlySpan<EnemyReport> reports, int activeIndex)
        {
            float distance = Positive(_settings.retreatDistance, 18f);
            for (int i = 0; i < reports.Length; i++)
            {
                if (i == activeIndex || !reports[i].CanReceiveCommand) continue;
                Vector3 away = reports[i].Position - world.PlayerPosition;
                away.y = 0f;
                if (away.sqrMagnitude >= distance * distance) continue;
                if (away.sqrMagnitude < .01f) away = (i % 2 == 0) ? Vector3.forward : Vector3.back;
                // Hold this target until reached; do not continually steer using hidden player movement.
                _commands[i] = reports[i].CurrentCommand.Type == EnemyCommandType.Retreat
                    ? reports[i].CurrentCommand
                    : EnemyCommand.Retreat(reports[i].Position + away.normalized * distance);
            }
        }

        private void BeginRecovery()
        {
            SetPhase(EnemyDirectorPhase.Recovery);
            FocusEnemyId = -1;
            _hasClue = false;
        }

        private void SetPhase(EnemyDirectorPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            _phaseTime = 0f;
        }

        private static float Positive(float value, float fallback) => EnemyDirectorSettings.Positive(value, fallback);
        public void Dispose() => _isPlayerFound.Dispose();
    }
}
