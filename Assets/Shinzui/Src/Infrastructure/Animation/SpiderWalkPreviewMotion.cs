using UnityEngine;

namespace Shinzui.Infrastructure.Animation
{
    /// <summary>Scene-only actor motion, executed before the feet are updated.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class SpiderWalkPreviewMotion : MonoBehaviour
    {
        [SerializeField] private float distance = 0.25f;
        [SerializeField] private float cycleSeconds = 16f;
        private Vector3 origin;
        private Quaternion rotation;
        private float elapsed;
        private void OnEnable()
        {
            origin = transform.position;
            rotation = transform.rotation;
            elapsed = 0f;
        }
        private void Update()
        {
            elapsed += Time.deltaTime;
            float phase = elapsed * 2f * Mathf.PI / Mathf.Max(1f, cycleSeconds);
            transform.position = origin + rotation * Vector3.forward * (Mathf.Sin(phase) * distance);
            transform.rotation = rotation * Quaternion.Euler(0f, Mathf.Sin(phase * 0.5f) * 20f, 0f);
        }
    }
}
