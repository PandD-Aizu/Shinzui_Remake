using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using Shinzui.View;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Shinzui.Presentation
{
    /// <summary>
    /// 1体分の敵の行動、ストロボ効果、プレイヤー死亡判定を管理するクラス
    /// </summary>
    internal sealed class EnemyController
    {
        private readonly EnemyView _view;
        private readonly EnemyMoveUseCase _enemyMoveUseCase;
        private readonly PlayerDeathUseCase _playerDeathUseCase;

        private NavMeshAgent _agent;
        private float _timer;
        private float _wanderInterval = -1.0f;
        private float _wanderRadius = -1.0f;
        private float _chaseDistance = -1.0f;
        private float _deathAttemptTimer;
        private float _baseAgentSpeed = -1.0f;
        private float _strobeEffectRemaining;
        private float _strobeSpeedMultiplier = 1.0f;
        private bool _strobeStopsMovement;
        private EnemyCommand _currentCommand = EnemyCommand.Wander;

        public EnemyController(
            int id,
            EnemyView view,
            EnemyMoveUseCase enemyMoveUseCase,
            PlayerDeathUseCase playerDeathUseCase)
        {
            Id = id;
            _view = view;
            _enemyMoveUseCase = enemyMoveUseCase;
            _playerDeathUseCase = playerDeathUseCase;
        }

        public int Id { get; }

        public Vector3 StrobeTargetPosition => _agent != null
            ? _agent.transform.position + Vector3.up
            : _view.EnemyPosition + Vector3.up;

        /// <summary>
        /// 統括AIへ渡す現在のEnemy状態を作成する
        /// </summary>
        /// <returns>Enemy状態報告</returns>
        public EnemyReport CreateReport()
        {
            return new EnemyReport(
                Id,
                _view.EnemyPosition,
                _currentCommand,
                _chaseDistance,
                _view != null);
        }

        /// <summary>
        /// 初期化処理
        /// NavMeshAgentの取得、徘徊情報の取得、Gizmoの設定を行う
        /// </summary>
        public void Initialize()
        {
            _agent = _view.ResolveAgent();

            if (_agent != null)
            {
                _baseAgentSpeed = _agent.speed;
            }

            (_wanderInterval, _wanderRadius, _chaseDistance) = _enemyMoveUseCase.GetWanderingInfo();
            _timer = _wanderInterval;
            _view.SetGizmoChaseDistance(_chaseDistance);

            if (_wanderInterval < 0) Debug.LogWarning("EnemyPresenter: Wander interval is negative");
            if (_wanderRadius < 0) Debug.LogWarning("EnemyPresenter: WanderRadius is negative");
            if (_chaseDistance < 0) Debug.LogWarning("EnemyPresenter: Chase distance is negative");
        }

        /// <summary>
        /// 毎フレームの更新処理
        /// </summary>
        /// <param name="deltaTime">経過時間</param>
        /// <param name="playerTracker">プレイヤートラッカー</param>
        public void Tick(float deltaTime, IPlayerTracker playerTracker, EnemyCommand command)
        {
            UpdateStrobeEffect(deltaTime);
            UpdateDeathAttemptTimer(deltaTime);

            if (playerTracker == null)
            {
                return;
            }

            Vector3 playerPos = playerTracker.PlayerPosition;
            TryKillPlayerIfClose(playerPos);

            var tunnelBounds = playerTracker.CurrentTunnelBounds;
            if (!tunnelBounds.HasValue)
            {
                return;
            }

            Vector3 tunnelStartPos = tunnelBounds.Value.start;
            Vector3 tunnelEndPos = tunnelBounds.Value.end;
            Vector3 dummy = CreateLoopDummyPosition(playerPos, tunnelStartPos, tunnelEndPos);

            Vector3 enemyPosition = _view.EnemyPosition;
            float distanceToPlayer = Vector3.Distance(enemyPosition, playerPos);
            float distanceToDummy = Vector3.Distance(enemyPosition, dummy);

            ApplyCommand(command);
            TickMovement(deltaTime, enemyPosition, command, dummy, distanceToPlayer, distanceToDummy);
            WarpIfOutsideTunnel(tunnelStartPos, tunnelEndPos);
        }

        /// <summary>
        /// ストロボの効果を適用する
        /// </summary>
        /// <param name="stopMovement">移動を停止するかどうか</param>
        /// <param name="speedMultiplier">速度の倍率</param>
        /// <param name="duration">持続時間</param>
        public void ApplyStrobeEffect(bool stopMovement, float speedMultiplier, float duration)
        {
            if (_agent == null || duration <= 0f)
            {
                return;
            }

            if (_baseAgentSpeed < 0f)
            {
                _baseAgentSpeed = _agent.speed;
            }

            if (stopMovement)
            {
                _strobeStopsMovement = true;
                _strobeEffectRemaining = Mathf.Max(_strobeEffectRemaining, duration);
                ApplyCurrentStrobeAgentState();
                return;
            }

            if (_strobeStopsMovement)
            {
                return;
            }

            _strobeSpeedMultiplier = Mathf.Min(_strobeSpeedMultiplier, Mathf.Clamp01(speedMultiplier));
            _strobeEffectRemaining = Mathf.Max(_strobeEffectRemaining, duration);
            ApplyCurrentStrobeAgentState();
        }

        public void Dispose()
        {
            ClearStrobeEffect();
        }

        /// <summary>
        /// プレイヤーがトンネルをループした際に、敵が追跡するためのダミー位置を作成する
        /// </summary>
        /// <param name="playerPos">プレイヤーの位置</param>
        /// <param name="tunnelStartPos">トンネルの開始位置</param>
        /// <param name="tunnelEndPos">トンネルの終了位置</param>
        /// <returns>ダミー位置</returns>
        private static Vector3 CreateLoopDummyPosition(Vector3 playerPos, Vector3 tunnelStartPos, Vector3 tunnelEndPos)
        {
            Vector3 dummy = playerPos;
            float centerZ = (tunnelStartPos.z + tunnelEndPos.z) / 2.0f;
            float tunnelDistance = Mathf.Abs(tunnelStartPos.z - tunnelEndPos.z);

            if (playerPos.z > centerZ)
            {
                dummy.z -= tunnelDistance;
            }
            else
            {
                dummy.z += tunnelDistance;
            }

            return dummy;
        }

        /// <summary>
        /// 統括AIから受け取った命令を移動状態へ反映する
        /// </summary>
        /// <param name="command">統括AIから受け取った命令</param>
        private void ApplyCommand(EnemyCommand command)
        {
            _currentCommand = command;

            switch (command.Type)
            {
                case EnemyCommandType.ChasePlayer:
                case EnemyCommandType.InvestigatePosition:
                    _enemyMoveUseCase.StartChasing();
                    break;
                case EnemyCommandType.Idle:
                    _enemyMoveUseCase.Stop();
                    break;
                default:
                    _enemyMoveUseCase.StartWandering();
                    break;
            }
        }

        /// <summary>
        /// 敵の移動を更新する
        /// </summary>
        /// <param name="deltaTime">経過時間</param>
        /// <param name="enemyPosition">敵の位置</param>
        /// <param name="command">統括AIから受け取った命令</param>
        /// <param name="dummy">ダミー位置</param>
        /// <param name="distanceToPlayer">プレイヤーまでの距離</param>
        /// <param name="distanceToDummy">ダミー位置までの距離</param>
        private void TickMovement(
            float deltaTime,
            Vector3 enemyPosition,
            EnemyCommand command,
            Vector3 dummy,
            float distanceToPlayer,
            float distanceToDummy)
        {
            if (_enemyMoveUseCase.IsWandering)
            {
                _timer += deltaTime;
                if (_timer > _wanderInterval)
                {
                    SetDestination(RandomTarget(enemyPosition, _wanderRadius));
                    _timer = 0;
                }
            }

            if (_enemyMoveUseCase.IsChasing)
            {
                if (command.Type == EnemyCommandType.ChasePlayer)
                {
                    Vector3 target = distanceToPlayer < distanceToDummy ? command.TargetPosition : dummy;
                    SetDestination(target);
                    return;
                }

                if (command.Type == EnemyCommandType.InvestigatePosition)
                {
                    SetDestination(command.TargetPosition);
                    return;
                }
            }

            if (!_enemyMoveUseCase.IsWandering && command.Type == EnemyCommandType.Idle)
            {
                SetDestination(enemyPosition);
            }
        }

        /// <summary>
        /// 敵がトンネルの外に出た場合、トンネル内にワープさせる
        /// </summary>
        /// <param name="tunnelStartPos"></param>
        /// <param name="tunnelEndPos"></param>
        private void WarpIfOutsideTunnel(Vector3 tunnelStartPos, Vector3 tunnelEndPos)
        {
            if (_view.TransformPosition.z > tunnelEndPos.z)
            {
                _view.WarpTo(tunnelStartPos);
            }

            if (_view.TransformPosition.z < tunnelStartPos.z)
            {
                _view.WarpTo(tunnelEndPos);
            }
        }

        /// <summary>
        /// NavMeshAgentの目的地を設定する
        /// </summary>
        /// <param name="targetPosition"></param>
        private void SetDestination(Vector3 targetPosition)
        {
            if (_agent != null)
            {
                _agent.SetDestination(targetPosition);
            }
        }

        /// <summary>
        /// 指定された位置を中心に、NavMesh上のランダムな目的地を生成する
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        private Vector3 RandomTarget(Vector3 origin, float radius)
        {
            Vector3 randomDirection = Random.insideUnitSphere * radius;
            randomDirection += origin;

            NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, radius, NavMesh.AllAreas);
            return hit.position;
        }

        /// <summary>
        /// ストロボの効果を更新する
        /// </summary>
        /// <param name="deltaTime"></param>
        private void UpdateStrobeEffect(float deltaTime)
        {
            if (_agent == null || _strobeEffectRemaining <= 0f)
            {
                return;
            }

            _strobeEffectRemaining -= deltaTime;
            if (_strobeEffectRemaining <= 0f)
            {
                ClearStrobeEffect();
                return;
            }

            ApplyCurrentStrobeAgentState();
        }

        /// <summary>
        /// 現在のストロボ効果に基づいてNavMeshAgentの状態を適用する
        /// </summary>
        private void ApplyCurrentStrobeAgentState()
        {
            if (_agent == null)
            {
                return;
            }

            if (_strobeStopsMovement)
            {
                _agent.isStopped = true;
                _agent.speed = 0f;
            }
            else
            {
                _agent.isStopped = false;
                _agent.speed = _baseAgentSpeed * _strobeSpeedMultiplier;
            }
        }

        /// <summary>
        /// ストロボの効果をクリアする
        /// </summary>
        private void ClearStrobeEffect()
        {
            _strobeEffectRemaining = 0f;
            _strobeSpeedMultiplier = 1.0f;
            _strobeStopsMovement = false;

            if (_agent == null)
            {
                return;
            }

            _agent.isStopped = false;
            if (_baseAgentSpeed >= 0f)
            {
                _agent.speed = _baseAgentSpeed;
            }
        }

        /// <summary>
        /// プレイヤーが近くにいる場合、プレイヤーを殺す試みを行う
        /// </summary>
        /// <param name="playerPosition">プレイヤーの位置</param>
        private void TryKillPlayerIfClose(Vector3 playerPosition)
        {
            if (_playerDeathUseCase == null || _deathAttemptTimer > 0f)
            {
                return;
            }

            float distanceToPlayer = Vector3.Distance(_view.EnemyPosition, playerPosition);
            if (distanceToPlayer > _view.PlayerDeathDistance)
            {
                return;
            }

            _playerDeathUseCase.TryKillPlayer();
            _deathAttemptTimer = _view.DeathAttemptCooldown;
        }

        /// <summary>
        /// プレイヤーを殺す試みのクールダウンタイマーを更新する
        /// </summary>
        /// <param name="deltaTime">経過時間</param>
        private void UpdateDeathAttemptTimer(float deltaTime)
        {
            if (_deathAttemptTimer <= 0f)
            {
                return;
            }

            _deathAttemptTimer -= deltaTime;
        }
    }
}
