using System;
using System.Collections.Generic;
using Shinzui.Application.Interfaces.Enemy;
using Shinzui.Domain.Entities.Enemy;
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
        private readonly MenaceSettings _menaceSettings;
        private readonly IEnemySearchPointProvider _points;
        private readonly MenaceGauge _menace = new();
        private readonly Dictionary<int, BackstageState> _backstage = new();
        private readonly List<int> _staleIds = new();
        private float _focusTime;
        private float _leashTime;
        private bool _reliefRequested;
        private uint _random = 731u;

        /// <summary>敵ごとの退避要求と抽象移動状態</summary>
        private sealed class BackstageState
        {
            public bool Pending;
            public bool WasHidden;
            public bool HasExit;
            public Vector3 Exit;
            public Vector3 AbstractPosition;
            public float Retry;
            public float ExitAge;
            public float SoundTime;
            public int Attempts;
        }
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
        public float Menace => _menace.Value;
        public int FocusEnemyId { get; private set; } = -1;

        /// <summary>演出通知と統括設定を保持</summary>
        /// <param name="feedback">発見演出</param>
        /// <param name="settings">統括設定</param>
        /// <param name="menaceSettings">緊張度と退避設定</param>
        /// <param name="points">地形からの候補取得</param>
        public EnemyDirectorUseCase(IEnemyEncounterFeedback feedback, EnemyDirectorSettings settings,
            MenaceSettings menaceSettings, IEnemySearchPointProvider points)
        {
            _feedback = feedback;
            _settings = settings;
            _menaceSettings = menaceSettings;
            _points = points;
        }

        /// <summary>主役、緊張度、舞台裏の出入りとヒントを更新</summary>
        /// <param name="world">全知の世界状態</param>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>報告順の指示</returns>
        public DirectorDirective[] DecideDirectives(EnemyWorldState world, float deltaTime)
        {
            ReadOnlySpan<EnemyReport> reports = world.EnemyReports.Span;
            if (_directives.Length != reports.Length) _directives = new DirectorDirective[reports.Length];
            Array.Clear(_directives, 0, _directives.Length);
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return _directives;
            _phaseTime += deltaTime;
            _hintTime -= deltaTime;
            _focusTime += deltaTime;
            _discoveryCooldown -= deltaTime;
            SynchronizeBackstage(reports);
            int focused = SelectFeatured(reports);
            ObserveReports(world.EnemyReports);

            // 主役との遭遇が続くほど緊張を加算し、舞台裏では速く下げる
            _menace.Tick(MenaceRate(focused >= 0 ? reports[focused] : default, focused >= 0, world.PlayerPosition), deltaTime);
            if (_menace.Value >= 1f) _reliefRequested = true;
            UpdateHint(world.PlayerPosition);
            for (int i = 0; i < reports.Length; i++)
            {
                EnemyReport report = reports[i];
                BackstageState state = _backstage[report.Id];
                state.Retry -= deltaTime;
                if (report.IsBackstage)
                {
                    UpdateHidden(world, report, state, i, deltaTime);
                    continue;
                }
                if (_reliefRequested && report.CanReceiveCommand && !report.IsEngaged
                    && (i == focused || Vector3.Distance(report.Position, world.PlayerPosition)
                        <= Positive(_menaceSettings.otherRetreatDistance, 20f))) state.Pending = true;
                if (state.Pending && report.CanReceiveCommand && !report.IsEngaged)
                    UpdateExit(world, report, state, i, deltaTime);
            }

            // 主役が遠いままなら、捜索の観測中でもヒント巡回へ誘導する
            if (focused >= 0 && !reports[focused].IsBackstage)
            {
                EnemyReport featured = reports[focused];
                bool far = Vector3.Distance(featured.Position, world.PlayerPosition) > Positive(_menaceSettings.leashDistance, 35f);
                _leashTime = far && !featured.IsEngaged ? _leashTime + deltaTime : 0f;
                if (_leashTime >= Positive(_menaceSettings.leashTime, 25f)
                    || (Phase == EnemyDirectorPhase.Ambient && _phaseTime >= Positive(_settings.ambientDuration, 12f)))
                    SetPhase(EnemyDirectorPhase.BuildUp);
                if (Phase == EnemyDirectorPhase.BuildUp && _directives[focused].Type == DirectorDirectiveType.None)
                    _directives[focused] = new DirectorDirective(DirectorDirectiveType.HintArea,
                        _hintPosition, Positive(_settings.hintCellSize, 12f) * .5f);
            }
            return _directives;
        }

        /// <summary>実際の退避を確認してから最低滞在時間を開始</summary>
        /// <param name="reports">現在の敵一覧</param>
        private void SynchronizeBackstage(ReadOnlySpan<EnemyReport> reports)
        {
            _staleIds.Clear();
            foreach (int id in _backstage.Keys) _staleIds.Add(id);
            bool occupied = false;
            foreach (EnemyReport report in reports)
            {
                _staleIds.Remove(report.Id);
                if (!_backstage.TryGetValue(report.Id, out BackstageState state))
                { state = new BackstageState(); _backstage.Add(report.Id, state); }
                if (report.IsBackstage && !state.WasHidden)
                {
                    _random = _random * 1664525u + 1013904223u;
                    float minimum = Positive(_menaceSettings.backstageMinDuration, 20f);
                    float maximum = Mathf.Max(minimum, Positive(_menaceSettings.backstageMaxDuration, 35f));
                    _menace.BeginBackstage(Mathf.Lerp(minimum, maximum, (_random & 65535u) / 65535f));
                    state.AbstractPosition = report.Position;
                    state.SoundTime = Positive(_menaceSettings.backstageSoundInterval, 4f);
                    state.Pending = state.HasExit = false;
                    state.Retry = 0f;
                    state.Attempts = 0;
                }
                state.WasHidden = report.IsBackstage;
                occupied |= report.IsBackstage || state.Pending;
            }
            foreach (int id in _staleIds) _backstage.Remove(id);
            if (!occupied && _menace.IsRecovering)
            { _menace.EndBackstage(); _reliefRequested = false; }
        }

        /// <summary>追跡者を優先し、それ以外は保持時間付きで経路が最短の敵を選ぶ</summary>
        /// <param name="reports">敵の報告</param>
        /// <returns>主役の報告添字</returns>
        private int SelectFeatured(ReadOnlySpan<EnemyReport> reports)
        {
            int current = -1, nearest = -1, engaged = -1;
            float distance = float.MaxValue;
            for (int i = 0; i < reports.Length; i++)
            {
                EnemyReport report = reports[i];
                if (report.Id == FocusEnemyId && (report.CanReceiveCommand || report.IsBackstage)) current = i;
                if (!report.CanReceiveCommand || report.IsBackstage) continue;
                if (report.IsEngaged && (engaged < 0 || report.Id == FocusEnemyId)) engaged = i;
                if (report.PathDistanceToPlayer < distance) { nearest = i; distance = report.PathDistanceToPlayer; }
            }
            int selected = engaged >= 0 ? engaged
                : current >= 0 && (reports[current].IsBackstage || _focusTime < Positive(_menaceSettings.featuredHoldTime, 20f))
                    ? current : nearest;
            int id = selected >= 0 ? reports[selected].Id : -1;
            if (id != FocusEnemyId) { FocusEnemyId = id; _focusTime = _leashTime = 0f; }
            return selected;
        }

        /// <summary>主役の距離と視認状態から毎秒の緊張変化を計算</summary>
        /// <param name="report">主役の報告</param>
        /// <param name="exists">主役がいるか</param>
        /// <param name="player">プレイヤー位置</param>
        /// <returns>毎秒の増減</returns>
        private float MenaceRate(EnemyReport report, bool exists, Vector3 player)
        {
            if (exists && report.IsBackstage) return -Positive(_menaceSettings.backstageDecayPerSecond, .12f);
            float rate = 0f;
            if (exists)
            {
                float distance = Vector3.Distance(report.Position, player);
                if (report.IsEngaged) rate += Positive(_menaceSettings.engagedPerSecond, .1f);
                if (report.IsVisibleToPlayer && distance <= Positive(_menaceSettings.visibleDistance, 12f))
                    rate += Positive(_menaceSettings.visiblePerSecond, .06f);
                rate += Positive(_menaceSettings.proximityPerSecond, .04f)
                    * Mathf.Clamp01(1f - distance / Positive(_menaceSettings.menaceRadius, 15f));
            }
            return rate > 0f ? rate : -Positive(_menaceSettings.decayPerSecond, .05f);
        }

        /// <summary>視認されない退出点を選び、失敗時は別の候補を再試行</summary>
        /// <param name="world">全知の世界状態</param>
        /// <param name="report">対象の敵</param>
        /// <param name="state">退避状態</param>
        /// <param name="index">指示の添字</param>
        /// <param name="dt">経過秒数</param>
        private void UpdateExit(EnemyWorldState world, EnemyReport report, BackstageState state, int index, float dt)
        {
            if (world.IsVisibleToPlayer == null) return;
            state.ExitAge += dt;
            if (state.HasExit && (world.IsVisibleToPlayer(state.Exit)
                || state.ExitAge >= Positive(_menaceSettings.exitRetryTime, 12f))) state.HasExit = false;
            if (!state.HasExit && state.Retry <= 0f)
            {
                state.Retry = Positive(_menaceSettings.pointRetryInterval, 2f);
                Span<Vector3> candidates = stackalloc Vector3[48];
                int count = _points.GetExitPoints(report.Position, candidates);
                float nearest = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    float distance = (candidates[i] - report.Position).sqrMagnitude;
                    if (distance >= nearest || world.IsVisibleToPlayer(candidates[i])
                        || (world.CanReachExit != null && !world.CanReachExit(report.Id, candidates[i]))) continue;
                    state.Exit = candidates[i];
                    nearest = distance;
                    state.HasExit = true;
                }
                state.ExitAge = 0f;
            }
            if (state.HasExit) _directives[index] = new DirectorDirective(DirectorDirectiveType.GoBackstage, state.Exit);
        }

        /// <summary>舞台裏の移動音と、十分休んだ後の不可視地点への出現を指示</summary>
        /// <param name="world">全知の世界状態</param>
        /// <param name="report">対象の敵</param>
        /// <param name="state">舞台裏状態</param>
        /// <param name="index">指示の添字</param>
        /// <param name="dt">経過秒数</param>
        private void UpdateHidden(EnemyWorldState world, EnemyReport report, BackstageState state, int index, float dt)
        {
            state.AbstractPosition = Vector3.MoveTowards(state.AbstractPosition, _hintPosition,
                Positive(_menaceSettings.backstageSpeed, 3f) * dt);
            state.SoundTime -= dt;
            if (state.SoundTime <= 0f)
            {
                _feedback?.OnEnemyBackstageMove(state.AbstractPosition);
                state.SoundTime = Positive(_menaceSettings.backstageSoundInterval, 4f);
            }
            if (!_menace.CanEmerge || _isPlayerFound.CurrentValue || state.Retry > 0f || world.IsVisibleToPlayer == null) return;
            state.Retry = Positive(_menaceSettings.pointRetryInterval, 2f);
            float minimum = Positive(_menaceSettings.emergeMinDistance, 15f);
            float maximum = Mathf.Max(minimum, Positive(_menaceSettings.emergeMaxDistance, 30f))
                * (1f + Mathf.Min(state.Attempts, 3) * .5f);
            state.Attempts++;
            if (!_points.TryFindEmergePoint(world.PlayerPosition, minimum, maximum, report.CanAmbushFromAbove,
                world.IsVisibleToPlayer, out Vector3 point)) return;
            // Providerの結果も再検証し、見えている点は採用しない
            float distance = Vector3.Distance(world.PlayerPosition, point);
            if (distance < minimum || distance > maximum || world.IsVisibleToPlayer(point)) return;
            _directives[index] = new DirectorDirective(DirectorDirectiveType.EmergeAt, point,
                Positive(_settings.hintCellSize, 12f) * .5f, _hintPosition);
        }

        /// <summary>プレイヤー位置をセルとオフセットにぼかす</summary>
        /// <param name="player">プレイヤー位置</param>
        private void UpdateHint(Vector3 player)
        {
            if (_hintTime > 0f) return;
            float cell = Positive(_settings.hintCellSize, 12f);
            float angle = ++_hintSequence * 2.399963f;
            _hintPosition = new Vector3(Mathf.Floor(player.x / cell) * cell + cell * .5f, player.y,
                Mathf.Floor(player.z / cell) * cell + cell * .5f)
                + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * cell * .5f;
            if ((_hintPosition - player).sqrMagnitude < .01f) _hintPosition += Vector3.right * cell * .25f;
            _hintTime = Positive(_settings.hintInterval, 8f);
        }

        /// <summary>同じフレームのBrain結果から発見演出を更新</summary>
        /// <param name="reports">敵自身が決めた警戒状態</param>
        public void ObserveReports(ReadOnlyMemory<EnemyReport> reports)
        {
            bool found = false;
            bool recognized = false;
            bool searching = false;
            bool recovering = false;
            foreach (EnemyReport report in reports.Span)
            {
                recovering |= report.Id == FocusEnemyId && report.IsBackstage;
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
            else if (recovering) SetPhase(EnemyDirectorPhase.Recovery);
            else if (searching) SetPhase(EnemyDirectorPhase.Search);
            else if (Phase == EnemyDirectorPhase.Hunt || Phase == EnemyDirectorPhase.Search || Phase == EnemyDirectorPhase.Recovery)
                SetPhase(EnemyDirectorPhase.Ambient);
        }

        /// <summary>フェーズが変わった時だけ経過時間を初期化</summary>
        /// <param name="phase">移行先</param>
        private void SetPhase(EnemyDirectorPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            _phaseTime = 0f;
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
