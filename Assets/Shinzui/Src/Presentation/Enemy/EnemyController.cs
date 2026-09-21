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
        private readonly IEnemyRuntime _runtime;
        private readonly EnemyMoveUseCase _movement;
        private readonly PlayerDeathUseCase _death;
        private readonly EnemyDirectorSettings _settings;
        private float _wanderTimer;
        private float _wanderInterval;
        private float _wanderRadius;
        private float _deathTimer;
        private float _strobeRemaining;
        private float _strobeMultiplier = 1f;
        private bool _strobeStops;
        private EnemyCommand _currentCommand = EnemyCommand.Idle;

        public EnemyController(int id, EnemyView view, IEnemyRuntime runtime, EnemyMoveUseCase movement,
            PlayerDeathUseCase death, EnemyDirectorSettings settings)
        {
            Id = id;
            _view = view;
            _runtime = runtime;
            _movement = movement;
            _death = death;
            _settings = settings;
        }

        public int Id { get; }
        public Vector3 StrobeTargetPosition => _runtime.Position + Vector3.up;

        public void Initialize()
        {
            (_wanderInterval, _wanderRadius, _) = _movement.GetWanderingInfo();
            _view.SetGizmoChaseDistance(_settings.sightDistance);
        }

        public EnemyReport CreateReport(float deltaTime, Vector3 playerPosition, float playerHeight)
        {
            _runtime.Tick(deltaTime);
            _strobeRemaining = Mathf.Max(0f, _strobeRemaining - deltaTime);
            _deathTimer = Mathf.Max(0f, _deathTimer - deltaTime);
            if (_strobeRemaining <= 0f) { _strobeStops = false; _strobeMultiplier = 1f; }
            bool ready = _runtime.IsReady && !_strobeStops;
            return new EnemyReport(Id, _runtime.Position, _currentCommand, _settings.sightDistance,
                ready, ready && _runtime.CanSee(playerPosition, playerHeight));
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
            if (_strobeStops || !_runtime.IsReady) return;

            // 移動用の原点ではなく体の中心から接近距離を測定
            if (_death != null && _deathTimer <= 0f &&
                Vector3.Distance(_view.PlayerDeathPosition, player.PlayerPosition) <= _view.PlayerDeathDistance &&
                _runtime.HasClearContact(player.PlayerPosition, playerHeight))
            {
                _death.TryKillPlayer();
                _deathTimer = Mathf.Max(.1f, _view.DeathAttemptCooldown);
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
