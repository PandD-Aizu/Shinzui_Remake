using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Shinzui.Infrastructure.Animation
{
    /// <summary>Engine-side ground probing and procedural IK target animation. Does not move the actor.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpiderDeityIkRig))]
    public sealed class SpiderProceduralWalk : MonoBehaviour
    {
        [SerializeField] private LayerMask groundLayers = 1;
        [SerializeField, Min(0.001f)] private float stepDistance = 0.065f;
        [SerializeField, Min(0.01f)] private float stepDuration = 0.28f;
        [SerializeField, Min(0f)] private float stepHeight = 0.055f;
        [SerializeField, Min(0.01f)] private float probeHeight = 0.2f;
        [SerializeField, Min(0.01f)] private float probeDepth = 0.3f;
        [SerializeField, Range(0f, 80f)] private float maxSlope = 50f;
        [SerializeField, Min(0f)] private float footOffset = 0.002f;
        [SerializeField, Min(0.1f)] private float teleportDistance = 0.6f;

        private sealed class Foot
        {
            public Transform Target, Root;
            public Vector3 Rest, Position, From, To;
            public float Reach, Time;
            public bool Planted, Moving;
            public int Group;
        }

        private Foot[] feet;
        private Vector3 previousPosition;
        private int nextGroup;
        private bool initialized;
        public int MovingFootCount { get; private set; }
        public int CompletedSteps { get; private set; }
        private float Scale => Mathf.Abs(transform.lossyScale.x);

        private void Start() => Initialize();

        private void OnEnable()
        {
            if (initialized) ResetContacts();
        }

        private void Initialize()
        {
            var constraints = GetComponentsInChildren<ChainIKConstraint>();
            Array.Sort(constraints, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (constraints.Length != 8)
            {
                Debug.LogError("Spider walking requires eight initialized IK legs.", this);
                enabled = false;
                return;
            }
            feet = new Foot[8];
            for (int i = 0; i < feet.Length; i++)
            {
                var data = constraints[i].data;
                if (!data.target || !data.root || !data.tip)
                {
                    Debug.LogError("Spider walking has an unresolved IK leg.", this);
                    enabled = false;
                    return;
                }
                float reach = 0f;
                for (var bone = data.tip; bone != data.root; bone = bone.parent)
                    reach += Vector3.Distance(bone.position, bone.parent.position);
                feet[i] = new Foot
                {
                    Target = data.target, Root = data.root,
                    Rest = transform.InverseTransformPoint(data.target.position),
                    Reach = reach, Group = (i % 4 + i / 4) % 2
                };
            }
            initialized = true;
            ResetContacts();
        }

        /// <summary>Replant after a teleport. Rest offsets remain those captured from the authored pose.</summary>
        public void ResetContacts()
        {
            if (!initialized) return;
            MovingFootCount = 0;
            nextGroup = 0;
            previousPosition = transform.position;
            foreach (var foot in feet)
            {
                foot.Moving = false;
                foot.Planted = FindGround(foot, out var contact);
                foot.Position = foot.Planted ? contact : transform.TransformPoint(foot.Rest);
                foot.Target.position = foot.Position;
            }
        }

        private bool FindGround(Foot foot, out Vector3 contact)
        {
            Vector3 rest = transform.TransformPoint(foot.Rest);
            contact = rest;
            // RaycastAll lets us reject actor colliders without allowing them to hide terrain.
            var hits = Physics.RaycastAll(rest + Vector3.up * probeHeight * Scale,
                Vector3.down, (probeHeight + probeDepth) * Scale, groundLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            bool found = false;
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= nearest ||
                    Vector3.Angle(hit.normal, Vector3.up) > maxSlope) continue;
                Vector3 point = hit.point + Vector3.up * footOffset * Scale;
                if (Vector3.Distance(foot.Root.position, point) > foot.Reach * 0.99f) continue;
                nearest = hit.distance;
                contact = point;
                found = true;
            }
            return found;
        }

        private void Update()
        {
            if (!initialized) return;
            if (Vector3.Distance(previousPosition, transform.position) > teleportDistance * Scale)
                ResetContacts();
            previousPosition = transform.position;
            MovingFootCount = 0;
            foreach (var foot in feet)
            {
                if (!foot.Moving) continue;
                foot.Time += Time.deltaTime;
                float t = Mathf.Clamp01(foot.Time / Mathf.Max(0.01f, stepDuration));
                foot.Position = Vector3.Lerp(foot.From, foot.To, t * t * (3f - 2f * t))
                    + Vector3.up * (Mathf.Sin(t * Mathf.PI) * stepHeight * Scale);
                if (t >= 1f)
                {
                    foot.Position = foot.To;
                    foot.Moving = false;
                    foot.Planted = true;
                    CompletedSteps++;
                }
                else MovingFootCount++;
            }
            if (MovingFootCount == 0)
            {
                // Only one diagonal group swings at once; idle groups cannot starve the other group.
                if (!BeginGroup(nextGroup)) BeginGroup(1 - nextGroup);
            }
            foreach (var foot in feet)
                foot.Target.position = foot.Position;
        }

        private bool BeginGroup(int group)
        {
            bool started = false;
            foreach (var foot in feet)
            {
                if (foot.Group != group || !FindGround(foot, out var destination)) continue;
                if (foot.Planted && Vector3.Distance(foot.Position, destination) <= stepDistance * Scale) continue;
                foot.From = foot.Position;
                foot.To = destination;
                foot.Time = 0f;
                foot.Moving = true;
                MovingFootCount++;
                started = true;
            }
            if (started) nextGroup = 1 - group;
            return started;
        }
    }
}
