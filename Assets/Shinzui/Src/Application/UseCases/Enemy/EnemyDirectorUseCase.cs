using System;
using R3;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Application.UseCases.Enemy
{
    /// <summary>遭遇のペースとぼかしたヒントを管理し、追跡判断は各Brainへ任せる</summary>
    public sealed class EnemyDirectorUseCase : IDisposable
    {
        private readonly IEnemyEncounterFeedback _feedback;
        private readonly EnemyDirectorSettings _settings;
        private readonly ReactiveProperty<bool> _isPlayerFound = new(false);
        private readonly ReactiveProperty<bool> _isEncounterRecognized = new(false);
        private DirectorDirective[] _directives = Array.Empty<DirectorDirective>();
        private float _phaseTime;
        private float _hintTime;
        private int _hintSequence;
        private Vector3 _hintPosition;
        private float _discoveryCooldown;

        public ReadOnlyReactiveProperty<bool> IsPlayerFound => _isPlayerFound;
        public ReadOnlyReactiveProperty<bool> IsEncounterRecognized => _isEncounterRecognized;
        public EnemyDirectorPhase Phase { get; private set; }
        public float Pressure { get; private set; }
        public int FocusEnemyId { get; private set; } = -1;

        /// <summary>演出通知と統括設定を保持</summary>
        /// <param name="feedback">発見演出</param>
        /// <param name="settings">統括設定</param>
        public EnemyDirectorUseCase(IEnemyEncounterFeedback feedback, EnemyDirectorSettings settings)
        { _feedback = feedback; _settings = settings ?? new EnemyDirectorSettings(); }

        /// <summary>観測状態をもとに主役だけへ曖昧なヒントを渡す</summary>
        /// <param name="world">全知の世界状態</param>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>報告順に対応する指示</returns>
        public DirectorDirective[] DecideDirectives(EnemyWorldState world, float deltaTime)
        {
            ReadOnlySpan<EnemyReport> reports = world.EnemyReports.Span;
            if (_directives.Length != reports.Length) _directives = new DirectorDirective[reports.Length];
            Array.Clear(_directives, 0, _directives.Length);
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return _directives;
            _phaseTime += deltaTime;
            _hintTime -= deltaTime;
            _discoveryCooldown -= deltaTime;
            ObserveReports(world.EnemyReports);

            // 継続中の主役を優先し、いなければ最寄りの行動可能な敵を選ぶ
            int focused = -1;
            int nearestIndex = -1;
            float nearest = float.MaxValue;
            for (int i = 0; i < reports.Length; i++)
            {
                EnemyReport report = reports[i];
                if (!report.CanReceiveCommand || report.IsBackstage) continue;
                float distance = Vector3.Distance(report.Position, world.PlayerPosition);
                if (distance < nearest) { nearest = distance; nearestIndex = i; }
                if (report.Id == FocusEnemyId) focused = i;
            }
            if (focused < 0) focused = nearestIndex;
            FocusEnemyId = focused >= 0 ? reports[focused].Id : -1;

            // Phase 3のMenace移行までは既存のPressureを維持する
            float rate = _isPlayerFound.CurrentValue ? Positive(_settings.chasePressurePerSecond, .075f)
                : Phase != EnemyDirectorPhase.Recovery && nearest < Positive(_settings.pressureDistance, 18f)
                    ? Positive(_settings.proximityPressurePerSecond, .025f)
                    : -Positive(_settings.pressureDecayPerSecond, .06f);
            Pressure = Mathf.Clamp01(Pressure + rate * deltaTime);
            if (_isPlayerFound.CurrentValue) return _directives;
            if (Pressure >= 1f && Phase != EnemyDirectorPhase.Recovery) SetPhase(EnemyDirectorPhase.Recovery);
            if (Phase == EnemyDirectorPhase.Recovery)
            {
                if (_phaseTime >= Positive(_settings.recoveryDuration, 18f) && Pressure <= .35f)
                    SetPhase(EnemyDirectorPhase.Ambient);
                return _directives;
            }
            if (Phase == EnemyDirectorPhase.Search) return _directives;
            if (Phase == EnemyDirectorPhase.Ambient && _phaseTime >= Positive(_settings.ambientDuration, 12f))
                SetPhase(EnemyDirectorPhase.BuildUp);
            if (Phase != EnemyDirectorPhase.BuildUp) return _directives;
            if (_phaseTime >= Positive(_settings.buildUpDuration, 35f))
            { SetPhase(EnemyDirectorPhase.Recovery); return _directives; }

            // プレイヤー位置はセルとオフセットに変換し、同一座標のヒントを渡さない
            if (focused >= 0)
            {
                float cell = Positive(_settings.hintCellSize, 12f);
                if (_hintTime <= 0f)
                {
                    Vector3 p = world.PlayerPosition;
                    float angle = ++_hintSequence * 2.399963f;
                    _hintPosition = new Vector3(Mathf.Floor(p.x / cell) * cell + cell * .5f, p.y,
                        Mathf.Floor(p.z / cell) * cell + cell * .5f)
                        + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * cell * .5f;
                    if ((_hintPosition - p).sqrMagnitude < .01f) _hintPosition += Vector3.right * cell * .25f;
                    _hintTime = Positive(_settings.hintInterval, 8f);
                }
                _directives[focused] = new DirectorDirective(DirectorDirectiveType.HintArea, _hintPosition, cell * .5f);
            }
            return _directives;
        }

        /// <summary>同じフレームのBrain結果から発見演出を更新</summary>
        /// <param name="reports">敵自身が決めた警戒状態</param>
        public void ObserveReports(ReadOnlyMemory<EnemyReport> reports)
        {
            bool found = false;
            bool recognized = false;
            bool searching = false;
            foreach (EnemyReport report in reports.Span)
            {
                if (!report.CanReceiveCommand || report.IsBackstage) continue;
                searching |= report.Awareness == EnemyAwarenessLevel.Alerted;
                if (!report.IsEngaged) continue;
                found = true;
                recognized |= report.IsVisibleToPlayer;
            }
            recognized = found && (recognized || _isEncounterRecognized.CurrentValue);
            if (recognized && !_isEncounterRecognized.CurrentValue && _discoveryCooldown <= 0f)
            { _feedback?.OnPlayerFound(); _discoveryCooldown = 5f; }
            _isPlayerFound.Value = found;
            _isEncounterRecognized.Value = recognized;

            // HuntとSearchは命令せず観測し、休息中の音はヒント再開の理由にしない
            if (found) SetPhase(EnemyDirectorPhase.Hunt);
            else if (searching && Phase != EnemyDirectorPhase.Recovery) SetPhase(EnemyDirectorPhase.Search);
            else if (Phase == EnemyDirectorPhase.Hunt || Phase == EnemyDirectorPhase.Search)
                SetPhase(EnemyDirectorPhase.Recovery);
        }

        /// <summary>フェーズが変わった時だけ経過時間を初期化</summary>
        /// <param name="phase">移行先</param>
        private void SetPhase(EnemyDirectorPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            _phaseTime = 0f;
            _hintTime = 0f;
        }

        /// <summary>設定値を正規化</summary>
        /// <param name="value">設定値</param>
        /// <param name="fallback">既定値</param>
        /// <returns>有効な正値</returns>
        private static float Positive(float value, float fallback) => EnemyDirectorSettings.Positive(value, fallback);

        /// <summary>発見状態と演出状態を解放</summary>
        public void Dispose() { _isPlayerFound.Dispose(); _isEncounterRecognized.Dispose(); }
    }
}
