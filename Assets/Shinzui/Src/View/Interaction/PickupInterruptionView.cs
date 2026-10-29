using UnityEngine;
using UnityEngine.Events;

namespace Shinzui.View.Interaction
{
    /// <summary>アイテムに付け、音やTimelineなどを取得途中のイベントへ接続</summary>
    [DisallowMultipleComponent]
    public sealed class PickupInterruptionView : MonoBehaviour, IPickupInterruption
    {
        [SerializeField, Range(0f, 1f)] private float triggerProgress = .65f;
        [SerializeField] private bool waitForResume;
        [SerializeField] private UnityEvent onReached = new();
        [SerializeField] private UnityEvent onAcquired = new();
        [SerializeField] private UnityEvent onCancelled = new();
        [SerializeField] private UnityEvent onEnded = new();
        private bool _active;

        public float TriggerProgress => triggerProgress;
        public PickupInterruptionResult Result { get; private set; }

        /// <summary>イベントを開始し、必要ならResumeまで進捗を止める</summary>
        public void Begin()
        {
            _active = true;
            Result = waitForResume ? PickupInterruptionResult.Wait : PickupInterruptionResult.Continue;
            onReached.Invoke();
        }

        /// <summary>外部の演出が終わった時に取得を再開</summary>
        public void Resume()
        {
            if (_active) Result = PickupInterruptionResult.Continue;
        }

        /// <summary>外部の演出から取得を中止</summary>
        public void Cancel()
        {
            if (_active) Result = PickupInterruptionResult.Cancel;
        }

        /// <summary>演出に終了理由を伝え、後始末を一度だけ実行</summary>
        /// <param name="acquired">取得を完了したか</param>
        public void End(bool acquired)
        {
            if (!_active) return;
            _active = false;
            if (acquired) onAcquired.Invoke();
            else onCancelled.Invoke();
            onEnded.Invoke();
        }

        /// <summary>対象が消えた場合も演出を解放</summary>
        private void OnDisable()
        {
            Result = PickupInterruptionResult.Cancel;
            End(false);
        }
    }
}
