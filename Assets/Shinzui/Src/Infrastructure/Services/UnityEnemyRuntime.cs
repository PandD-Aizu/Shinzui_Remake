using System;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces;
using UnityEngine;
using UnityEngine.AI;

namespace Shinzui.Infrastructure.Services
{
    /// <summary>Owns physics queries and NavMesh access. Never depends on View or Presentation.</summary>
    public sealed class UnityEnemyRuntime : IEnemyRuntime
    {
        private readonly NavMeshAgent _agent;
        private readonly Transform _body;
        private readonly Transform _player;
        private readonly Func<bool> _isEnabled;
        private readonly EnemyDirectorSettings _settings;
        private readonly NavMeshPath _path = new();
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly float _baseSpeed;
        private float _repathRemaining;
        private float _recoverRemaining;
        private Vector3 _lastTarget;
        private bool _hasTarget;

        public UnityEnemyRuntime(NavMeshAgent agent, Transform body, Transform player,
            Func<bool> isEnabled, EnemyDirectorSettings settings)
        {
            _agent = agent;
            _body = agent != null ? agent.transform : body;
            _player = player;
            _isEnabled = isEnabled;
            _settings = settings;
            _baseSpeed = agent != null ? agent.speed : 0f;
        }

        public bool IsAvailable => _body != null && _agent != null && _agent.isActiveAndEnabled && _isEnabled();
        public bool IsReady => IsAvailable && _agent.isOnNavMesh;
        public Vector3 Position => _body != null ? _body.position : Vector3.zero;

        public void Tick(float deltaTime)
        {
            _repathRemaining -= deltaTime;
            _recoverRemaining -= deltaTime;
            // Generated maps bake later than scene initialization. Recover only once a surface exists.
            if (!IsAvailable || _agent.isOnNavMesh || _recoverRemaining > 0f) return;
            _recoverRemaining = 1f;
            if (NavMesh.SamplePosition(Position, out NavMeshHit hit, 3f, Filter)) _agent.Warp(hit.position);
        }

        /// <summary>視覚サンプルから可視状態を取得</summary>
        /// <param name="playerPosition">対象の位置</param>
        /// <param name="targetHeight">対象の高さ</param>
        /// <returns>対象の一部が見えているか</returns>
        public bool CanSee(Vector3 playerPosition, float targetHeight)
            => SampleSight(playerPosition, targetHeight, Vector3.zero).VisibleRatio > 0f;

        /// <summary>視野内の頭、胴、足の遮蔽を調べる</summary>
        /// <param name="playerPosition">対象の位置</param>
        /// <param name="playerHeight">対象の高さ</param>
        /// <param name="playerVelocity">対象の速度</param>
        /// <returns>不可視時は座標を含まない視覚サンプル</returns>
        public SightSample SampleSight(Vector3 playerPosition, float playerHeight, Vector3 playerVelocity)
        {
            if (!IsReady || _player == null) return default;
            Vector3 offset = playerPosition - Position;
            float distance = offset.magnitude;
            offset.y = 0f;
            Vector3 forward = _body.forward;
            forward.y = 0f;
            float angle = offset.sqrMagnitude > .001f ? Vector3.Angle(forward, offset) : 0f;
            bool inRange = distance <= EnemyDirectorSettings.Positive(_settings.sightDistance, 16f);
            if (!inRange || angle > Mathf.Clamp(_settings.fieldOfView, 1f, 360f) * .5f)
                return new SightSample(inRange, distance, angle, 0f, default, default);

            // 姿勢に合わせて三点を下げ、部分遮蔽を割合として返す
            int visible = 0;
            float height = Mathf.Max(.1f, playerHeight);
            if (HasLineTo(playerPosition + Vector3.up * height * .9f)) visible++;
            if (HasLineTo(playerPosition + Vector3.up * height * .5f)) visible++;
            if (HasLineTo(playerPosition + Vector3.up * height * .1f)) visible++;
            return new SightSample(true, distance, angle, visible / 3f, playerPosition, playerVelocity);
        }

        /// <summary>従来の胴体への接触判定を維持</summary>
        /// <param name="playerPosition">対象の位置</param>
        /// <param name="targetHeight">対象の高さ</param>
        /// <returns>胴体への遮蔽がないか</returns>
        public bool HasClearContact(Vector3 playerPosition, float targetHeight)
            => IsReady && _player != null && HasLineTo(playerPosition + Vector3.up * Mathf.Max(.1f, targetHeight * .5f));

        /// <summary>自身とプレイヤーを除外して刺激への遮蔽を確認</summary>
        /// <param name="position">刺激の位置</param>
        /// <returns>遮蔽がないか</returns>
        public bool HasLineTo(Vector3 position)
        {
            if (!IsReady) return false;
            Vector3 origin = Position + Vector3.up * Mathf.Max(.1f, _settings.eyeHeight);
            Vector3 offset = position - origin;
            int count = Physics.RaycastNonAlloc(origin, offset.normalized, _hits, offset.magnitude,
                _settings.sightBlockingLayers, QueryTriggerInteraction.Ignore);
            if (count == _hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Transform hit = _hits[i].transform;
                if (hit == _body || hit.IsChildOf(_body) || _body.IsChildOf(hit)) continue;
                if (_player != null && (hit == _player || hit.IsChildOf(_player))) continue;
                return false;
            }
            return true;
        }

        private NavMeshQueryFilter Filter => new() { agentTypeID = _agent.agentTypeID, areaMask = _agent.areaMask };

        public bool MoveTo(Vector3 targetPosition)
        {
            if (!IsReady) return false;
            if (_repathRemaining > 0f) return _hasTarget;
            if (_hasTarget && (targetPosition - _lastTarget).sqrMagnitude < .25f &&
                (_agent.pathPending || (_agent.hasPath && _agent.pathStatus == NavMeshPathStatus.PathComplete))) return true;
            _repathRemaining = Mathf.Max(.05f, _settings.repathInterval);
            if (!TryPath(targetPosition, 3f))
            {
                Stop();
                return false;
            }
            _lastTarget = targetPosition;
            _hasTarget = true;
            return true;
        }

        private bool TryPath(Vector3 target, float sampleRadius)
        {
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, sampleRadius, Filter) ||
                !_agent.CalculatePath(hit.position, _path) || _path.status != NavMeshPathStatus.PathComplete) return false;
            return _agent.SetPath(_path);
        }

        public void Wander(float radius)
        {
            if (!IsReady) return;
            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
                if (!TryPath(Position + new Vector3(offset.x, 0f, offset.y), 3f)) continue;
                _hasTarget = false;
                return;
            }
            Stop();
        }

        public void Stop()
        {
            if (IsReady) _agent.ResetPath();
            _hasTarget = false;
        }

        /// <summary>水平方向だけを変え、見回し中の移動を発生させない</summary>
        /// <param name="position">注視する位置</param>
        public void FaceTowards(Vector3 position)
        {
            if (!IsReady) return;
            Vector3 direction = position - Position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= .001f) return;
            _body.rotation = Quaternion.LookRotation(direction);
        }

        public void SetSpeed(float multiplier, bool stopped)
        {
            if (!IsAvailable) return;
            _agent.speed = _baseSpeed * Mathf.Max(0f, multiplier);
            if (IsReady) _agent.isStopped = stopped;
        }

        public void WarpZ(float z)
        {
            if (!IsReady) return;
            Vector3 target = Position;
            target.z = z;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, Filter)) _agent.Warp(hit.position);
            _hasTarget = false;
        }
    }
}