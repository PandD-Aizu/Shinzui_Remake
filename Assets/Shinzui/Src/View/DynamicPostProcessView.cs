using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.View
{
    /// <summary>
    /// カメラの移動・回転速度に応じてモーションブラーの強度を変化させ、
    /// カメラの視線上のオブジェクトとの距離に応じて被写界深度のピントを動的に調整するコンポーネント
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class DynamicPostProcessView : MonoBehaviour
    {
        [Header("Volume Settings")]
        [SerializeField] private Volume targetVolume;

        [Header("Motion Blur Settings")]
        [SerializeField] private bool enableMotionBlur = true;
        [SerializeField] private Transform trackingCamera;
        [SerializeField] private float maxVelocity = 10f; // m/s (この速度に達したときブラー強度が最大になる)
        [SerializeField] private float maxAngularVelocity = 180f; // deg/s (この回転速度に達したときブラー強度が最大になる)
        [SerializeField] private float minBlurIntensity = 0f;
        [SerializeField] private float maxBlurIntensity = 1f;
        [SerializeField] private float blurLerpSpeed = 5f;

        [Header("Depth of Field Settings")]
        [SerializeField] private bool enableDepthOfField = true;
        [SerializeField] private LayerMask focusLayerMask = ~0; // フォーカス対象レイヤー
        [SerializeField] private float defaultFocusDistance = 30f; // Raycast未ヒット時のデフォルトフォーカス距離
        [SerializeField] private float minFocusDistance = 0.2f;    // 最小ピント距離
        [SerializeField] private float maxFocusDistance = 100f;    // 最大ピント距離
        [SerializeField] private float focusLerpSpeed = 8f;        // ピント合わせの速度
        [SerializeField] private float raycastInterval = 0.1f;     // Raycast負荷軽減用の間隔(秒)

        private VolumeProfile profile;
        private MotionBlur motionBlur;
        private DepthOfField depthOfField;

        private Vector3 lastPosition;
        private Quaternion lastRotation;
        
        private float targetFocusDistance;
        private float raycastTimer;

        private void Start()
        {
            if (targetVolume == null)
            {
                targetVolume = GetComponent<Volume>();
            }

            if (targetVolume != null)
            {
                // 元のVolumeProfileアセットが直接書き換わるのを防ぐため、profileのコピーを取得して操作する
                profile = targetVolume.profile;

                // Motion Blurコンポーネントの取得、なければ追加
                if (!profile.TryGet(out motionBlur))
                {
                    motionBlur = profile.Add<MotionBlur>(true);
                }

                // Depth of Fieldコンポーネントの取得、なければ追加
                if (!profile.TryGet(out depthOfField))
                {
                    depthOfField = profile.Add<DepthOfField>(true);
                }
            }

            if (trackingCamera == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    trackingCamera = cam.transform;
                }
                else
                {
                    trackingCamera = transform;
                }
            }

            lastPosition = trackingCamera.position;
            lastRotation = trackingCamera.rotation;
            targetFocusDistance = defaultFocusDistance;

            // パラメータの上書き設定
            if (depthOfField != null && enableDepthOfField)
            {
                depthOfField.mode.overrideState = true;
                depthOfField.mode.value = DepthOfFieldMode.Bokeh;
                depthOfField.focusDistance.overrideState = true;
            }

            if (motionBlur != null && enableMotionBlur)
            {
                motionBlur.intensity.overrideState = true;
                motionBlur.intensity.value = minBlurIntensity;
            }
        }

        private void Update()
        {
            if (trackingCamera == null) return;

            // 被写界深度のフォーカス距離測定
            if (enableDepthOfField && depthOfField != null)
            {
                raycastTimer += Time.deltaTime;
                if (raycastTimer >= raycastInterval)
                {
                    raycastTimer = 0f;
                    UpdateAutoFocusDistance();
                }
            }
        }

        private void LateUpdate()
        {
            if (trackingCamera == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // モーションブラー強度の適用
            if (enableMotionBlur && motionBlur != null)
            {
                UpdateDynamicMotionBlur(dt);
            }

            // 被写界深度フォーカス距離の適用
            if (enableDepthOfField && depthOfField != null)
            {
                depthOfField.focusDistance.value = Mathf.Lerp(
                    depthOfField.focusDistance.value,
                    targetFocusDistance,
                    dt * focusLerpSpeed
                );
            }

            // 次フレーム追跡用のデータ更新
            lastPosition = trackingCamera.position;
            lastRotation = trackingCamera.rotation;
        }

        private void UpdateDynamicMotionBlur(float dt)
        {
            // カメラの移動速度を算出 (m/s)
            Vector3 posDiff = trackingCamera.position - lastPosition;
            float velocity = posDiff.magnitude / dt;

            // カメラの回転速度を算出 (度/秒)
            float angleDiff = Quaternion.Angle(trackingCamera.rotation, lastRotation);
            float angularVelocity = angleDiff / dt;

            // 制限値に対する比率 (0.0f - 1.0f)
            float velocityRatio = Mathf.Clamp01(velocity / maxVelocity);
            float angularRatio = Mathf.Clamp01(angularVelocity / maxAngularVelocity);
            float maxRatio = Mathf.Max(velocityRatio, angularRatio);

            // 目標ブラー強度の決定
            float targetBlur = Mathf.Lerp(minBlurIntensity, maxBlurIntensity, maxRatio);

            // 滑らかにブラー強度を適用
            motionBlur.intensity.value = Mathf.Lerp(
                motionBlur.intensity.value,
                targetBlur,
                dt * blurLerpSpeed
            );
        }

        private void UpdateAutoFocusDistance()
        {
            Ray ray = new Ray(trackingCamera.position, trackingCamera.forward);
            RaycastHit hit;

            // カメラの正面方向にRaycastを行いピント距離を決定
            if (Physics.Raycast(ray, out hit, maxFocusDistance, focusLayerMask))
            {
                targetFocusDistance = Mathf.Clamp(hit.distance, minFocusDistance, maxFocusDistance);
            }
            else
            {
                targetFocusDistance = defaultFocusDistance;
            }
        }
    }
}
