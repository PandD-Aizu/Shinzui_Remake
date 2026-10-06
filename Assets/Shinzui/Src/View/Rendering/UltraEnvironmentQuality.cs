using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Shinzui.View.Rendering
{
    /// <summary>
    /// ULTRA選択中のトンネルへ高精細材質と局所的なHDR反射を適用する
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class UltraEnvironmentQuality : MonoBehaviour
    {
        private static UltraEnvironmentProfile _runtimeProfile;
        private readonly Dictionary<MeshRenderer, Material[]> _originalMaterials = new();
        private readonly List<MeshRenderer> _removedRenderers = new();
        private readonly List<Camera> _fogOverrideCameras = new();
        private UltraEnvironmentProfile _profile;
        private ReflectionProbe _probe;
        private bool _ultraActive;
        private bool _hasEnvironment;
        private float _nextMaterialScan;
        private float _nextReflectionCapture;
        private int _captureId = -1;
        private bool _originalFogEnabled;

        /// <summary>
        /// ULTRA用のパイプラインが現在適用されているかを返す
        /// </summary>
        public static bool IsActive => _runtimeProfile != null && _runtimeProfile.pipelineAsset != null
            && GraphicsSettings.currentRenderPipeline == _runtimeProfile.pipelineAsset;

        /// <summary>
        /// 現在の画質に対応する生成済み蛍光灯の光量倍率を返す
        /// </summary>
        public static float CeilingLightIntensityMultiplier => IsActive
            ? Mathf.Max(0f, _runtimeProfile.ceilingLightIntensityMultiplier) : 1f;

        /// <summary>
        /// 品質切り替えと遅延生成されるトンネルを監視する常駐オブジェクトを作成する
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var profile = Resources.Load<UltraEnvironmentProfile>("UltraEnvironmentProfile");
            _runtimeProfile = profile;
            if (profile == null || profile.pipelineAsset == null
                || profile.sourceMaterial == null || profile.ultraMaterial == null)
                return;

            // ドメインリロードを省略した再生でも常駐オブジェクトを重複させない
            if (FindFirstObjectByType<UltraEnvironmentQuality>(FindObjectsInactive.Include) != null)
                return;

            // シーンやプレハブを変更せずに品質専用の描画状態を管理する
            var host = new GameObject("ULTRA Environment Quality");
            DontDestroyOnLoad(host);
            var quality = host.AddComponent<UltraEnvironmentQuality>();
            quality._profile = profile;
        }

        /// <summary>
        /// シーン切り替え後の反射を再取得するため通知を購読する
        /// </summary>
        private void OnEnable()
        {
            // 再生中のスクリプト再読込でも非シリアライズ参照を復元する
            if (_profile == null)
                _profile = Resources.Load<UltraEnvironmentProfile>("UltraEnvironmentProfile");
            if (_runtimeProfile == null)
                _runtimeProfile = _profile;

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        /// <summary>
        /// 品質の変更と生成済み環境の描画設定を反映する
        /// </summary>
        private void LateUpdate()
        {
            if (_profile == null) return;

            // 実際に適用されたURPアセットを基準にし、他の画質へ設定を漏らさない
            bool ultra = IsActive;
            if (ultra != _ultraActive)
            {
                _ultraActive = ultra;
                if (!ultra) RestoreEnvironment();
                _nextMaterialScan = 0f;
            }

            if (!ultra) return;

            // 非同期ロードと再生成されたメッシュも同じマテリアルへ切り替える
            if (Time.unscaledTime >= _nextMaterialScan)
            {
                ApplyMaterials();
                _nextMaterialScan = Time.unscaledTime + 0.5f;
            }

            UpdateReflection();
        }

        /// <summary>
        /// 元のトンネル材質を使用する描画オブジェクトだけを置き換える
        /// </summary>
        private void ApplyMaterials()
        {
            // 再生成で破棄されたRendererへの参照を解放する
            _removedRenderers.Clear();
            foreach (var entry in _originalMaterials)
                if (entry.Key == null) _removedRenderers.Add(entry.Key);

            foreach (MeshRenderer renderer in _removedRenderers)
                _originalMaterials.Remove(renderer);

            _hasEnvironment = false;
            foreach (MeshRenderer renderer in FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == _profile.sourceMaterial)
                    {
                        if (!_originalMaterials.ContainsKey(renderer))
                            _originalMaterials.Add(renderer, (Material[])materials.Clone());

                        materials[i] = _profile.ultraMaterial;
                        changed = true;
                    }

                    if (materials[i] == _profile.ultraMaterial && renderer.enabled
                        && renderer.gameObject.activeInHierarchy)
                        _hasEnvironment = true;
                }

                if (changed) renderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// プレイヤー周辺の照明と材質をHDRキューブマップへ取り込む
        /// </summary>
        private void UpdateReflection()
        {
            Camera camera = Camera.main;
            if (!_hasEnvironment || camera == null || !camera.isActiveAndEnabled
                || camera.cameraType != CameraType.Game)
            {
                if (_probe != null) _probe.enabled = false;
                return;
            }

            // 黒い背景で実際の室内だけを撮影し、屋外の空を反射へ混ぜない
            if (_probe == null)
            {
                var probeObject = new GameObject("ULTRA Local Reflection");
                probeObject.transform.SetParent(transform, false);
                _probe = probeObject.AddComponent<ReflectionProbe>();
                _probe.mode = ReflectionProbeMode.Realtime;
                _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                _probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                _probe.resolution = Mathf.Clamp(Mathf.NextPowerOfTwo(_profile.reflectionResolution), 128, 2048);
                _probe.hdr = true;
                _probe.boxProjection = true;
                _probe.size = _profile.reflectionBoxSize;
                _probe.blendDistance = 3f;
                _probe.intensity = _profile.reflectionIntensity;
                _probe.importance = 10;
                _probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
                _probe.backgroundColor = Color.black;
                _probe.nearClipPlane = 0.1f;
                _probe.farClipPlane = Mathf.Max(32f, _profile.reflectionBoxSize.magnitude);
                _probe.shadowDistance = _probe.farClipPlane;
                _nextReflectionCapture = 0f;
            }

            _probe.enabled = true;
            if (Time.unscaledTime < _nextReflectionCapture
                || (_captureId >= 0 && !_probe.IsFinishedRendering(_captureId)))
                return;

            // 反射カメラにはURPのポストプロセスが適用されず、ピクセル化前の光を保持する
            _probe.transform.position = camera.transform.position;
            _probe.cullingMask = camera.cullingMask;
            _captureId = _probe.RenderProbe();
            _nextReflectionCapture = Time.unscaledTime + _profile.reflectionRefreshSeconds;
        }

        /// <summary>
        /// シーン変更後に材質と反射を再取得する
        /// </summary>
        /// <param name="previous">以前のアクティブシーン</param>
        /// <param name="current">新しいアクティブシーン</param>
        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            _nextMaterialScan = 0f;
            _nextReflectionCapture = 0f;
            if (_probe != null) _probe.enabled = false;
        }

        /// <summary>
        /// ULTRAの室内描画では旧式の黒い距離フォグを止め、体積霧の二重適用を避ける
        /// </summary>
        /// <param name="context">描画コンテキスト</param>
        /// <param name="camera">描画を開始するカメラ</param>
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!IsActive || !_hasEnvironment
                || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.Reflection))
                return;

            // カメラ描画の間だけ変更し、各シーン固有の霧設定を保持する
            if (_fogOverrideCameras.Count == 0) _originalFogEnabled = RenderSettings.fog;
            _fogOverrideCameras.Add(camera);
            RenderSettings.fog = false;
        }

        /// <summary>
        /// 反射やポータルを含むカメラ描画が終わったら元の距離フォグへ戻す
        /// </summary>
        /// <param name="context">描画コンテキスト</param>
        /// <param name="camera">描画を終了したカメラ</param>
        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            int index = _fogOverrideCameras.LastIndexOf(camera);
            if (index < 0) return;

            _fogOverrideCameras.RemoveAt(index);
            if (_fogOverrideCameras.Count == 0) RenderSettings.fog = _originalFogEnabled;
        }

        /// <summary>
        /// ULTRAで置き換えたスロットだけを復元して反射用リソースを破棄する
        /// </summary>
        private void RestoreEnvironment()
        {
            // 描画中に監視が停止した場合もシーンの霧設定を復元する
            if (_fogOverrideCameras.Count > 0)
            {
                RenderSettings.fog = _originalFogEnabled;
                _fogOverrideCameras.Clear();
            }

            foreach (var entry in _originalMaterials)
            {
                if (entry.Key == null) continue;

                // 他のゲーム処理で変更された材質は上書きしない
                Material[] materials = entry.Key.sharedMaterials;
                for (int i = 0; i < Mathf.Min(materials.Length, entry.Value.Length); i++)
                    if (materials[i] == _profile.ultraMaterial) materials[i] = entry.Value[i];

                entry.Key.sharedMaterials = materials;
            }

            _originalMaterials.Clear();
            _hasEnvironment = false;
            if (_probe != null)
            {
                _probe.enabled = false;
                Destroy(_probe.gameObject);
                _probe = null;
            }

            _captureId = -1;
        }

        /// <summary>
        /// 監視の終了時に実行中の材質変更と通知購読を解除する
        /// </summary>
        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            if (_profile != null) RestoreEnvironment();
            _ultraActive = false;
        }
    }
}
