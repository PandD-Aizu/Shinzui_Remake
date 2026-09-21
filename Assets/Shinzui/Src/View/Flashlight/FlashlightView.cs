using UnityEngine;
using FMODUnity;

namespace Shinzui.View.Flashlight
{
    public class FlashlightView : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Light flashlightLight;         // スポットライト等のLightコンポーネント
        [SerializeField] private Camera targetCamera;           // 追従対象のカメラ

        [Header("Held Model")]
        [SerializeField] private Transform flashlightModel;     // ライトを親に持つ手持ちモデル
        [SerializeField] private Vector3 modelLightOffset = new Vector3(-0.24f, -0.23f, 0.52f);

        [Header("Beam Optics")]
        [SerializeField] private bool useRealisticBeam;
        [SerializeField] private LightShadows beamShadows = LightShadows.None;
        [SerializeField, Min(1f)] private float beamFocusDistance = 8f;
        [SerializeField, Min(0.1f)] private float nearWallDistance = 2f;
        [SerializeField, Range(0.02f, 1f)] private float nearWallIntensity = 0.12f;
        [SerializeField] private LayerMask beamObstructionMask = ~0;

        [Header("Tracking Ease Settings")]
        [SerializeField] private float minSpeed = 2.0f;         // 目標接近時の最低追従速度
        [SerializeField] private float maxSpeed = 45.0f;        // 動き始めの最高追従速度
        [SerializeField] private float maxAngle = 30.0f;        // 最大速度になる角度差の閾値

        [Header("Sway (Hand Shake) Settings")]
        [SerializeField] private bool enableSway = true;        // 手振れを有効にするか
        [SerializeField] private float swaySpeed = 1.8f;        // 手振れの周期速度
        [SerializeField] private float swayAmount = 0.8f;       // 手振れの最大角度幅

        [Header("Volumetric Light Settings")]
        [SerializeField] private bool enableVolumetric = true;  // ボリュメトリック効果を有効にするか
        [Range(-1.0f, 1.0f)]
        [SerializeField] private float anisotropy = 0.25f;      // 前方散乱の度合い
        [Range(0.0f, 16.0f)]
        [SerializeField] private float scattering = 2.0f;       // 散乱強度
        [Range(0.0f, 1.0f)]
        [SerializeField] private float radius = 0.2f;           // 光源付近のノイズ減衰半径

        [Header("Strobe Visual Settings")]
        [SerializeField] private float maxStrobeBoost = 15.0f;             // ストロボ最大追加輝度値
        [SerializeField] private float volumetricScatteringBoost = 8.0f;   // ストロボ最大追加ボリュメトリック散乱度

        [Header("Strobe Hit Settings")]
        [SerializeField] private float strobeRange = 12.0f;                // ストロボが届く最大距離
        [SerializeField] private float strobeRadius = 1.2f;                // ストロボ判定の太さ
        [SerializeField] private float maxSlowDuration = 4.0f;             // 最大減速時間
        [SerializeField] private float slowSpeedMultiplier = 0.35f;        // 減速中の移動速度倍率
        [SerializeField] private float stopDistance = 3.0f;                // 最大チャージ時に停止できる距離
        [SerializeField] private float stopDuration = 2.0f;                // 停止時間
        [SerializeField] private float fullChargeStopThreshold = 0.98f;    // 停止扱いにするチャージ率
        [SerializeField] private float centerViewportRadius = 0.18f;       // 画面中央判定の半径
        [SerializeField] private LayerMask strobeHitMask = ~0;          // ストロボ命中判定対象

        private Quaternion _currentFollowRotation;          // 手振れを含まない純粋な追従回転キャッシュ
        private VolumetricAdditionalLight _volumetricLight; // ボリュメトリックライトコンポーネント参照
        private float _baseIntensity = -1f;                 // ライトの基本輝度
        private float _baseVolumetricScattering = -1f;      // ボリュメトリックライトの基本散乱強度
        private float _nearWallGain = 1f;                  // 壁際の減光係数
        private float _strobeFactor;                      // 現在のストロボ発光係数

        public Camera TargetCamera => targetCamera != null ? targetCamera : Camera.main;
        public Vector3 StrobeOrigin => TargetCamera != null ? TargetCamera.transform.position : transform.position;
        public Vector3 StrobeDirection => TargetCamera != null ? TargetCamera.transform.forward : transform.forward;
        public float StrobeRange => strobeRange;
        public float StrobeRadius => strobeRadius;
        public float MaxSlowDuration => maxSlowDuration;
        public float SlowSpeedMultiplier => slowSpeedMultiplier;
        public float StopDistance => stopDistance;
        public float StopDuration => stopDuration;
        public float FullChargeStopThreshold => fullChargeStopThreshold;
        public float CenterViewportRadius => centerViewportRadius;
        public LayerMask StrobeHitMask => strobeHitMask;

        private void EnsureBaseValuesCached()
        {
            if (_baseIntensity < 0f && flashlightLight != null)
            {
                _baseIntensity = flashlightLight.intensity;
            }

            if (_baseVolumetricScattering < 0f)
            {
                if (_volumetricLight != null)
                {
                    _baseVolumetricScattering = _volumetricLight.Scattering;
                }
                else
                {
                    _baseVolumetricScattering = scattering;
                }
            }
        }

        /// <summary>
        /// 追従対象とライトの影、空気中の散乱を初期化する
        /// </summary>
        private void Start()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            // ボリュメトリックライトコンポーネントを自動セットアップ
            if (flashlightLight != null && enableVolumetric)
            {
                _volumetricLight = flashlightLight.GetComponent<VolumetricAdditionalLight>();
                if (_volumetricLight == null)
                {
                    _volumetricLight = flashlightLight.gameObject.AddComponent<VolumetricAdditionalLight>();
                }
            }

            EnsureBaseValuesCached();
            
            if (flashlightLight != null)
            {
                flashlightLight.shadows = beamShadows;
                _currentFollowRotation = flashlightLight.transform.rotation;

                // ボリュメトリックライトコンポーネントの設定適用
                if (enableVolumetric && _volumetricLight != null)
                {
                    _volumetricLight.Anisotropy = anisotropy;
                    _volumetricLight.Scattering = scattering;
                    _volumetricLight.Radius = radius;

                    // 初期有効状態をライトの点灯状態に同期
                    _volumetricLight.enabled = flashlightLight.enabled;
                }
            }
            else
            {
                _currentFollowRotation = transform.rotation;
            }
        }

        /// <summary>
        /// 手持ちモデルの位置とライトの追従回転を更新する
        /// </summary>
        private void LateUpdate()
        {
            // モデル付きのライトは消灯中も視点に追従させる
            if (flashlightModel != null && flashlightLight != null && targetCamera != null)
            {
                flashlightLight.transform.position = targetCamera.transform.TransformPoint(modelLightOffset);
            }

            // ライトまたは手持ちモデルの回転をカメラに同期する
            if (flashlightLight != null && (flashlightLight.enabled || flashlightModel != null) && targetCamera != null)
            {
                // 左端の発光位置から画面中央の注視点へ照射する
                Quaternion targetRotation = targetCamera.transform.rotation;
                if (useRealisticBeam)
                {
                    Vector3 focus = targetCamera.transform.position + targetCamera.transform.forward * beamFocusDistance;
                    targetRotation = Quaternion.LookRotation(focus - flashlightLight.transform.position, targetCamera.transform.up);
                }

                // キャッシュされている回転とターゲットカメラの回転の角度差を計算
                float angleDiff = Quaternion.Angle(_currentFollowRotation, targetRotation);
                
                // 角度差を0〜1に正規化
                float t = Mathf.Clamp01(angleDiff / maxAngle);
                
                // Ease-Out Cubic
                // 角度差が大きい時は高速を維持し、目標に非常に近づいた時に急激に減速する
                float easeOutT = 1.0f - Mathf.Pow(1.0f - t, 3.0f);
                float currentSpeed = Mathf.Lerp(minSpeed, maxSpeed, easeOutT);

                // 計算された可変速度でSlerp補間を行い、基本追従回転を更新
                _currentFollowRotation = Quaternion.Slerp(
                    _currentFollowRotation,
                    targetRotation,
                    1f - Mathf.Exp(-Time.deltaTime * currentSpeed)
                );

                // 基本追従回転に手振れを加算して最終的なライトの姿勢にする
                if (enableSway)
                {
                    float time = Time.time * swaySpeed;
                    // ノイズでX軸とY軸の揺れ角を計算
                    float shakeX = (Mathf.PerlinNoise(time, 0.0f) - 0.5f) * swayAmount;
                    float shakeY = (Mathf.PerlinNoise(0.0f, time) - 0.5f) * swayAmount;
                    
                    flashlightLight.transform.rotation = _currentFollowRotation * Quaternion.Euler(shakeX, shakeY, 0f);
                }
                else
                {
                    flashlightLight.transform.rotation = _currentFollowRotation;
                }
            }

            // 壁際で白飛びを抑え、ストロボと通常光に同じ減光を適用する
            if (useRealisticBeam && flashlightLight != null && flashlightLight.enabled && targetCamera != null)
            {
                float gain = 1f;
                if (Physics.Raycast(targetCamera.transform.position, targetCamera.transform.forward,
                    out RaycastHit hit, nearWallDistance, beamObstructionMask, QueryTriggerInteraction.Ignore))
                {
                    gain = Mathf.Lerp(nearWallIntensity, 1f,
                        Mathf.SmoothStep(0f, 1f, hit.distance / nearWallDistance));
                }

                _nearWallGain = Mathf.Lerp(_nearWallGain, gain, 1f - Mathf.Exp(-Time.deltaTime * 12f));
                ApplyBeamIntensity();
            }
        }

        /// <summary>
        /// ライトのアクティブ状態を切り替える
        /// </summary>
        /// <param name="active">点灯する場合はtrue</param>
        public void SetLightActive(bool active)
        {
            if (flashlightLight != null)
            {
                flashlightLight.enabled = active;
                // シーンで指定した影品質を点灯後も維持する
                flashlightLight.shadows = beamShadows;

                if (active && targetCamera != null)
                {
                    // 点灯開始時に向きをカメラに同期させてワープを防ぐ
                    _currentFollowRotation = targetCamera.transform.rotation;
                    flashlightLight.transform.rotation = _currentFollowRotation;
                }

                // ボリュメトリックフォグへの寄与状態をライトと同期
                if (_volumetricLight != null)
                {
                    _volumetricLight.enabled = active;
                }
            }
        }

        /// <summary>
        /// ストロボの発光輝度を反映する
        /// </summary>
        /// <param name="strobeFactor">0.0 〜 1.0 の発光係数</param>
        public void SetStrobeIntensity(float strobeFactor)
        {
            _strobeFactor = Mathf.Clamp01(strobeFactor);
            ApplyBeamIntensity();
        }

        /// <summary>
        /// 通常光とストロボを合成し、壁際の減光を反映する
        /// </summary>
        private void ApplyBeamIntensity()
        {
            EnsureBaseValuesCached();
            float gain = useRealisticBeam ? _nearWallGain : 1f;

            if (flashlightLight != null)
            {
                flashlightLight.intensity = (_baseIntensity + maxStrobeBoost * _strobeFactor) * gain;
            }

            if (_volumetricLight != null)
            {
                _volumetricLight.Scattering = (_baseVolumetricScattering + volumetricScatteringBoost * _strobeFactor) * gain;
            }
        }
    }
}
