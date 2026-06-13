using UnityEngine;

namespace Shinzui.View.Flashlight
{
    public class FlashlightView : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Light flashlightLight;         // スポットライト等のLightコンポーネント
        [SerializeField] private FMODUnity.EventReference toggleEvent; // スイッチON/OFF時のFMODイベント
        [SerializeField] private Camera targetCamera;           // 追従対象のカメラ

        public FMODUnity.EventReference ToggleEvent => toggleEvent;

        [Header("Tracking Ease Settings")]
        [SerializeField] private float minSpeed = 2.0f;         // 目標接近時の最低追従速度（最後ゆっくり）
        [SerializeField] private float maxSpeed = 45.0f;        // 動き始めの最高追従速度（最初急に）
        [SerializeField] private float maxAngle = 30.0f;        // 最大速度になる角度差の閾値

        [Header("Sway (Hand Shake) Settings")]
        [SerializeField] private bool enableSway = true;        // 手振れを有効にするか
        [SerializeField] private float swaySpeed = 1.8f;        // 手振れの周期速度
        [SerializeField] private float swayAmount = 0.8f;       // 手振れの最大角度幅

        [Header("Volumetric Light Settings")]
        [SerializeField] private bool enableVolumetric = true;  // ボリュメトリック効果（光の軌跡）を有効にするか
        [Range(-1.0f, 1.0f)]
        [SerializeField] private float anisotropy = 0.25f;      // 前方散乱の度合い (光の広がり)
        [Range(0.0f, 16.0f)]
        [SerializeField] private float scattering = 2.0f;       // 散乱強度 (軌跡の明るさ、強めに設定)
        [Range(0.0f, 1.0f)]
        [SerializeField] private float radius = 0.2f;           // 光源付近のノイズ減衰半径

        private Quaternion _currentFollowRotation;              // 手振れを含まない純粋な追従回転キャッシュ
        private VolumetricAdditionalLight _volumetricLight;      // ボリュメトリックライトコンポーネント参照

        private void Start()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            // バグ回避：GPU Resident Drawer (Occlusion Culling)の平面計算クラッシュを防ぐため、影をオフにします。
            if (flashlightLight != null)
            {
                flashlightLight.shadows = LightShadows.None;
                _currentFollowRotation = flashlightLight.transform.rotation;

                // ボリュメトリックライトコンポーネントを自動セットアップ
                if (enableVolumetric)
                {
                    _volumetricLight = flashlightLight.GetComponent<VolumetricAdditionalLight>();
                    if (_volumetricLight == null)
                    {
                        _volumetricLight = flashlightLight.gameObject.AddComponent<VolumetricAdditionalLight>();
                    }

                    // 設定パラメータの適用
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

        private void LateUpdate()
        {
            // ライトが有効な間、カメラの回転と同じ向きに同期する
            if (flashlightLight != null && flashlightLight.enabled && targetCamera != null)
            {
                // キャッシュされている回転とターゲットカメラの回転の角度差（0〜180度）を計算
                float angleDiff = Quaternion.Angle(_currentFollowRotation, targetCamera.transform.rotation);
                
                // 角度差を0〜1に正規化 (角度差が大きいほど1に近づく)
                float t = Mathf.Clamp01(angleDiff / maxAngle);
                
                // イージングカーブ (Ease-Out Cubic):
                // 角度差が大きい時は高速を維持し、目標に非常に近づいた時に急激に減速する
                float easeOutT = 1.0f - Mathf.Pow(1.0f - t, 3.0f);
                float currentSpeed = Mathf.Lerp(minSpeed, maxSpeed, easeOutT);

                // 計算された可変速度でSlerp補間を行い、基本追従回転を更新
                _currentFollowRotation = Quaternion.Slerp(
                    _currentFollowRotation,
                    targetCamera.transform.rotation,
                    Time.deltaTime * currentSpeed
                );

                // 基本追従回転に手振れ（微小なローカル回転揺れ）を加算して最終的なライトの姿勢にする
                if (enableSway)
                {
                    float time = Time.time * swaySpeed;
                    // ノイズでX軸(ピッチ)とY軸(ヨー)の揺れ角を計算 (-swayAmount 〜 +swayAmount)
                    float shakeX = (Mathf.PerlinNoise(time, 0.0f) - 0.5f) * swayAmount;
                    float shakeY = (Mathf.PerlinNoise(0.0f, time) - 0.5f) * swayAmount;
                    
                    flashlightLight.transform.rotation = _currentFollowRotation * Quaternion.Euler(shakeX, shakeY, 0f);
                }
                else
                {
                    flashlightLight.transform.rotation = _currentFollowRotation;
                }
            }
        }

        /// <summary>
        /// ライトのアクティブ状態を切り替える
        /// </summary>
        public void SetLightActive(bool active)
        {
            if (flashlightLight != null)
            {
                flashlightLight.enabled = active;
                // ライトON時にも影を強制オフにしてクラッシュを防止
                flashlightLight.shadows = LightShadows.None;

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
    }
}
