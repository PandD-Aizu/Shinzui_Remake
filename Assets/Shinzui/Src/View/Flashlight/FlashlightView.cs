using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using Shinzui.Infrastructure.Lighting;
using FMODUnity;
using Unity.Cinemachine;

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
        [SerializeField] private LightShadows beamShadows = LightShadows.Soft;
        [SerializeField, Min(1f)] private float beamFocusDistance = 8f;
        [SerializeField, Range(0f, 20f)] private float beamDownwardAngle = 8f;
#if UNITY_EDITOR
        private bool _lookDevForceLight;
#endif
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
        [SerializeField, HideInInspector] private float anisotropy = 0.25f;      // 前方散乱の度合い
        [Range(0.0f, 16.0f)]
        [SerializeField] private float scattering = 0.65f;       // 散乱強度
        [Range(0.0f, 1.0f)]
        [SerializeField, HideInInspector] private float radius = 0.2f;           // 光源付近のノイズ減衰半径

        [Header("Strobe Visual Settings")]
        [SerializeField, Min(1f)] private float strobeIntensityMultiplier = 10.0f;   // フルチャージ時の輝度倍率
        [SerializeField, Min(1f)] private float strobeVolumetricMultiplier = 4.0f;   // フルチャージ時の散乱倍率
        [SerializeField, Min(0f)] private float strobeSpotAngleBoost = 20.0f;        // フルチャージ時に広げる照射角

        [Header("Strobe Camera Flash")]
        [SerializeField, Min(0f)] private float cameraFlashIntensity = 400.0f;       // カメラ位置で一瞬光る点光源の光度(cd)
        [SerializeField, Min(0.1f)] private float cameraFlashRange = 10.0f;          // 点光源の届く距離
        [SerializeField, Min(0.01f)] private float screenFlashDuration = 0.3f;       // 閃光が消えるまでの秒数
        [SerializeField] private float screenFlashExposure = 0.8f;                   // 閃光時の露出補正(EV)
        [SerializeField, Range(0f, 1f)] private float screenFlashLift = 0.06f;       // 閃光時に暗部を白く持ち上げる量

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
        private HDAdditionalLightData _volumetricLight; // ボリュメトリックライトコンポーネント参照
        private float _baseIntensity = -1f;                 // ライトの基本輝度
        private float _baseVolumetricScattering = -1f;      // ボリュメトリックライトの基本散乱強度
        private float _nearWallGain = 1f;                  // 壁際の減光係数
        private float _strobeFactor;                      // 現在のストロボ発光係数
        private float _baseSpotAngle = -1f;               // ライトの基本照射角
        private Light _cameraFlashLight;                  // カメラ位置で光る閃光用の点光源
        private Volume _flashVolume;                      // 画面全体を白く飛ばす閃光用のボリューム
        private VolumeProfile _flashProfile;
        private float _flashStrength;                     // 閃光の強さ
        private float _flashTime;                         // 閃光開始からの経過秒数
        private StrobeZoomExtension _strobeZoom;          // チャージ中に画面中央へ寄せるズーム

        private static readonly System.Collections.Generic.List<FlashlightView> ActiveViews = new();
        private float _mistGain = 1f;
        private float _mistPhase;

        /// <summary>消灯能力から除外する懐中電灯を登録する</summary>
        private void OnEnable()
        {
            ActiveViews.Add(this);
        }

        /// <summary>懐中電灯の登録と一時的な減光を解除する</summary>
        private void OnDisable()
        {
            ActiveViews.Remove(this);
            _mistGain = 1f;
            _mistPhase = 0f;
            ApplyBeamIntensity();
        }

        /// <summary>光源が懐中電灯の照射光か判定する</summary>
        /// <param name="light">判定対象の光源</param>
        /// <returns>懐中電灯の光源ならtrue</returns>
        public static bool IsFlashlight(Light light)
        {
            foreach (var view in ActiveViews)
                if (view.flashlightLight == light) return true;
            return false;
        }

        /// <summary>黒い霧への接近に合わせて点滅を速くし、消灯時間も増やす</summary>
        private void UpdateMistFlicker()
        {
            float strength = BlackMistLightEffect.GetInterference(StrobeOrigin);
            if (strength <= 0f)
            {
                _mistPhase = 0f;
                _mistGain = 1f;
            }
            else
            {
                // 遠方では毎秒1回、至近距離では毎秒12回の点滅にする
                _mistPhase = Mathf.Repeat(_mistPhase + Time.deltaTime * Mathf.Lerp(1f, 12f, strength), 1f);
                _mistGain = _mistPhase < Mathf.Lerp(.15f, .65f, strength) ? 0f : 1f;
            }
            ApplyBeamIntensity();
        }

        public Camera TargetCamera => targetCamera != null ? targetCamera : Camera.main;
        /// <summary>腕と指の追従に使う懐中電灯の発光基準</summary>
        public Transform HeldLightTransform => flashlightLight != null ? flashlightLight.transform : null;
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

        /// <summary>
        /// ライトの基本強度と散乱倍率を保持する
        /// </summary>
        private void EnsureBaseValuesCached()
        {
            if (_baseIntensity < 0f && flashlightLight != null)
            {
                _baseIntensity = flashlightLight.intensity;
                _baseSpotAngle = flashlightLight.spotAngle;
            }

            if (_baseVolumetricScattering < 0f)
            {
                if (_volumetricLight != null)
                {
                    _baseVolumetricScattering = _volumetricLight.volumetricDimmer;
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
            if (flashlightLight != null)
            {
                _volumetricLight = flashlightLight.GetComponent<HDAdditionalLightData>();
                if (_volumetricLight == null)
                {
                    _volumetricLight = HdrpLightUtility.SetIntensity(flashlightLight,
                        flashlightLight.intensity, LightUnitUtils.GetNativeLightUnit(flashlightLight.type));
                }
            }

            /* 手持ちモデルと一人称の腕が自分の光源を遮らないようにする */
            foreach (Renderer renderer in GetSelfShadowRenderers()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            _baseVolumetricScattering = scattering;
            EnsureBaseValuesCached();
            
            if (flashlightLight != null)
            {
                flashlightLight.shadows = beamShadows;
                _currentFollowRotation = flashlightLight.transform.rotation;

                // ボリュメトリックライトコンポーネントの設定適用
                if (_volumetricLight != null)
                {
                    // 異方性はHDRPのFog Volumeで設定し光源では散乱倍率を調整する
                    _volumetricLight.volumetricDimmer = scattering;

                    // 初期有効状態をライトの点灯状態に同期
                    _volumetricLight.affectsVolumetric = enableVolumetric;
                }
            }
            else
            {
                _currentFollowRotation = transform.rotation;
            }

            CreateStrobeFlash();
        }

        /// <summary>
        /// ストロボ発光時の閃光に使う点光源とボリュームを用意する
        /// </summary>
        private void CreateStrobeFlash()
        {
            // カメラ位置から周囲を一瞬照らす点光源
            if (targetCamera != null)
            {
                var lightObject = new GameObject("StrobeCameraFlash");
                lightObject.transform.SetParent(targetCamera.transform, false);
                _cameraFlashLight = lightObject.AddComponent<Light>();
                _cameraFlashLight.type = LightType.Point;
                _cameraFlashLight.range = cameraFlashRange;
                _cameraFlashLight.shadows = LightShadows.None;
                _cameraFlashLight.color = new Color(0.92f, 0.96f, 1f);
                HdrpLightUtility.SetIntensity(_cameraFlashLight, 0f, LightUnit.Candela);
                _cameraFlashLight.enabled = false;
            }

            // 露出と暗部の持ち上げで画面全体を白く飛ばすボリューム
            var volumeObject = new GameObject("StrobeFlashVolume");
            volumeObject.transform.SetParent(transform, false);
            _flashVolume = volumeObject.AddComponent<Volume>();
            _flashVolume.isGlobal = true;
            _flashVolume.priority = 100f;
            _flashVolume.weight = 0f;
            _flashProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _flashProfile.Add<ColorAdjustments>().postExposure.Override(screenFlashExposure);
            _flashProfile.Add<LiftGammaGain>().lift.Override(new Vector4(1f, 1f, 1f, screenFlashLift));
            _flashVolume.sharedProfile = _flashProfile;
        }

        /// <summary>
        /// ストロボ発光の瞬間にカメラから閃光を出す
        /// </summary>
        /// <param name="charge">ストロボのチャージ量</param>
        public void PlayStrobeFlash(float charge)
        {
            _flashStrength = Mathf.Clamp01(charge);
            _flashTime = 0f;
        }

        /// <summary>
        /// チャージ量に応じて画面中央へズームさせる
        /// </summary>
        /// <param name="charge">ストロボのチャージ量</param>
        public void SetStrobeZoom(float charge)
        {
            // 視点を描いている仮想カメラにズーム用の拡張を付ける
            if (_strobeZoom == null && targetCamera != null
                && targetCamera.TryGetComponent(out CinemachineBrain brain)
                && brain.ActiveVirtualCamera is CinemachineVirtualCameraBase vcam)
            {
                if (!vcam.TryGetComponent(out _strobeZoom))
                {
                    _strobeZoom = vcam.gameObject.AddComponent<StrobeZoomExtension>();
                }
            }

            if (_strobeZoom != null) _strobeZoom.SetZoom(charge);
        }

        /// <summary>
        /// 閃光を一瞬で立ち上げ、急速に減衰させる
        /// </summary>
        private void UpdateStrobeFlash()
        {
            if (_flashStrength <= 0f) return;

            _flashTime += Time.deltaTime;
            float remain = 1f - _flashTime / screenFlashDuration;
            float curve = remain > 0f ? remain * remain * _flashStrength : 0f;
            if (curve <= 0f) _flashStrength = 0f;

            if (_flashVolume != null) _flashVolume.weight = curve;
            if (_cameraFlashLight != null)
            {
                _cameraFlashLight.enabled = curve > 0f;
                _cameraFlashLight.intensity = cameraFlashIntensity * curve;
            }
        }

        /// <summary>
        /// 実行時に作った閃光用のプロファイルを破棄する
        /// </summary>
        private void OnDestroy()
        {
            if (_flashProfile != null) Destroy(_flashProfile);
        }

        /// <summary>
        /// 手持ちモデルの位置とライトの追従回転を更新する
        /// </summary>
        private void LateUpdate()
        {
            UpdateMistFlicker();
            UpdateStrobeFlash();

            // モデル付きのライトは消灯中も視点に追従させる
            if (flashlightModel != null && flashlightLight != null && targetCamera != null)
            {
                flashlightLight.transform.position = targetCamera.transform.TransformPoint(modelLightOffset);
            }

            // ライトまたは手持ちモデルの回転をカメラに同期する
            if (flashlightLight != null && (flashlightLight.enabled || flashlightModel != null) && targetCamera != null)
            {
                // 左端の発光位置から画面中央の注視点へ照射する
                Quaternion targetRotation = GetBeamRotation();

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

        /// <summary>視線より少し下の注視点へ発光位置から照射する</summary>
        /// <returns>手振れを含まないライトの向き</returns>
        private Quaternion GetBeamRotation()
        {
            if (!useRealisticBeam) return targetCamera.transform.rotation;
            Vector3 direction = targetCamera.transform.rotation * Quaternion.Euler(beamDownwardAngle, 0f, 0f) * Vector3.forward;
            Vector3 focus = targetCamera.transform.position + direction * beamFocusDistance;
            return Quaternion.LookRotation(focus - flashlightLight.transform.position, targetCamera.transform.up);
        }

        /// <summary>自分の光源を遮ってはいけない手持ちモデルと一人称の体を列挙する</summary>
        /// <returns>影を落とさない描画部品</returns>
        public System.Collections.Generic.IEnumerable<Renderer> GetSelfShadowRenderers()
        {
            if (flashlightModel != null)
                foreach (Renderer renderer in flashlightModel.GetComponentsInChildren<Renderer>(true)) yield return renderer;
            PlayerView player = GetComponentInParent<PlayerView>();
            if (player != null)
                foreach (SkinnedMeshRenderer renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true)) yield return renderer;
        }

#if UNITY_EDITOR
        /// <summary>再生中の見た目確認だけに電池状態を無視した点灯を設定する</summary>
        /// <param name="enabled">強制点灯するか</param>
        public void SetLookDevLightOverride(bool enabled)
        {
            _lookDevForceLight = enabled;
            SetLightActive(enabled);
            ApplyBeamIntensity();
        }
#endif

        /// <summary>
        /// ライトのアクティブ状態を切り替える
        /// </summary>
        /// <param name="active">点灯する場合はtrue</param>
        public void SetLightActive(bool active)
        {
#if UNITY_EDITOR
            active |= _lookDevForceLight;
#endif
            if (flashlightLight != null)
            {
                flashlightLight.enabled = active;
                // シーンで指定した影品質を点灯後も維持する
                flashlightLight.shadows = beamShadows;

                if (active && targetCamera != null)
                {
                    // 点灯開始時に向きをカメラに同期させてワープを防ぐ
                    _currentFollowRotation = GetBeamRotation();
                    flashlightLight.transform.rotation = _currentFollowRotation;
                }

                // ボリュメトリックフォグへの寄与状態をライトと同期
                if (_volumetricLight != null)
                {
                    _volumetricLight.affectsVolumetric = enableVolumetric && active;
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
            float gain = (useRealisticBeam ? _nearWallGain : 1f) * _mistGain;

            // ストロボは通常光への倍率で強め、照射角も一時的に広げる
            if (flashlightLight != null)
            {
                float boost = Mathf.Lerp(1f, strobeIntensityMultiplier, _strobeFactor);
                flashlightLight.intensity = _baseIntensity * boost * gain;
                flashlightLight.spotAngle = Mathf.Min(_baseSpotAngle + strobeSpotAngleBoost * _strobeFactor, 179f);
            }

            if (_volumetricLight != null)
            {
                float boost = Mathf.Lerp(1f, strobeVolumetricMultiplier, _strobeFactor);
                _volumetricLight.volumetricDimmer = _baseVolumetricScattering * boost * gain;
            }
        }
    }
}
