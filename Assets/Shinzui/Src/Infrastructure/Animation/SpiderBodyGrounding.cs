using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Shinzui.Infrastructure.Animation
{
    /// <summary>Samples the authored footprint in the navigation frame, so animated feet cannot feed back into body tilt.</summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class SpiderBodyGrounding : MonoBehaviour
    {
        [SerializeField] private LayerMask groundLayers = 1;
        [SerializeField, Range(0f, 40f)] private float maxTilt = 18f;
        [SerializeField, Min(0f)] private float maxHeightOffset = 0.15f;
        [SerializeField, Min(0.01f)] private float probeHeight = 0.35f;
        [SerializeField, Min(0.01f)] private float probeDepth = 0.5f;
        [SerializeField, Min(0.01f)] private float smoothing = 6f;
        [SerializeField, Range(0f, 80f)] private float maxGroundSlope = 45f;
        private Transform anchor;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3[] footprint;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Vector3[] groundPoints = new Vector3[8];
        private readonly bool[] supported = new bool[8];
        private bool initialized;
        public int SupportCount { get; private set; }
        public float TiltAngle => Vector3.Angle(transform.up, anchor ? anchor.up : Vector3.up);
        public float HeightOffset => transform.localPosition.y - restPosition.y;

        private void Start()
        {
            anchor = transform.parent;
            if (!anchor) { Debug.LogError("Spider grounding requires a separate navigation parent.", this); enabled = false; return; }
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            var constraints = GetComponentsInChildren<ChainIKConstraint>();
            Array.Sort(constraints, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (constraints.Length != 8) { enabled = false; return; }
            footprint = new Vector3[8];
            for (int i = 0; i < 8; i++) footprint[i] = transform.InverseTransformPoint(constraints[i].data.target.position);
            initialized = true;
        }

        private void OnDisable()
        {
            if (!initialized) return;
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
        }

        private void Update()
        {
            if (!initialized) return;
            float scale = Mathf.Abs(transform.lossyScale.x);
            var baseWorld = anchor.TransformPoint(restPosition);
            var baseRotation = anchor.rotation * restRotation;
            Vector3 normal = Vector3.zero;
            SupportCount = 0;
            for (int i = 0; i < footprint.Length; i++)
            {
                Vector3 point = baseWorld + baseRotation * (footprint[i] * scale);
                int count = Physics.RaycastNonAlloc(point + Vector3.up * probeHeight * scale, Vector3.down,
                    hits, (probeHeight + probeDepth) * scale, groundLayers, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                supported[i] = false;
                Vector3 hitNormal = Vector3.up;
                // Do not accept an incomplete hit set as reliable support.
                if (count == hits.Length) continue;
                for (int j = 0; j < count; j++)
                {
                    var hit = hits[j];
                    if (hit.transform.IsChildOf(anchor) || hit.distance >= nearest ||
                        Vector3.Angle(hit.normal, Vector3.up) > maxGroundSlope) continue;
                    nearest = hit.distance;
                    supported[i] = true;
                    groundPoints[i] = hit.point;
                    hitNormal = hit.normal;
                }
                if (supported[i]) { normal += hitNormal; SupportCount++; }
            }
            Quaternion wantedRotation = restRotation;
            float offset = 0f;
            if (SupportCount >= 3)
            {
                Vector3 up = Vector3.RotateTowards(Vector3.up, normal.normalized, maxTilt * Mathf.Deg2Rad, 0f);
                Quaternion worldRotation = Quaternion.FromToRotation(Vector3.up, up) * baseRotation;
                wantedRotation = Quaternion.Inverse(anchor.rotation) * worldRotation;
                for (int i = 0; i < footprint.Length; i++)
                    if (supported[i]) offset += groundPoints[i].y - (baseWorld + worldRotation * (footprint[i] * scale)).y;
                offset = Mathf.Clamp(offset / SupportCount / Mathf.Abs(anchor.lossyScale.y), -maxHeightOffset, maxHeightOffset);
            }
            float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, wantedRotation, blend);
            transform.localPosition = Vector3.Lerp(transform.localPosition, restPosition + Vector3.up * offset, blend);
        }
    }
}
