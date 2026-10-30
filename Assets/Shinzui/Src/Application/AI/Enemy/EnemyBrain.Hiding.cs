using System;
using System.Collections.Generic;
using Shinzui.Application.AI.BehaviorTree;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces.Enemy;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Application.AI.Enemy
{
    public sealed partial class EnemyBrain
    {
        private readonly IHidingSpotRegistry _hidingSpots;
        private readonly Dictionary<int, float> _inspectedUntil = new();
        private int _inspectId;
        private float _inspectTime, _presence, _inspectMoveTime;
        private float _lingerRemaining;
        public int InspectingSpotId => _inspectId;
        public event Action SearchTimedOut;
        /// <summary>目撃した入口へ移動し学習前でも開ける</summary>
        /// <param name="dt">経過時間</param>
        /// <returns>目撃対象がある間はRunning</returns>
        internal BtStatus OpenWitnessedLocker(float dt)
        {
            int id = Blackboard.WitnessedHidingSpotId;
            if (id == 0) return BtStatus.Failure;
            if (_hidingSpots == null || !_hidingSpots.TryGet(id, out var spot))
            {
                Blackboard.WitnessedHidingSpotId = 0;
                return BtStatus.Failure;
            }
            _intent = AtLocker(spot)
                ? new EnemyIntent(EnemyIntentType.OpenHidingSpot, spot.Position, hidingSpotId: id)
                : new EnemyIntent(EnemyIntentType.MoveTo, spot.StandPosition);
            return BtStatus.Running;
        }
        /// <summary>占有を知らず、手掛かり周辺のロッカーを順に検査</summary>
        /// <param name="dt">経過時間</param>
        /// <returns>検査中はRunning</returns>
        internal BtStatus InspectLocker(float dt)
        {
            if (_hidingSpots == null || (!CanSearch && !CanInvestigate)) return BtStatus.Failure;
            if (_inspectId == 0)
            {
                Span<HidingSpotDto> nearby = stackalloc HidingSpotDto[16];
                int count = _hidingSpots.GetWithin(Blackboard.InvestigateTarget, Positive(_settings.searchRadius, 7f), nearby);
                float nearest = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    if (_inspectedUntil.TryGetValue(nearby[i].Id, out float until) && until > _time) continue;
                    float distance = (nearby[i].StandPosition - _input.EnemyPosition).sqrMagnitude;
                    if (distance >= nearest) continue;
                    nearest = distance; _inspectId = nearby[i].Id;
                }
            }
            if (_inspectId == 0 || !_hidingSpots.TryGet(_inspectId, out var spot))
            { ResetHidingInspection(); return BtStatus.Failure; }
            _inspectMoveTime += dt;
            if (!AtLocker(spot))
            {
                if (_inspectMoveTime > Positive(_settings.moveTimeout, 6f)) { FinishHidingInspection(); return BtStatus.Failure; }
                _intent = new EnemyIntent(EnemyIntentType.MoveTo, spot.StandPosition, SearchSpeed);
                return BtStatus.Running;
            }
            _inspectTime += dt;
            if (_input.HidingSense.PresenceId == _inspectId) _presence += .35f * dt;
            bool open = _presence >= 1f;
            if (_inspectTime >= Positive(_settings.inspectDuration, 3f))
            {
                _random = _random * 1664525u + 1013904223u;
                float chance = HasBehavior(EnemyBehaviorFlags.LingerAfterSearch) ? _settings.lingerBlindOpenChance : _settings.blindOpenChance;
                open |= (_random & 65535u) / 65536f < Mathf.Clamp01(chance);
                if (!open) { FinishHidingInspection(); return BtStatus.Failure; }
            }
            _intent = new EnemyIntent(open ? EnemyIntentType.OpenHidingSpot : EnemyIntentType.InspectHidingSpot,
                spot.Position, hidingSpotId: _inspectId);
            return BtStatus.Running;
        }
        /// <summary>空振りでも検査間隔を設けて捜索を続ける</summary>
        public void FinishHidingInspection()
        {
            int id = _inspectId != 0 ? _inspectId : Blackboard.WitnessedHidingSpotId;
            if (id != 0) _inspectedUntil[id] = _time + Positive(_settings.inspectCooldown, 30f);
            Blackboard.WitnessedHidingSpotId = 0;
            ResetHidingInspection();
        }
        /// <summary>検査途中の状態を破棄</summary>
        private void ResetHidingInspection() { _inspectId = 0; _inspectTime = _presence = _inspectMoveTime = 0f; }
        /// <summary>通常捜索より近い位置まで寄ってから検査する</summary>
        /// <param name="spot">対象</param>
        /// <returns>正面に到達したか</returns>
        private bool AtLocker(HidingSpotDto spot)
            => Vector3.Distance(_input.EnemyPosition, spot.StandPosition) <= .5f;
        /// <summary>引きずり出して実際に露出したプレイヤーを追跡</summary>
        /// <param name="position">露出した位置</param>
        public void ConfirmPulledOut(Vector3 position)
        {
            _confirmed = true; Blackboard.LastSeenPosition = position; Blackboard.LastSeenTime = _time;
            Blackboard.InvestigateTarget = position; Blackboard.HasClue = true;
        }
        /// <summary>捜索後に立ち去るふりをして近くで待つ</summary>
        /// <param name="dt">経過時間</param>
        /// <returns>待機中はRunning</returns>
        internal BtStatus Linger(float dt)
        {
            if (_lingerRemaining <= 0f) return BtStatus.Failure;
            _lingerRemaining -= dt;
            if (_lingerRemaining > 6f) return Wander(dt);
            SetLook(_lingerRemaining);
            return BtStatus.Running;
        }
    }
}
