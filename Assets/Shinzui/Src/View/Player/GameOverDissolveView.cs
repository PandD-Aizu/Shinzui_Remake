using Cysharp.Threading.Tasks;
using Shinzui.Infrastructure.Rendering.GameOverDissolve;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shinzui.View
{
    [RequireComponent(typeof(Volume))]
    public class GameOverDissolveView : MonoBehaviour
    {
        [SerializeField] private Volume targetVolume;
        [SerializeField] private float duration = 1.35f;
        [SerializeField] private AnimationCurve progressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool resetOnAwake = true;

        private GameOverDissolveVolume _dissolveVolume;
        private bool _isPlaying;

        private void Awake()
        {
            EnsureVolume();

            if (resetOnAwake)
            {
                SetProgress(0f);
            }
        }

        public void Play()
        {
            if (_isPlaying)
            {
                return;
            }

            PlayAsync().Forget();
        }

        public async UniTask PlayAsync()
        {
            if (_isPlaying)
            {
                return;
            }

            _isPlaying = true;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                SetProgress(progressCurve.Evaluate(normalizedTime));

                await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
            }

            SetProgress(1f);
            _isPlaying = false;
        }

        public void SetProgress(float progress)
        {
            EnsureVolume();
            if (_dissolveVolume == null)
            {
                return;
            }

            float clampedProgress = Mathf.Clamp01(progress);
            _dissolveVolume.active = clampedProgress > 0.001f;
            _dissolveVolume.progress.overrideState = true;
            _dissolveVolume.progress.value = clampedProgress;

            GameOverDissolveRuntimeState.Set(
                clampedProgress,
                _dissolveVolume.edgeWidth.value,
                _dissolveVolume.noiseStrength.value,
                _dissolveVolume.cellIntensity.value,
                _dissolveVolume.coverColor.value,
                _dissolveVolume.membraneColor.value,
                _dissolveVolume.hotEdgeColor.value);
        }

        private void EnsureVolume()
        {
            if (targetVolume == null)
            {
                targetVolume = GetComponent<Volume>();
            }

            if (targetVolume == null)
            {
                return;
            }

            VolumeProfile profile = targetVolume.profile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Runtime Game Over Dissolve Profile";
                targetVolume.profile = profile;
            }

            if (!profile.TryGet(out _dissolveVolume))
            {
                _dissolveVolume = profile.Add<GameOverDissolveVolume>(true);
            }
        }
    }
}
