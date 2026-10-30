using System.Collections.Generic;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.Application.AI.Enemy;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces;
using Shinzui.Application.Interfaces.Enemy;
using Shinzui.Domain.ValueObjects.Enemy;
using Shinzui.Application.UseCases;
using Shinzui.View;
using UnityEngine;

namespace Shinzui.Presentation
{
    /// <summary>Brainの判断を移動へ反映し、ストロボと接触死亡を維持</summary>
    internal sealed class EnemyController
    {
        private readonly EnemyView _view;
        private readonly HumanEnemyLocomotion _humanMotion;
        private readonly float _humanChaseMultiplier;
        private readonly IEnemyRuntime _runtime;
        private readonly EnemyMoveUseCase _movement;
        private readonly PlayerDeathUseCase _death;
        private readonly EnemyDirectorSettings _settings;
        private readonly System.Func<EnemyView, bool> _capture;
        private readonly EnemyBrainSettings _brainSettings;
        private readonly EnemyBrain _brain;
        private EnemyBrainInput _input;
        private bool _visibleToPlayer;
        private Camera _playerCamera;
        private Transform _playerRoot;
        private float _pathTimer;
        private float _pathDistance = float.MaxValue;
        private readonly IEnemyEncounterFeedback _feedback;
        private float _wanderTimer;
        private float _wanderInterval;
        private float _wanderRadius;
        private float _deathTimer;
        private float _strobeRemaining;
        private float _strobeMultiplier = 1f;
        private bool _strobeStops;
        private EnemyIntent _currentIntent;
        private readonly PlayerTacticTracker _tactics;
        private Vector3 _strobeSource;
        private bool _investigateAfterStrobe;
        private float _debugTimer;
        private readonly Shinzui.Application.UseCases.Player.PlayerHidingUseCase _hiding;
        private readonly PlayerHidingPresenter _hidingPresenter;
        private readonly System.Func<int, bool> _openLocker;
        private bool _hidDuringSearch;
        private readonly IHidingSpotRegistry _hidingSpots;

        /// <summary>敵一体の知覚と移動を初期化</summary>
        /// <param name="id">敵の識別子</param>
        /// <param name="view">敵の表示</param>
        /// <param name="runtime">物理と経路探索</param>
        /// <param name="movement">移動状態</param>
        /// <param name="death">死亡判定</param>
        /// <param name="settings">統括設定</param>
        /// <param name="brainSettings">知覚設定</param>
        /// <param name="feedback">出現音の通知</param>
        /// <param name="capture">捕獲演出の開始処理</param>
        /// <param name="tactics">共有する学習状態</param>
        /// <param name="points">捜索候補の取得先</param>
        public EnemyController(int id, EnemyView view, IEnemyRuntime runtime, EnemyMoveUseCase movement,
            PlayerDeathUseCase death, EnemyDirectorSettings settings, EnemyBrainSettings brainSettings, IEnemyEncounterFeedback feedback,
            System.Func<EnemyView, bool> capture = null, PlayerTacticTracker tactics = null, IEnemySearchPointProvider points = null,
            IHidingSpotRegistry hidingSpots = null, Shinzui.Application.UseCases.Player.PlayerHidingUseCase hiding = null,
            PlayerHidingPresenter hidingPresenter = null, System.Func<int, bool> openLocker = null)
        {
            Id = id;
            _view = view;
            _humanMotion = view.GetComponent<HumanEnemyLocomotion>();
            if (_humanMotion != null)
            {
                // 通常歩行より10%速くし、歩行では追いつかれダッシュでは距離を離せる速度に設定
                var playerSpeed = new Shinzui.Domain.ValueObjects.Player.PlayerSpeedStatus();
                _humanChaseMultiplier = playerSpeed.MoveSpeed * 1.10f
                    / Mathf.Max(.01f, view.ResolveAgent().speed);
            }
            _runtime = runtime;
            _movement = movement;
            _death = death;
            _settings = settings;
            _brainSettings = brainSettings;
            _tactics = tactics;
            _brain = new EnemyBrain(brainSettings, settings, id + 1, tactics, points, hidingSpots);
            _hiding = hiding; _hidingPresenter = hidingPresenter; _openLocker = openLocker; _hidingSpots = hidingSpots;
            _brain.SearchTimedOut += OnSearchExpired;
            _capture = capture;
            _feedback = feedback;
        }

        public int Id { get; }
        public Vector3 StrobeTargetPosition => _runtime.Position + Vector3.up;

        public void Initialize()
        {
            (_wanderInterval, _wanderRadius, _) = _movement.GetWanderingInfo();
            _view.SetGizmoChaseDistance(_settings.sightDistance);
            _view.PrepareBackstage();
        }

        /// <summary>敵の索敵状態とプレイヤーからの視認状態を報告</summary>
        /// <param name="deltaTime">フレーム経過時間</param>
        /// <param name="playerPosition">プレイヤーの位置</param>
        /// <param name="playerHeight">プレイヤーの高さ</param>
        /// <param name="playerCamera">プレイヤーの視点カメラ</param>
        /// <param name="playerRoot">視線判定から除外するプレイヤー</param>
        /// <param name="playerVelocity">プレイヤーの速度</param>
        /// <param name="signature">姿勢と光</param>
        /// <param name="noises">フレーム内のノイズ</param>
        /// <returns>敵の状態報告</returns>
        public EnemyReport CreateReport(float deltaTime, Vector3 playerPosition, float playerHeight,
            Camera playerCamera, Transform playerRoot, Vector3 playerVelocity,
            PlayerSignature signature, IReadOnlyList<NoiseEvent> noises)
        {
            _runtime.Tick(deltaTime);
            if (_tactics != null) _brain.Blackboard.Unlocked = _tactics.State.Unlocked;
            _playerCamera = playerCamera;
            _playerRoot = playerRoot;
            _pathTimer -= deltaTime;
            if (_pathTimer <= 0f && !_runtime.IsBackstage)
            {
                _pathDistance = _runtime.PathDistanceTo(playerPosition);
                _pathTimer = .5f;
            }
            _strobeRemaining = Mathf.Max(0f, _strobeRemaining - deltaTime);
            _deathTimer = Mathf.Max(0f, _deathTimer - deltaTime);
            if (_strobeRemaining <= 0f)
            {
                _strobeStops = false;
                _strobeMultiplier = 1f;
                if (_investigateAfterStrobe) _brain.InvestigateStrobe(_strobeSource);
                _investigateAfterStrobe = false;
            }
            bool ready = _runtime.IsReady && !_strobeStops;
            bool hidden = _hiding != null && _hiding.IsHidden;
            SightSample sight = ready && !hidden ? _runtime.SampleSight(playerPosition, playerHeight, playerVelocity) : default;
            int witnessed = 0, presence = 0;
            if (ready && hidden)
            {
                if (_hiding.IsEntering && _brain.Awareness >= EnemyAwarenessLevel.Suspicious && _hidingPresenter != null
                    && _runtime.SampleSight(_hidingPresenter.EntryPosition, playerHeight, Vector3.zero).VisibleRatio > 0f)
                    witnessed = _hiding.CurrentSpotId;
                if (_brain.InspectingSpotId == _hiding.CurrentSpotId && !_hiding.IsHoldingBreath
                    && _hidingSpots != null && _hidingSpots.TryGet(_hiding.CurrentSpotId, out var occupiedSpot)
                    && Vector3.Distance(_runtime.Position, occupiedSpot.Position) <= 1.5f)
                    presence = _hiding.CurrentSpotId;
                if (_brain.Awareness == EnemyAwarenessLevel.Alerted && Vector3.Distance(_runtime.Position, playerPosition) <= 15f)
                    _hidDuringSearch = true;
            }
            if (_brain.Awareness == EnemyAwarenessLevel.Engaged || _runtime.IsBackstage) _hidDuringSearch = false;
            // 聴覚は遮蔽を反映し、最も強い刺激だけを調査対象にする
            float strongest = 0f;
            float totalNoiseGain = 0f;
            NoiseEvent heardNoise = default;
            if (ready)
            {
                for (int i = 0; i < noises.Count; i++)
                {
                    NoiseEvent noise = noises[i];
                    float distance = Vector3.Distance(_runtime.Position, noise.Position);
                    if (distance >= noise.Radius * (_brain.HasBehavior(EnemyBehaviorFlags.SlowStalk) ? 1.3f : 1f)) continue;
                    float gain = EnemyPerception.NoiseGain(noise, distance, _runtime.HasLineTo(noise.Position), _brainSettings,
                        _brain.Blackboard.Unlocked);
                    totalNoiseGain += gain;
                    if (gain <= strongest) continue;
                    strongest = gain;
                    heardNoise = noise;
                }
            }
            // Brainへは視覚サンプルと聞こえた音だけを渡す
            bool visibleForTransition = IsVisibleAt(_runtime.Position);
            _input = new EnemyBrainInput(_runtime.Position, sight, signature, totalNoiseGain, heardNoise,
                _runtime.IsReady, _strobeStops, _runtime.IsBackstage, visibleForTransition, new HidingSense(witnessed, presence));
            _visibleToPlayer = !_runtime.IsBackstage && _view.IsVisibleFrom(playerCamera, playerRoot, _settings.sightBlockingLayers);
            return CurrentReport();
        }

        /// <summary>行動決定後の状態を演出へ渡す</summary>
        /// <returns>最新のBrain報告</returns>
        public EnemyReport CurrentReport()
            => _brain.CreateReport(Id, _runtime.Position, (_runtime.IsReady && !_strobeStops) || _runtime.IsBackstage, _visibleToPlayer, _pathDistance);

        /// <summary>
        /// 接近による死亡判定とBrainの移動意図を処理
        /// </summary>
        /// <param name="deltaTime">フレーム経過時間</param>
        /// <param name="player">プレイヤーの位置情報</param>
        /// <param name="playerHeight">プレイヤーの現在の高さ</param>
        /// <param name="directive">統括AIのヒント</param>
        public void Tick(float deltaTime, IPlayerTracker player, float playerHeight, DirectorDirective directive)
        {
            if (player == null || _view == null || (!_runtime.IsAvailable && !_runtime.IsBackstage)) return;
            EnemyIntent intent = _brain.Tick(_input, directive, deltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugTimer -= deltaTime;
            if (_debugTimer <= 0f)
            {
                _debugTimer = .5f;
                _view.SetLearningDebug(_brain.ActivePath, _brain.Blackboard.Unlocked.ToString(),
                    _tactics == null ? "" : $"Sneak:{_tactics.State.Count(PlayerTacticKind.SneakNearEnemy)} Decoy:{_tactics.State.Count(PlayerTacticKind.DecoyThrow)} Strobe:{_tactics.State.Count(PlayerTacticKind.StrobeHitEnemy)} Escape:{_tactics.State.Count(PlayerTacticKind.EscapedSearch)} Corner:{_tactics.State.Count(PlayerTacticKind.BrokeSightAtCorner)} Cooldown:{_tactics.State.CooldownRemaining:F1}");
            }
#endif
            // 移動用の原点ではなく体の中心から接近距離を測定
            if (!(_hiding?.IsHidden ?? false) && !_runtime.IsBackstage && _runtime.IsReady && !_strobeStops && _death != null && _deathTimer <= 0f &&
                Vector3.Distance(_view.PlayerDeathPosition, player.PlayerPosition) <= _view.PlayerDeathDistance &&
                _runtime.HasClearContact(player.PlayerPosition, playerHeight))
            {
                bool killed = _capture != null ? _capture(_view) : _death.TryKillPlayer();
                _deathTimer = Mathf.Max(.1f, _view.DeathAttemptCooldown);
                if (killed) return;
            }

            // 通常行動中だけ叫び、舞台裏や出現待ちでは音声を停止
            if (_humanMotion != null)
                _humanMotion.TickVoice(deltaTime, _runtime.IsReady && !_runtime.IsBackstage
                    && intent.Type != EnemyIntentType.Hidden && intent.Type != EnemyIntentType.Emerge);

            // 表示と接触を切り替える直前に再確認し、見えている間は切り替えない
            if (intent.Type == EnemyIntentType.Hidden)
            {
                if (!_runtime.IsBackstage && !IsVisibleAt(_runtime.Position))
                {
                    _runtime.SetBackstage(true);
                    _view.SetBackstage(true);
                    _brain.ConfirmBackstage();
                }
                return;
            }
            if (intent.Type == EnemyIntentType.Emerge)
            {
                if (_runtime.IsBackstage && !IsVisibleAt(intent.Target) && _runtime.WarpTo(intent.Target)
                    && !IsVisibleAt(_runtime.Position))
                {
                    _runtime.SetBackstage(false);
                    // Agent固有の経路でもプレイヤーと接続されていることを確認する
                    if (!_runtime.IsReady || _runtime.PathDistanceTo(player.PlayerPosition) == float.MaxValue)
                    { _runtime.SetBackstage(true); return; }
                    _view.SetBackstage(false);
                    _brain.ConfirmEmerged(directive.SearchHint);
                    _feedback?.OnEnemyEmerge(_runtime.Position);
                    _pathTimer = 0f;
                }
                return;
            }
            bool changed = intent.Type != _currentIntent.Type;
            _currentIntent = intent;
            bool stopped = intent.Type == EnemyIntentType.Idle || intent.Type == EnemyIntentType.LookAround
                || intent.Type == EnemyIntentType.InspectHidingSpot || intent.Type == EnemyIntentType.OpenHidingSpot;
            float speedMultiplier = _humanMotion != null && intent.Type == EnemyIntentType.Chase
                ? _humanChaseMultiplier : intent.SpeedMultiplier;
            _runtime.SetSpeed(speedMultiplier * _strobeMultiplier, _strobeStops || stopped);
            // 追跡命令を見た目へ反映し、腕をプレイヤーの上半身へ向ける
            if (_humanMotion != null)
                _humanMotion.SetChasePose(intent.Type == EnemyIntentType.Chase && !_strobeStops && _runtime.IsReady,
                    intent.Target + Vector3.up * playerHeight * .35f);
            if (_strobeStops || !_runtime.IsReady) return;

            switch (intent.Type)
            {
                case EnemyIntentType.OpenHidingSpot:
                    _runtime.Stop();
                    _brain.FinishHidingInspection();
                    if (_openLocker?.Invoke(intent.HidingSpotId) == true) _brain.ConfirmPulledOut(player.PlayerPosition);
                    return;
                case EnemyIntentType.Idle:
                    _movement.Stop();
                    _runtime.Stop();
                    break;
                case EnemyIntentType.Wander:
                    _movement.StartWandering();
                    _wanderTimer -= deltaTime;
                    if (changed || _wanderTimer <= 0f)
                    {
                        _runtime.Wander(_wanderRadius);
                        _wanderTimer = Mathf.Max(.1f, _wanderInterval);
                    }
                    break;
                case EnemyIntentType.LookAround:
                case EnemyIntentType.InspectHidingSpot:
                    _movement.Stop();
                    _runtime.Stop();
                    _runtime.FaceTowards(intent.Target);
                    break;
                case EnemyIntentType.GoBackstage:
                case EnemyIntentType.MoveTo:
                case EnemyIntentType.Chase:
                    _movement.StartChasing();
                    Vector3 target = intent.Target;
                    var bounds = player.CurrentTunnelBounds;
                    if (intent.Type == EnemyIntentType.Chase && bounds.HasValue)
                    {
                        Vector3 dummy = target;
                        float length = Mathf.Abs(bounds.Value.end.z - bounds.Value.start.z);
                        dummy.z += target.z > (bounds.Value.start.z + bounds.Value.end.z) * .5f ? -length : length;
                        if ((_runtime.Position - dummy).sqrMagnitude < (_runtime.Position - target).sqrMagnitude) target = dummy;
                    }
                    _runtime.MoveTo(target);
                    break;
            }
            var tunnel = player.CurrentTunnelBounds;
            if (tunnel.HasValue)
            {
                if (_runtime.Position.z > tunnel.Value.end.z) _runtime.WarpZ(tunnel.Value.start.z);
                else if (_runtime.Position.z < tunnel.Value.start.z) _runtime.WarpZ(tunnel.Value.end.z);
            }
        }

        /// <summary>実位置と候補位置の保守的な視認判定</summary>
        /// <param name="position">判定する位置</param>
        /// <returns>一部でも見えるか、カメラが不明ならtrue</returns>
        public bool IsVisibleAt(Vector3 position)
            => _view.IsVisibleAt(position, _playerCamera, _playerRoot, _settings.sightBlockingLayers);

        /// <summary>退出点がこの敵のAgentでも到達可能か確認</summary>
        /// <param name="position">退出候補</param>
        /// <returns>完全経路があるか</returns>
        public bool CanReachExit(Vector3 position) => _runtime.PathDistanceTo(position) < float.MaxValue;

        /// <summary>命中を記録し、耐性解放後は停止時間と解除後の調査を変える</summary>
        /// <param name="stopMovement">移動を止めるか</param>
        /// <param name="speedMultiplier">減速倍率</param>
        /// <param name="duration">効果時間</param>
        /// <param name="source">命中時の光源位置</param>
        public void ApplyStrobeEffect(bool stopMovement, float speedMultiplier, float duration, Vector3 source)
        {
            if (!_runtime.IsAvailable || duration <= 0f) return;
            _tactics?.Record(PlayerTacticKind.StrobeHitEnemy);
            duration = _brain.StrobeDuration(duration, stopMovement);
            if (_brain.HasBehavior(EnemyBehaviorFlags.StrobeResistance))
            {
                _strobeSource = source;
                _investigateAfterStrobe = true;
            }
            if (!stopMovement && _strobeStops) return;
            _strobeStops |= stopMovement;
            _strobeMultiplier = Mathf.Min(_strobeMultiplier, Mathf.Clamp01(speedMultiplier));
            _strobeRemaining = Mathf.Max(_strobeRemaining, duration);
            _runtime.SetSpeed(_strobeStops ? 0f : _strobeMultiplier, _strobeStops);
        }

        public void Dispose()
        {
            _runtime.Stop();
            _runtime.SetSpeed(1f, false);
        }

        /// <summary>隠れながら捜索をやり過ごした回だけ共有学習へ通知</summary>
        private void OnSearchExpired()
        {
            if (_hidDuringSearch) _tactics?.Record(PlayerTacticKind.HidDuringSearch);
            _hidDuringSearch = false;
        }
    }
}
