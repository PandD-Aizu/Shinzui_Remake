using Shinzui.Application.AI.BehaviorTree;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces.Enemy;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Application.AI.Enemy
{
    public sealed partial class EnemyBrain
    {
        private readonly PlayerTacticTracker _tactics;
        private readonly IEnemySearchPointProvider _points;
        private bool _wasVisible;
        private float _seenDistance;
        private bool _cornerPending;
        private bool _impactClue;
        private int _searchPointCount = 8;
        private Vector3 _searchOrigin;
        private bool _backtrackPending;
        private Vector3 _backtrackTarget;
        private float _backtrackTime;
        private float _reverseTime;
        private float _stalkTime;
        private float SearchSpeed => Positive(_settings.searchSpeedMultiplier, .8f)
            * (HasBehavior(EnemyBehaviorFlags.SlowStalk) ? .5f : 1f);

        /// <summary>共有された行動の解放を調べる</summary>
        /// <param name="flag">行動</param>
        /// <returns>解放済みか</returns>
        public bool HasBehavior(EnemyBehaviorFlags flag) => (Blackboard.Unlocked & flag) != 0;

        /// <summary>耐性解放後だけストロボの停止時間を半分にする</summary>
        /// <param name="duration">元の効果時間</param>
        /// <param name="stopsMovement">停止効果か</param>
        /// <returns>適用する効果時間</returns>
        public float StrobeDuration(float duration, bool stopsMovement)
            => stopsMovement && HasBehavior(EnemyBehaviorFlags.StrobeResistance) ? duration * .5f : duration;

        /// <summary>停止による知覚欠落を除き、近距離での視線切れを記録</summary>
        /// <param name="sight">今回の視覚</param>
        /// <param name="enabled">知覚可能か</param>
        private void ObserveSightLoss(SightSample sight, bool enabled)
        {
            bool visible = sight.InRange && sight.VisibleRatio > 0f;
            if (!enabled) { _wasVisible = false; _cornerPending = false; return; }
            if (visible) { _seenDistance = sight.Distance; _cornerPending = false; }
            else if (_wasVisible && _confirmed && _seenDistance <= 8f) _cornerPending = true;
            _wasVisible = visible;
        }

        /// <summary>中断ではなく捜索期限切れだけを学習し、一度だけ戻る準備を行う</summary>
        internal void SearchExpired()
        {
            if (_searching)
            {
                _tactics?.Record(PlayerTacticKind.EscapedSearch);
                SearchTimedOut?.Invoke();
                _lingerRemaining = HasBehavior(EnemyBehaviorFlags.LingerAfterSearch) ? 9f : 0f;
                _backtrackPending = HasBehavior(EnemyBehaviorFlags.Backtrack);
                _backtrackTarget = _searchOrigin;
                _backtrackTime = 0f;
            }
            FinishClue();
        }

        /// <summary>捜索後に少し離れてから開始点へ一度戻る</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>戻る間はRunning</returns>
        internal BtStatus Backtrack(float dt)
        {
            if (!_backtrackPending) return BtStatus.Failure;
            _backtrackTime += dt;
            if (_backtrackTime < 3f) return Wander(dt);
            if (Arrived(_backtrackTarget) || _backtrackTime >= 3f + Positive(_settings.moveTimeout, 6f))
            { _backtrackPending = false; return BtStatus.Failure; }
            _intent = new EnemyIntent(EnemyIntentType.MoveTo, _backtrackTarget, SearchSpeed);
            return BtStatus.Running;
        }

        /// <summary>捜索中に立ち止まって音を聞く</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>捜索中はRunning</returns>
        internal BtStatus SlowStalk(float dt)
        {
            _stalkTime += dt;
            if (_stalkTime % 4.5f >= 3f) { SetLook(_stalkTime); return BtStatus.Running; }
            return SearchPatrol(dt);
        }

        /// <summary>石の着弾点から観測した飛来方向を逆に辿る</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>対象がなければFailure、移動終了でSuccess</returns>
        internal BtStatus ReverseDecoy(float dt)
        {
            if (!_impactClue || Blackboard.LastHeardNoise.Direction.sqrMagnitude < .001f) return BtStatus.Failure;
            Vector3 direction = Blackboard.LastHeardNoise.Direction;
            direction.y = 0f;
            return Move(Blackboard.LastHeardNoise.Position - direction.normalized * 5f,
                Positive(_settings.investigateSpeedMultiplier, .5f), dt, ref _reverseTime);
        }

        /// <summary>任意の行動を飛ばして次の調査段階へ進む</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>Success</returns>
        internal BtStatus Skip(float dt) => BtStatus.Success;

        /// <summary>逆探知の移動時間を初期化</summary>
        internal void ResetReverse() => _reverseTime = 0f;

        /// <summary>未解放時に通常巡回の進捗を消さず聴取の時計だけを戻す</summary>
        internal void ResetSlowStalk() => _stalkTime = 0f;

        /// <summary>ストロボ解除時に観測した光源位置を調査</summary>
        /// <param name="source">命中時の光源位置</param>
        public void InvestigateStrobe(Vector3 source)
        {
            if (!HasBehavior(EnemyBehaviorFlags.StrobeResistance)) return;
            FinishClue();
            Blackboard.InvestigateTarget = source;
            Blackboard.HasClue = true;
            _investigating = true;
            _tree.Reset();
        }
    }
}
