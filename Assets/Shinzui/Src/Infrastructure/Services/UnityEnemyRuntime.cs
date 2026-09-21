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

        public bool CanSee(Vector3 playerPosition, float targetHeight)
        {
            if (!IsReady || _player == null) return false;
            Vector3 offset = playerPosition - Position;
            float distance = Mathf.Max(.1f, _settings.sightDistance);
            if (offset.sqrMagnitude > distance * distance) return false;
            offset.y = 0f;
            Vector3 forward = _body.forward;
            forward.y = 0f;
            float halfFov = Mathf.Clamp(_settings.fieldOfView, 1f, 360f) * .5f;
            if (offset.sqrMagnitude > .001f && Vector3.Angle(forward, offset) > halfFov) return false;
            return HasClearContact(playerPosition, targetHeight);
        }

        public bool HasClearContact(Vector3 playerPosition, float targetHeight)
        {
            if (!IsReady || _player == null) return false;
            Vector3 origin = Position + Vector3.up * Mathf.Max(.1f, _settings.eyeHeight);
            Vector3 target = playerPosition + Vector3.up * Mathf.Max(.1f, targetHeight * .5f);
            Vector3 offset = target - origin;
            int count = Physics.RaycastNonAlloc(origin, offset.normalized, _hits, offset.magnitude,
                _settings.sightBlockingLayers, QueryTriggerInteraction.Ignore);
            if (count == _hits.Length) return false; // Saturation must never turn a wall transparent.
            for (int i = 0; i < count; i++)
            {
                Transform hit = _hits[i].transform;
                if (hit == _body || hit.IsChildOf(_body) || _body.IsChildOf(hit)) continue;
                if (hit == _player || hit.IsChildOf(_player)) continue;
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