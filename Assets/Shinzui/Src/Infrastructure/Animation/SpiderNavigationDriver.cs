using System;
using Shinzui.Application.UseCases.Enemy;
using UnityEngine;
using UnityEngine.AI;

namespace Shinzui.Infrastructure.Animation
{
    /// <summary>NavMesh and sight adapter. Keep this on the upright navigation parent, outside the animated rig.</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SpiderNavigationDriver : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Transform[] patrolPoints = Array.Empty<Transform>();
        [SerializeField, Min(0f)] private float speed = 0.08f;
        [SerializeField, Min(0f)] private float turnSpeed = 12f;
        [SerializeField, Min(0f)] private float detectionDistance = 2f;
        [SerializeField, Range(1f, 360f)] private float fieldOfView = 220f;
        [SerializeField, Min(0f)] private float stopDistance = 0.65f;
        [SerializeField, Min(0f)] private float memorySeconds = 2f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.25f;
        [SerializeField, Min(0.01f)] private float sampleRadius = 0.3f;
        [SerializeField] private LayerMask sightBlockingLayers = ~0;
        [SerializeField, Min(0f)] private float eyeHeight = 0.55f;
        [SerializeField, Min(0f)] private float targetEyeHeight = 0.5f;

        private NavMeshAgent agent;
        private NavMeshPath path;
        private readonly RaycastHit[] sightHits = new RaycastHit[32];
        private SpiderMovementUseCase decision = new SpiderMovementUseCase();
        private Vector3 lastSeen;
        private Vector3 lastDestination;
        private bool hasDestination;
        private float repathRemaining, recoverRemaining;
        private SpiderMovementState previousState;
        private int previousPatrolIndex = -1;
        public Transform Target { get => target; set => target = value; }
        public SpiderMovementState State => decision.State;
        public bool IsReady => agent && agent.isActiveAndEnabled && agent.isOnNavMesh;
        public bool RouteBlocked { get; private set; }
        public void SetPatrolPoints(Transform[] points)
        {
            patrolPoints = points ?? Array.Empty<Transform>();
            hasDestination = false;
            repathRemaining = 0f;
            previousPatrolIndex = -1;
        }
        private NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

        private void Awake()
        {
            path = new NavMeshPath();
            agent = GetComponent<NavMeshAgent>();
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.autoTraverseOffMeshLink = false;
        }

        private void OnEnable()
        {
            decision = new SpiderMovementUseCase();
            recoverRemaining = repathRemaining = 0f;
            RouteBlocked = false;
            hasDestination = false;
            previousPatrolIndex = -1;
        }

        private void OnDisable()
        {
            if (IsReady) agent.ResetPath();
            if (agent) agent.enabled = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (!IsReady)
            {
                recoverRemaining -= dt;
                if (recoverRemaining > 0f) return;
                recoverRemaining = 0.5f;
                if (!NavMesh.SamplePosition(transform.position, out var spawn, sampleRadius, Filter)) return;
                transform.position = spawn.position;
                agent.enabled = true;
                if (!agent.Warp(spawn.position)) return;
            }
            agent.speed = speed * Mathf.Abs(transform.lossyScale.x);
            bool exists = target && target.gameObject.activeInHierarchy;
            float distance = exists ? Vector3.Distance(transform.position, target.position) : float.PositiveInfinity;
            bool visible = exists && CanSeeTarget(distance);
            if (visible) lastSeen = target.position;
            // Replanning and natural arrival can clear hasPath/remainingDistance temporarily.
            // Measure against the last successfully projected endpoint so the patrol dwell timer stays stable.
            bool arrived = hasDestination && !RouteBlocked &&
                Vector3.Distance(transform.position, lastDestination) <= agent.stoppingDistance + 0.025f;
            var state = decision.Tick(dt, exists, visible, distance, stopDistance, memorySeconds,
                patrolPoints.Length, arrived, RouteBlocked);
            if (state != previousState || previousPatrolIndex != decision.PatrolIndex) repathRemaining = 0f;
            previousState = state;
            previousPatrolIndex = decision.PatrolIndex;
            if (state == SpiderMovementState.Idle || state == SpiderMovementState.Hold)
            {
                agent.ResetPath();
                hasDestination = false;
                RouteBlocked = false;
                return;
            }
            Vector3 destination = lastSeen;
            if (state == SpiderMovementState.Patrol)
            {
                var waypoint = patrolPoints[decision.PatrolIndex];
                if (!waypoint)
                {
                    RouteBlocked = true;
                    hasDestination = false;
                    agent.ResetPath();
                    return;
                }
                destination = waypoint.position;
            }
            repathRemaining -= dt;
            if (repathRemaining <= 0f)
            {
                repathRemaining = Mathf.Max(0.05f, repathInterval);
                agent.stoppingDistance = state == SpiderMovementState.Chase ? stopDistance : 0.025f;
                RouteBlocked = !NavMesh.SamplePosition(destination, out var hit, sampleRadius, Filter)
                    || !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete;
                if (!RouteBlocked && agent.SetPath(path))
                {
                    lastDestination = hit.position;
                    hasDestination = true;
                }
                else
                {
                    RouteBlocked = true;
                    hasDestination = false;
                    agent.ResetPath();
                }
            }
            if (agent.isOnOffMeshLink) { agent.isStopped = true; RouteBlocked = true; return; }
            agent.isStopped = false;
            Vector3 direction = agent.desiredVelocity;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), turnSpeed * dt);
        }

        private bool CanSeeTarget(float distance)
        {
            if (distance > detectionDistance) return false;
            Vector3 horizontal = target.position - transform.position;
            horizontal.y = 0f;
            if (horizontal.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, horizontal) > fieldOfView * 0.5f) return false;
            Vector3 origin = transform.position + Vector3.up * eyeHeight;
            Vector3 ray = target.position + Vector3.up * targetEyeHeight - origin;
            int count = Physics.RaycastNonAlloc(origin, ray.normalized, sightHits, ray.magnitude,
                sightBlockingLayers, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i].transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(target)) continue;
                return false;
            }
            return true;
        }
    }
}
