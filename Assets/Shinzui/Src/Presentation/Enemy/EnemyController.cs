using System.Collections.Generic;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using Shinzui.View;
using UnityEngine;

namespace Shinzui.Presentation
{
    /// <summary>Binds director commands to the runtime adapter and preserves strobe/death behavior.</summary>
    internal sealed class EnemyController
    {
        private readonly EnemyView _view;
        private readonly HumanEnemyLocomotion _humanMotion;
        private readonly IEnemyRuntime _runtime;
        private readonly EnemyMoveUseCase _movement;
        private readonly PlayerDeathUseCase _death;
        private readonly EnemyDirectorSettings _settings;
        private readonly System.Func<EnemyView, bool> _capture;
        private readonly EnemyBrainSettings _brainSettings;
        private readonly EnemyPerception _perception;
        private float _wanderTimer;
        private float _wanderInterval;
        private float _wanderRadius;
        private float _deathTimer;
        private float _strobeRemaining;
        private float _strobeMultiplier = 1f;
        private bool _strobeStops;
        private EnemyCommand _currentCommand = EnemyCommand.Idle;

        /// <summary>敵一体の知覚と移動を初期化</summary>
        /// <param name="id">敵の識別子</param>
        /// <param name="view">敵の表示</param>
        /// <param name="runtime">物理と経路探索</param>
        /// <param name="movement">移動状態</param>
        /// <param name="death">死亡判定</param>
        /// <param name="settings">統括設定</param>
        /// <param name="brainSettings">知覚設定</param>
        /// <param name="capture">捕獲演出の開始処理</param>
        public EnemyController(int id, EnemyView view, IEnemyRuntime runtime, EnemyMoveUseCase movement,
            PlayerDeathUseCase death, EnemyDirectorSettings settings, EnemyBrainSettings brainSettings, System.Func<EnemyView, bool> capture = null)
        {
            Id = id;
            _view = view;
            _humanMotion = view.GetComponent<HumanEnemyLocomotion>();
            _runtime = runtime;
            _movement = movement;
            _death = death;
            _settings = settings;
            _brainSettings = brainSettings;
            _perception = new EnemyPerception(brainSettings);
            _capture = capture;
        }

        public int Id { get; }
        public Vector3 StrobeTargetPosition => _runtime.Position + Vector3.up;

        public void Initialize()
        {
            (_wanderInterval, _wanderRadius, _) = _movement.GetWanderingInfo();
            _view.SetGizmoChaseDistance(_settings.sightDistance);
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
            _strobeRemaining = Mathf.Max(0f, _strobeRemaining - deltaTime);
            _deathTimer = Mathf.Max(0f, _deathTimer - deltaTime);
            if (_strobeRemaining <= 0f) { _strobeStops = false; _strobeMultiplier = 1f; }
            bool ready = _runtime.IsReady && !_strobeStops;
            SightSample sight = ready ? _runtime.SampleSight(playerPosition, playerHeight, playerVelocity) : default;
            bool canSeePlayer = sight.VisibleRatio > 0f;
            // 聴覚は遮蔽を反映し、最も強い刺激だけを調査対象にする
            float strongest = 0f;
            float totalNoiseGain = 0f;
            Vector3 heardPosition = default;
            if (ready)
            {
                for (int i = 0; i < noises.Count; i++)
                {
                    NoiseEvent noise = noises[i];
                    float distance = Vector3.Distance(_runtime.Position, noise.Position);
                    if (distance >= noise.Radius) continue;
                    float gain = EnemyPerception.NoiseGain(noise, distance, _runtime.HasLineTo(noise.Position), _brainSettings);
                    totalNoiseGain += gain;
                    if (gain <= strongest) continue;
                    strongest = gain;
                    heardPosition = noise.Position;
                }
            }
            _perception.Update(sight, signature, _settings.sightDistance, _settings.fieldOfView, totalNoiseGain, deltaTime);

            // 演出対象となる索敵中の敵だけプレイヤー側の視線を確認
            return _perception.CreateReport(Id, _runtime.Position, _currentCommand, _settings.sightDistance,
                ready, canSeePlayer,
                canSeePlayer && _view.IsVisibleFrom(playerCamera, playerRoot, _settings.sightBlockingLayers),
                strongest, heardPosition);
        }

        /// <summary>
        /// 接近による死亡判定と統括AIの移動命令を処理
        /// </summary>
        /// <param name="deltaTime">フレーム経過時間</param>
        /// <param name="player">プレイヤーの位置情報</param>
        /// <param name="playerHeight">プレイヤーの現在の高さ</param>
        /// <param name="command">統括AIの命令</param>
        public void Tick(float deltaTime, IPlayerTracker player, float playerHeight, EnemyCommand command)
        {
            if (player == null || _view == null || !_runtime.IsAvailable) return;
            bool changed = command.Type != _currentCommand.Type;
            _currentCommand = command;
            float speed = command.Type == EnemyCommandType.ChasePlayer ? 1f : Mathf.Clamp(_settings.patrolSpeedMultiplier, .1f, 1f);
            _runtime.SetSpeed(speed * _strobeMultiplier, _strobeStops || command.Type == EnemyCommandType.Idle);
            // 追跡命令を見た目へ反映し、腕をプレイヤーの上半身へ向ける
            if (_humanMotion != null)
                _humanMotion.SetChasePose(command.Type == EnemyCommandType.ChasePlayer && !_strobeStops && _runtime.IsReady,
                    player.PlayerPosition + Vector3.up * playerHeight * .35f);
            if (_strobeStops || !_runtime.IsReady) return;

            // 移動用の原点ではなく体の中心から接近距離を測定
            if (_death != null && _deathTimer <= 0f &&
                Vector3.Distance(_view.PlayerDeathPosition, player.PlayerPosition) <= _view.PlayerDeathDistance &&
                _runtime.HasClearContact(player.PlayerPosition, playerHeight))
            {
                bool killed = _capture != null ? _capture(_view) : _death.TryKillPlayer();
                _deathTimer = Mathf.Max(.1f, _view.DeathAttemptCooldown);
                if (killed) return;
            }

            switch (command.Type)
            {
                case EnemyCommandType.Idle:
                    _movement.Stop();
                    _runtime.Stop();
                    break;
                case EnemyCommandType.Wander:
                    _movement.StartWandering();
                    _wanderTimer -= deltaTime;
                    if (changed || _wanderTimer <= 0f)
                    {
                        _runtime.Wander(_wanderRadius);
                        _wanderTimer = Mathf.Max(.1f, _wanderInterval);
                    }
                    break;
                default:
                    _movement.StartChasing();
                    Vector3 target = command.TargetPosition;
                    var bounds = player.CurrentTunnelBounds;
                    if (command.Type == EnemyCommandType.ChasePlayer && bounds.HasValue)
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

        public void ApplyStrobeEffect(bool stopMovement, float speedMultiplier, float duration)
        {
            if (!_runtime.IsAvailable || duration <= 0f) return;
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
    }
}
