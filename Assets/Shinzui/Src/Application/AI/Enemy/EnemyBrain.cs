using Shinzui.Application.AI.BehaviorTree;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.Domain.ValueObjects.Enemy;
using UnityEngine;

namespace Shinzui.Application.AI.Enemy
{
    /// <summary>視覚、聴覚、ぼかしたヒントだけで行動を選ぶ敵一体の脳</summary>
    public sealed partial class EnemyBrain
    {
        private readonly EnemyBrainSettings _settings;
        private readonly EnemyDirectorSettings _directorSettings;
        private readonly EnemyPerception _perception;
        private readonly BtNode _tree;
        private EnemyBrainInput _input;
        private EnemyIntent _intent;
        private float _time;
        private bool _confirmed;
        private bool _searching;
        private bool _investigating;
        private float _searchMoveTime;
        private float _searchLookTime;
        private float _moveTime;
        private float _lookTime;
        private float _turnTime;
        private float _patrolTime;
        private float _patrolLookTime;
        private int _pointIndex;
        private bool _patrolLooking;
        private bool _hasHint;
        private Vector3 _hintCenter;
        private Vector3 _hintTarget;
        private float _hintRadius;
        private float _hintTime;
        private uint _random;

        public EnemyBlackboard Blackboard { get; } = new();
        public string ActivePath => _tree.ActivePath;
        public EnemyIntent ActiveIntent => _intent;
        public EnemyAwarenessLevel Awareness => Blackboard.IsBackstage ? EnemyAwarenessLevel.Unaware : _confirmed ? EnemyAwarenessLevel.Engaged : _searching ? EnemyAwarenessLevel.Alerted : _perception.Level;
        internal float SearchDuration => Positive(_settings.searchDuration, 20f);
        internal bool CanSearch => Blackboard.HasClue && (_searching || Awareness >= EnemyAwarenessLevel.Alerted);
        internal bool CanInvestigate => Blackboard.HasClue && (_investigating || Awareness >= EnemyAwarenessLevel.Suspicious);

        /// <summary>知覚状態とツリーを一度だけ構築</summary>
        /// <param name="settings">知覚と行動の設定</param>
        /// <param name="directorSettings">視野と巡回速度の設定</param>
        /// <param name="seed">敵ごとの巡回用乱数種</param>
        /// <param name="tactics">共有する戦術の記録先</param>
        /// <param name="points">捜索点の取得先</param>
        public EnemyBrain(EnemyBrainSettings settings, EnemyDirectorSettings directorSettings, int seed = 1,
            PlayerTacticTracker tactics = null, Shinzui.Application.Interfaces.Enemy.IEnemySearchPointProvider points = null)
        {
            _settings = settings;
            _directorSettings = directorSettings;
            _perception = new EnemyPerception(settings);
            _tactics = tactics;
            _points = points;
            _random = (uint)seed + 1u;
            _tree = EnemyBrainTreeFactory.Create(this);
        }

        /// <summary>知覚を記憶し、優先順位に従って行動を選ぶ</summary>
        /// <param name="input">自身の状態と知覚サンプル</param>
        /// <param name="directive">追跡命令を含まない統括側の指示</param>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>敵自身が決めた行動</returns>
        public EnemyIntent Tick(EnemyBrainInput input, DirectorDirective directive, float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return _intent;
            _input = input;
            Blackboard.IsBackstage = input.IsBackstage;
            _time += deltaTime;
            Blackboard.Directive = directive;

            // 停止中の物理サンプルを捨て、知覚の記憶だけを減衰する
            bool sensesEnabled = input.IsReady && !input.IsStunned && !input.IsBackstage;
            SightSample sight = sensesEnabled ? input.Sight : default;
            float noise = sensesEnabled ? input.NoiseGain : 0f;
            ObserveSightLoss(sight, sensesEnabled);
            _perception.Update(sight, input.PlayerSignature, _directorSettings.sightDistance,
                _directorSettings.fieldOfView, noise, deltaTime);

            // 可視の位置だけを記憶し、見失ってからの猶予中も実座標には追従しない
            if (sight.VisibleRatio > 0f && sight.InRange)
            {
                Blackboard.LastSeenPosition = sight.SeenPosition;
                Blackboard.LastSeenVelocity = sight.SeenVelocity;
                Blackboard.LastSeenTime = _time;
                Blackboard.InvestigateTarget = sight.SeenPosition;
                Blackboard.HasClue = true;
                _impactClue = false;
                _backtrackPending = false;
                if (_perception.Level == EnemyAwarenessLevel.Engaged) _confirmed = true;
            }
            else if (_time - Blackboard.LastSeenTime >= Positive(_settings.chaseGraceTime, 1.5f))
                _confirmed = false;

            // 見えていない時だけ音源を優先し、音で追跡状態を作らない
            if (noise > 0f && sight.VisibleRatio <= 0f)
            {
                Blackboard.LastHeardNoise = input.HeardNoise;
                Blackboard.InvestigateTarget = input.HeardNoise.Position;
                Blackboard.HasClue = true;
                _impactClue = input.HeardNoise.Kind == NoiseKind.Impact;
                _backtrackPending = false;
            }

            _intent = new EnemyIntent(EnemyIntentType.Idle);
            _tree.Tick(deltaTime);
            return _intent;
        }

        /// <summary>Brainの判断を統括側へ報告</summary>
        /// <param name="id">敵の識別子</param>
        /// <param name="position">敵自身の位置</param>
        /// <param name="ready">行動可能か</param>
        /// <param name="visibleToPlayer">プレイヤーが敵を視認しているか</param>
        /// <returns>現在の警戒と行動</returns>
        /// <param name="pathDistance">プレイヤーまでの経路長</param>
        public EnemyReport CreateReport(int id, Vector3 position, bool ready, bool visibleToPlayer, float pathDistance = float.MaxValue)
            => new(id, position, _intent, ready, visibleToPlayer, Awareness, Blackboard.IsBackstage, pathDistance,
                (Blackboard.Unlocked & EnemyBehaviorFlags.AmbushFromAbove) != 0);

        /// <summary>物理層で退避が成功したことを反映</summary>
        public void ConfirmBackstage()
        {
            _backtrackPending = false;
            Blackboard.IsBackstage = true;
            _confirmed = false;
            FinishClue();
            _tree.Reset();
        }

        /// <summary>配置が成功してから曖昧なヒントの捜索を開始</summary>
        /// <param name="hint">Directorがぼかした捜索位置</param>
        public void ConfirmEmerged(Vector3 hint)
        {
            _backtrackPending = false;
            Blackboard.IsBackstage = false;
            _confirmed = false;
            FinishClue();
            Blackboard.InvestigateTarget = hint;
            Blackboard.HasClue = true;
            _perception.BeginSearch();
            _tree.Reset();
        }

        /// <summary>実際の舞台裏状態と退出地点への移動を優先処理</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>舞台裏制御中ならRunning</returns>
        internal BtStatus Backstage(float dt)
        {
            DirectorDirective directive = Blackboard.Directive;
            if (Blackboard.IsBackstage)
            {
                _intent = directive.Type == DirectorDirectiveType.EmergeAt
                    ? new EnemyIntent(EnemyIntentType.Emerge, directive.Position)
                    : new EnemyIntent(EnemyIntentType.Hidden);
                return BtStatus.Running;
            }
            if (_confirmed || !_input.IsReady || _input.IsStunned
                || directive.Type != DirectorDirectiveType.GoBackstage) return BtStatus.Failure;
            _intent = Arrived(directive.Position) && !_input.IsVisibleToPlayer
                ? new EnemyIntent(EnemyIntentType.Hidden)
                : new EnemyIntent(EnemyIntentType.GoBackstage, directive.Position,
                    Positive(_directorSettings.patrolSpeedMultiplier, .65f));
            return BtStatus.Running;
        }

        /// <summary>ストロボ停止または移動不能なら待機</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>停止中ならRunning</returns>
        internal BtStatus Stunned(float dt)
        {
            if (_input.IsReady && !_input.IsStunned) return BtStatus.Failure;
            _intent = new EnemyIntent(EnemyIntentType.Idle);
            return BtStatus.Running;
        }

        /// <summary>視認位置または猶予中の最終目撃地点を追跡</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>追跡中ならRunning</returns>
        internal BtStatus Chase(float dt)
        {
            if (!_confirmed) return BtStatus.Failure;
            _searching = false;
            _investigating = false;
            _intent = new EnemyIntent(EnemyIntentType.Chase, Blackboard.LastSeenPosition);
            return BtStatus.Running;
        }

        /// <summary>解放時だけ観測済み速度から見失った先を予測</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>見失い猶予中ならRunning</returns>
        internal BtStatus PredictiveChase(float dt)
        {
            if (!_confirmed || _input.Sight.VisibleRatio > 0f) return BtStatus.Failure;
            _searching = false;
            _investigating = false;
            _intent = new EnemyIntent(EnemyIntentType.Chase,
                Blackboard.LastSeenPosition + Blackboard.LastSeenVelocity * Positive(_settings.chaseGraceTime, 1.5f));
            return BtStatus.Running;
        }

        /// <summary>捜索開始地点へ移動し、周囲の捜索点を固定バッファへ作る</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>到着または移動期限でSuccess</returns>
        internal BtStatus SearchApproach(float dt)
        {
            if (!_searching)
            {
                if (_cornerPending) _tactics?.Record(PlayerTacticKind.BrokeSightAtCorner);
                _cornerPending = false;
                _searching = true;
                _investigating = false;
                _searchOrigin = Blackboard.InvestigateTarget;
                _searchPointCount = _points?.GetSearchPoints(_searchOrigin, Positive(_settings.searchRadius, 7f),
                    HasBehavior(EnemyBehaviorFlags.SearchSideRooms), Blackboard.SearchPoints) ?? 0;
                if (_searchPointCount == 0)
                {
                    _searchPointCount = Blackboard.SearchPoints.Length;
                    for (int i = 0; i < _searchPointCount; i++)
                    {
                        float angle = i * 2.399963f;
                        float radius = Positive(_settings.searchRadius, 7f) * (i + 1) / _searchPointCount;
                        Blackboard.SearchPoints[i] = _searchOrigin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    }
                }
            }
            return Move(Blackboard.InvestigateTarget, SearchSpeed, dt, ref _searchMoveTime);
        }

        /// <summary>捜索地点で周囲を見回す</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>見回し完了でSuccess</returns>
        internal BtStatus SearchLook(float dt) => Look(dt, ref _searchLookTime);

        /// <summary>捜索点で移動と見回しを繰り返す</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>時間制限までRunning</returns>
        internal BtStatus SearchPatrol(float dt)
        {
            Vector3 target = Blackboard.SearchPoints[_pointIndex];
            if (!_patrolLooking)
            {
                _patrolTime += dt;
                _intent = new EnemyIntent(EnemyIntentType.MoveTo, target, SearchSpeed);
                if (Arrived(target) || _patrolTime >= Positive(_settings.moveTimeout, 6f)) _patrolLooking = true;
            }
            else
            {
                _patrolLookTime += dt;
                SetLook(_patrolLookTime);
                if (_patrolLookTime >= Positive(_settings.lookAroundDuration, 2f))
                {
                    _pointIndex = (_pointIndex + 1) % _searchPointCount;
                    _patrolTime = _patrolLookTime = 0f;
                    _patrolLooking = false;
                }
            }
            return BtStatus.Running;
        }

        /// <summary>調査前に刺激の方向へ向く</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>向き直り完了でSuccess</returns>
        internal BtStatus TurnToClue(float dt)
        {
            if (!_investigating && _impactClue) _tactics?.RecordDecoy(Blackboard.LastHeardNoise);
            _investigating = true;
            _turnTime += dt;
            _intent = new EnemyIntent(EnemyIntentType.LookAround, Blackboard.InvestigateTarget);
            return _turnTime >= Positive(_settings.investigateTurnDuration, 1f) ? BtStatus.Success : BtStatus.Running;
        }

        /// <summary>疑わしい刺激の位置へ歩く</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>到着または期限でSuccess</returns>
        internal BtStatus InvestigateApproach(float dt)
            => Move(Blackboard.InvestigateTarget, Positive(_settings.investigateSpeedMultiplier, .5f), dt, ref _moveTime);

        /// <summary>調査地点で周囲を見る</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>見回し完了でSuccess</returns>
        internal BtStatus InvestigateLook(float dt) => Look(dt, ref _lookTime);

        /// <summary>調査済みの手掛かりを消費</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>Success</returns>
        internal BtStatus FinishInvestigation(float dt) { FinishClue(); return BtStatus.Success; }

        /// <summary>ヒント領域内で位置を変えながら巡回</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>ヒントがある場合はRunning</returns>
        internal BtStatus FollowHint(float dt)
        {
            DirectorDirective directive = Blackboard.Directive;
            if (directive.Type != DirectorDirectiveType.HintArea) return BtStatus.Failure;
            _hintTime += dt;
            if (!_hasHint || _hintCenter != directive.Position || _hintRadius != directive.Radius
                || Arrived(_hintTarget) || _hintTime >= Positive(_settings.moveTimeout, 6f))
            {
                _hasHint = true;
                _hintCenter = directive.Position;
                _hintRadius = directive.Radius;
                _hintTime = 0f;
                _random = _random * 1664525u + 1013904223u;
                float angle = (_random & 65535u) / 65535f * Mathf.PI * 2f;
                _hintTarget = _hintCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Max(0f, _hintRadius) * .7f;
            }
            _intent = new EnemyIntent(EnemyIntentType.MoveTo, _hintTarget, Positive(_directorSettings.patrolSpeedMultiplier, .65f));
            return BtStatus.Running;
        }

        /// <summary>知覚の手掛かりがない時は通常巡回</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>Running</returns>
        internal BtStatus Wander(float dt)
        {
            _intent = new EnemyIntent(EnemyIntentType.Wander, default, Positive(_directorSettings.patrolSpeedMultiplier, .65f));
            return BtStatus.Running;
        }

        /// <summary>期限付きで知覚由来の位置へ移動</summary>
        /// <param name="target">移動先</param>
        /// <param name="speed">速度倍率</param>
        /// <param name="dt">経過秒数</param>
        /// <returns>到着または期限でSuccess</returns>
        /// <param name="elapsed">行動ごとの移動経過時間</param>
        private BtStatus Move(Vector3 target, float speed, float dt, ref float elapsed)
        {
            elapsed += dt;
            _intent = new EnemyIntent(EnemyIntentType.MoveTo, target, speed);
            return Arrived(target) || elapsed >= Positive(_settings.moveTimeout, 6f) ? BtStatus.Success : BtStatus.Running;
        }

        /// <summary>周囲の見回しを進める</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>終了時Success</returns>
        /// <param name="elapsed">行動ごとの見回し経過時間</param>
        private BtStatus Look(float dt, ref float elapsed)
        {
            elapsed += dt;
            SetLook(elapsed);
            return elapsed >= Positive(_settings.lookAroundDuration, 2f) ? BtStatus.Success : BtStatus.Running;
        }

        /// <summary>自身の周囲を向く目標を作成</summary>
        /// <param name="elapsed">見回し経過秒数</param>
        private void SetLook(float elapsed)
            => _intent = new EnemyIntent(EnemyIntentType.LookAround,
                _input.EnemyPosition + new Vector3(Mathf.Sin(elapsed * 2f), 0f, Mathf.Cos(elapsed * 2f)) * 3f);

        /// <summary>目標への到着を確認</summary>
        /// <param name="target">移動先</param>
        /// <returns>到着許容距離内か</returns>
        private bool Arrived(Vector3 target) => (_input.EnemyPosition - target).sqrMagnitude
            <= Mathf.Pow(Positive(_settings.arrivalDistance, 1f), 2f);

        /// <summary>調査と捜索を終了</summary>
        internal void FinishClue()
        {
            Blackboard.HasClue = false;
            _searching = false;
            _investigating = false;
            _impactClue = false;
            _cornerPending = false;
        }
        /// <summary>捜索開始の移動時間を破棄</summary>
        internal void ResetSearchMove() => _searchMoveTime = 0f;
        /// <summary>捜索開始の見回し時間を破棄</summary>
        internal void ResetSearchLook() => _searchLookTime = 0f;
        /// <summary>移動の経過時間を破棄</summary>
        internal void ResetMove() => _moveTime = 0f;
        /// <summary>見回しの経過時間を破棄</summary>
        internal void ResetLook() => _lookTime = 0f;
        /// <summary>向き直りの経過時間を破棄</summary>
        internal void ResetTurn() => _turnTime = 0f;
        /// <summary>捜索点の巡回を初期化</summary>
        internal void ResetPatrol() { _pointIndex = 0; _patrolTime = _patrolLookTime = _stalkTime = 0f; _patrolLooking = false; }
        /// <summary>割り込まれたヒント巡回を解除</summary>
        internal void ResetHint() => _hasHint = false;
        /// <summary>無効な設定値を既定値に置換</summary>
        /// <param name="value">設定値</param>
        /// <param name="fallback">既定値</param>
        /// <returns>有効な設定値</returns>
        private static float Positive(float value, float fallback) => EnemyDirectorSettings.Positive(value, fallback);
    }
}
