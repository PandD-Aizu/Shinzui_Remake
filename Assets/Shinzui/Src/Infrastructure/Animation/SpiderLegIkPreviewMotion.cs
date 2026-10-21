using System;
using UnityEngine;

namespace Shinzui.Infrastructure.Animation
{
    /// <summary>
    /// Moves the preview scene's IK targets before animation evaluation.
    /// This demonstration is not a locomotion or ground-contact system.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class SpiderLegIkPreviewMotion : MonoBehaviour
    {
        [SerializeField] private Transform[] targets = Array.Empty<Transform>();
        [SerializeField, Min(0.1f)] private float cycleSeconds = 3f;
        [SerializeField, Min(0f)] private float liftHeight = 0.06f;
        [SerializeField, Min(0f)] private float forwardDistance = 0.02f;

        private Vector3[] restPositions;
        private float elapsed;

        public void Configure(Transform[] footTargets)
        {
            targets = footTargets ?? throw new ArgumentNullException(nameof(footTargets));
            CaptureRestPose();
        }

        private void OnEnable() => CaptureRestPose();

        private void CaptureRestPose()
        {
            restPositions = new Vector3[targets.Length];
            for (int i = 0; i < targets.Length; i++)
                if (targets[i]) restPositions[i] = targets[i].localPosition;
            elapsed = 0f;
        }

        private void Update()
        {
            elapsed = Mathf.Repeat(elapsed + Time.deltaTime, Mathf.Max(0.1f, cycleSeconds));
            for (int i = 0; i < targets.Length && i < restPositions.Length; i++)
            {
                if (!targets[i]) continue;
                // L01..L04, R01..R04: opposite sides use opposite phases.
                float phase = ((i % 4) + i / 4) % 2 == 0 ? 0f : Mathf.PI;
                float lift = Mathf.Max(0f, Mathf.Sin(elapsed / Mathf.Max(0.1f, cycleSeconds) * 2f * Mathf.PI + phase));
                targets[i].localPosition = restPositions[i] + new Vector3(0f, liftHeight, forwardDistance) * lift;
            }
        }

        private void OnDisable()
        {
            if (restPositions == null) return;
            for (int i = 0; i < targets.Length && i < restPositions.Length; i++)
                if (targets[i]) targets[i].localPosition = restPositions[i];
        }
    }
}
