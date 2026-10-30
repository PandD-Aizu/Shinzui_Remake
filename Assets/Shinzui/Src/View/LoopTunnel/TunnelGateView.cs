using System;
using System.Collections.Generic;
using Shinzui.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.View
{
    /// <summary>
    /// トンネルの端点のViewクラス
    /// HDRP環境において、RenderTexture投影方式によるステンシルマスク付きポータル表現をセットアップ
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [RequireComponent(typeof(Collider))]
    public class TunnelGateView : MonoBehaviour
    {
        [Header("Warp Configuration")]
        [Tooltip("対になるもう一方の端点のゲート（ワープ先）。")]
        [SerializeField] private TunnelGateView targetGate;

        [Header("Portal Rendering Settings")]
        [Tooltip("ニアクリップ面のオフセット量。")]
        [SerializeField] private float nearClipOffset = 0.04f;

        /* 既存シーンの互換性のためフィールドは保持し補正値は描画に使わない */
        [SerializeField, HideInInspector] private float portalExposureEV = 0.0f;

        [Header("Debug / Adjustments")]
        [Tooltip("ニアクリップの動的調整を適用して、ポータル手前の余計な壁をクリップするかどうか。")]
        [SerializeField] private bool useDynamicNearClip = true;

        [Header("Material Templates")]
        [SerializeField] private Material portalMaterialTemplate;
        [SerializeField] private Material maskMaterialTemplate;

        private Collider _collider;
        private Camera _mainCamera;
        private Camera _portalCamera;
        private readonly List<Camera> _recursionCameras = new();
        private MeshRenderer _portalRenderer;
        private MeshRenderer _portalMaskRenderer;
        private RenderTexture _portalRT;
        private RenderTexture[] _recursionRTs;
        private Mesh _portalMesh;
        private float _lastVisibleTime;

        // 露出倍率のキャッシュ
        private static readonly int PortalExposureMultiplierId = Shader.PropertyToID("_PortalExposureMultiplier");
        private float _lastAppliedExposureEV = float.NaN;

        // 視錐台カリング用キャッシュ（GC Alloc ゼロ）
        private static readonly Plane[] _frustumPlanesScratch = new Plane[6];

        // プレイヤー/カメラ配下のローカルライト追跡用キャッシュ
        private readonly List<Light> _candidateLights = new();
        private readonly List<Light> _tempLightScanList = new();
        private readonly Light[] _activeLights = new Light[16];
        private readonly Vector3[] _savedLightPositions = new Vector3[16];
        private readonly Quaternion[] _savedLightRotations = new Quaternion[16];
        private PlayerView _cachedPlayerView;
        private Transform _cachedPlayerRoot;
        private Transform _cachedCameraRoot;
        private float _lastLightScanTime;

        // 全アクティブなゲートのリスト
        public static readonly List<TunnelGateView> ActiveGates = new();

        public TunnelGateView TargetGate => targetGate;
        public Collider Collider => _collider;
        public MeshRenderer PortalRenderer => _portalRenderer;
        public RenderTexture PortalRT => _portalRT;
        public float PortalExposureMultiplier => 1f;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            if (_collider != null)
            {
                // プレイヤーが衝突して引っかかるのを防ぐため、自動でトリガー化
                _collider.isTrigger = true;
            }
        }

        private void OnEnable()
        {
            if (!ActiveGates.Contains(this))
            {
                ActiveGates.Add(this);
            }
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            ActiveGates.Remove(this);
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            ReleasePortalResources();
        }

        private void Start()
        {

        }

        private void OnValidate()
        {
            UpdateMaterialExposure();
        }

        /// <summary>
        /// 描画用カメラと開口を準備し、テクスチャの確保は可視になるまで遅延する
        /// </summary>
        private void InitializePortalResources()
        {
            if (targetGate == null) return;
            if (_portalCamera != null && _portalRenderer != null && _portalMaskRenderer != null && _portalMesh != null) return;

            // 古いゴミオブジェクトがあれば破棄
            CleanupLeftoverObjects();

            // 共有用のダブルサイドクアッドメッシュを生成
            float width = 10.0f;
            float height = 5.0f;
            if (_collider == null)
            {
                _collider = GetComponent<Collider>();
            }
            if (_collider is BoxCollider box)
            {
                // コライダーのサイズを基準にする
                width = box.size.x;
                height = box.size.y;
            }
            _portalMesh = CreateDoubleSidedQuad(width, height);

            BuildPortalMaskQuad(_portalMesh);
            BuildPortalQuad(_portalMesh);
            BuildPortalCamera();
            UpdateMaterialExposure();
        }

        /// <summary>
        /// 開口の表示設定を更新し、使われていない描画バッファを解放する
        /// </summary>
        private void Update()
        {
            if (targetGate == null) return;

            // ポータル用オブジェクトの存在を保証する
            InitializePortalResources();

            UpdateMaterialExposure();

            _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            // 非表示が続くゲートの描画バッファを解放し、視点移動中の再確保は抑える
            if (_portalRT != null && Time.unscaledTime - _lastVisibleTime > 1.0f)
            {
                ReleasePortalTextures();
            }
        }

        private void UpdateMaterialExposure()
        {
            if (_portalRenderer != null && _portalRenderer.sharedMaterial != null)
            {
                if (!Mathf.Approximately(_lastAppliedExposureEV, 0f))
                {
                    _portalRenderer.sharedMaterial.SetFloat(PortalExposureMultiplierId, PortalExposureMultiplier);
                    _lastAppliedExposureEV = 0f;
                }
            }
        }

        private void OnDestroy()
        {
            ReleasePortalResources();
        }

        private const int MaxRecursionDepth = 2; // 再帰深度

        /// <summary>
        /// メイン描画の開始前に可視ポータルの再帰画像を更新する
        /// </summary>
        private void LateUpdate()
        {
            // HDRPの描画ループ内でRenderRequestを再入させない
            _mainCamera = Camera.main;
            if (targetGate != null && _mainCamera != null && _mainCamera.isActiveAndEnabled
                && GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset
                && IsVisibleFromMainCamera(_mainCamera))
            {
                RenderPortalRecursive(MaxRecursionDepth);
            }
        }

        /// <summary>
        /// ポータルカメラの描画時にクリップ面を再適用する
        /// </summary>
        /// <param name="context">現在の描画コンテキスト</param>
        /// <param name="camera">描画対象カメラ</param>
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (targetGate == null) return;

            // ポータルカメラ自身のレンダリング開始時：
            // HDRPによる自動的な射影行列の再計算を上書きし、フェイルセーフな斜め射影ニアクリップ面を適用する
            if (camera == _portalCamera || _recursionCameras.Contains(camera))
            {
                if (useDynamicNearClip)
                {
                    SetObliqueNearClipPlane(camera, targetGate.transform);
                }
                else
                {
                    RestoreDefaultProjection(camera);
                }
                return;
            }

        }

        /// <summary>
        /// 視錐台とステージの遮蔽からポータルの更新要否を判定する
        /// </summary>
        /// <param name="mainCam">表示先のメインカメラ</param>
        /// <returns>ポータルの一部が見える可能性がある場合はtrue</returns>
        private bool IsVisibleFromMainCamera(Camera mainCam)
        {
            if (mainCam == null) return false;

            Vector3 gateCenter = transform.position;
            if (_collider is BoxCollider box)
            {
                gateCenter = transform.TransformPoint(box.center);
            }

            // 通過直前だけ遮蔽判定を省き、壁越しの広い範囲を常時更新しない
            Vector3 camPos = mainCam.transform.position;
            float distSq = (camPos - gateCenter).sqrMagnitude;
            if (distSq < 1.0f)
            {
                return true;
            }

            // メインカメラの視錐台による判定（PortalRenderer.isVisibleに依存せずメインカメラのみで判定）
            Bounds targetBounds;
            if (_collider != null)
            {
                targetBounds = _collider.bounds;
            }
            else if (_portalRenderer != null)
            {
                targetBounds = _portalRenderer.bounds;
            }
            else
            {
                targetBounds = new Bounds(gateCenter, Vector3.one * 5.0f);
            }

            // エッジ付近でのクリップ漏れ・ちらつきを防ぐためBoundsを適度に拡張
            targetBounds.Expand(3.0f);

            GeometryUtility.CalculateFrustumPlanes(mainCam, _frustumPlanesScratch);
            if (!GeometryUtility.TestPlanesAABB(_frustumPlanesScratch, targetBounds)) return false;

            // 開口の中央と端を含む格子を調べ、一点でも遮られていなければ更新する
            if (!(_collider is BoxCollider portalBox)) return true;
            int wallMask = LayerMask.GetMask("Stage") & mainCam.cullingMask;
            if (wallMask == 0) return true;

            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    Vector3 localPoint = portalBox.center + new Vector3(
                        (x / 4f - 0.5f) * portalBox.size.x,
                        (y / 4f - 0.5f) * portalBox.size.y, 0f);
                    Vector3 point = transform.TransformPoint(localPoint);
                    Vector3 direction = point - camPos;
                    float distance = direction.magnitude;

                    // ニア面をまたぐ開口は保守的に描画して通過時の欠けを防ぐ
                    if (mainCam.WorldToViewportPoint(point).z <= mainCam.nearClipPlane) return true;
                    if (!Physics.Raycast(camPos, direction.normalized, distance - 0.05f,
                            wallMask, QueryTriggerInteraction.Ignore)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// カメラと開口を残して表示用と再帰用のテクスチャを解放する
        /// </summary>
        private void ReleasePortalTextures()
        {
            if (_portalCamera != null) _portalCamera.targetTexture = null;
            if (_portalRenderer != null && _portalRenderer.sharedMaterial != null)
            {
                _portalRenderer.sharedMaterial.SetTexture("_MainTex", Texture2D.blackTexture);
            }

            ReleaseRecursionRenderTextures();
            if (_portalRT == null) return;
            _portalRT.Release();
            Destroy(_portalRT);
            _portalRT = null;
        }

        /// <summary>
        /// ポータルの描画リソースをすべて解放する
        /// </summary>
        private void ReleasePortalResources()
        {
            ReleasePortalTextures();

            foreach (Camera camera in _recursionCameras)
                if (camera != null) Destroy(camera.gameObject);
            _recursionCameras.Clear();
            if (_portalCamera != null)
            {
                Destroy(_portalCamera.gameObject);
                _portalCamera = null;
            }

            if (_portalRenderer != null)
            {
                if (_portalRenderer.sharedMaterial != null)
                {
                    Destroy(_portalRenderer.sharedMaterial);
                }
                Destroy(_portalRenderer.gameObject);
                _portalRenderer = null;
            }

            if (_portalMaskRenderer != null)
            {
                if (_portalMaskRenderer.sharedMaterial != null)
                {
                    Destroy(_portalMaskRenderer.sharedMaterial);
                }
                Destroy(_portalMaskRenderer.gameObject);
                _portalMaskRenderer = null;
            }

            if (_portalMesh != null)
            {
                Destroy(_portalMesh);
                _portalMesh = null;
            }
        }

        private void CleanupLeftoverObjects()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.Contains("[Portal")
                    || child.name.Contains("[Portal Camera]")
                    || child.name.Contains("[Portal Mask Quad]")
                    || child.name.Contains("[Portal Quad]"))
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void BuildPortalQuad(Mesh sharedMesh)
        {
            var quadObject = new GameObject("[Portal Quad]");
            quadObject.transform.SetParent(transform, false);

            quadObject.transform.localPosition = Vector3.zero;
            quadObject.transform.localRotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
            quadObject.layer = LayerMask.NameToLayer("Portal");

            if (_collider is BoxCollider box)
            {
                quadObject.transform.localPosition = new Vector3(box.center.x, box.center.y, box.center.z);
            }

            var meshFilter = quadObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = sharedMesh;

            _portalRenderer = quadObject.AddComponent<MeshRenderer>();
            _portalRenderer.sharedMaterial = CreatePortalMaterial();
            _portalRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _portalRenderer.receiveShadows = false;
            _portalRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void BuildPortalMaskQuad(Mesh sharedMesh)
        {
            var maskObject = new GameObject("[Portal Mask Quad]");
            maskObject.transform.SetParent(transform, false);

            maskObject.transform.localPosition = Vector3.zero;
            maskObject.transform.localRotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
            maskObject.layer = 2;

            if (_collider is BoxCollider box)
            {
                maskObject.transform.localPosition = new Vector3(box.center.x, box.center.y, box.center.z);
            }

            var meshFilter = maskObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = sharedMesh;

            _portalMaskRenderer = maskObject.AddComponent<MeshRenderer>();
            _portalMaskRenderer.sharedMaterial = CreatePortalMaskMaterial();
            _portalMaskRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _portalMaskRenderer.receiveShadows = false;
            _portalMaskRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        /// <summary>
        /// 手動描画するポータルカメラを生成する
        /// </summary>
        private void BuildPortalCamera()
        {
            var cameraObject = new GameObject("[Portal Camera]");
            cameraObject.transform.SetParent(transform, false);

            _portalCamera = cameraObject.AddComponent<Camera>();
            _portalCamera.enabled = false; // 手動でレンダリングするため、自動レンダリングはオフにする
            _portalCamera.depth = -100;
            _portalCamera.clearFlags = CameraClearFlags.SolidColor;
            _portalCamera.backgroundColor = Color.black;
            _portalCamera.useOcclusionCulling = false;

            // 手動描画の初期状態ではポスト処理を無効にする
            HdrpCameraUtility.SetPostProcessing(_portalCamera, false);
        }

        /// <summary>
        /// 可視ポータルの表示先をメインカメラの解像度に合わせて確保する
        /// </summary>
        private void CreateRenderTexture()
        {
            if (_portalRT != null)
            {
                _portalRT.Release();
                Destroy(_portalRT);
            }

            _mainCamera = Camera.main;
            int width = _mainCamera != null ? _mainCamera.pixelWidth : Screen.width;
            int height = _mainCamera != null ? _mainCamera.pixelHeight : Screen.height;

            if (width <= 0) width = 1024;
            if (height <= 0) height = 576;

            _portalRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
            {
                name = $"PortalRT_{gameObject.name}",
                useMipMap = false
            };
            _portalRT.Create();

            if (_portalCamera != null)
            {
                _portalCamera.targetTexture = _portalRT;
            }

            if (_portalRenderer != null && _portalRenderer.sharedMaterial != null)
            {
                _portalRenderer.sharedMaterial.SetTexture("_MainTex", _portalRT);
            }
        }

        private void MatchPortalCameraTransform(Transform cameraTransform, Transform source, Transform destination)
        {
            Vector3 offset = destination.position - source.position;
            cameraTransform.position = _mainCamera.transform.position + offset;
            cameraTransform.rotation = _mainCamera.transform.rotation;
        }

        /// <summary>
        /// 出口面より手前の物体を除外する斜めクリップ面を設定する
        /// </summary>
        /// <param name="portalCamera">ポータルカメラ</param>
        /// <param name="destinationPortal">出口の平面</param>
        private void SetObliqueNearClipPlane(Camera portalCamera, Transform destinationPortal)
        {
            // まずベースのプロジェクション行列を確定（メインカメラの通常プロジェクションに一致させる）
            RestoreDefaultProjection(portalCamera);

            if (destinationPortal == null)
            {
                return;
            }

            // destination portal の位置と前方ベクトル
            Vector3 planePos = destinationPortal.position;
            if (destinationPortal.GetComponent<Collider>() is BoxCollider box)
            {
                planePos = destinationPortal.TransformPoint(box.center);
            }

            Vector3 planeNormal = destinationPortal.forward;

            // カメラからポータル平面への相対ベクトル
            Vector3 camToPlane = planePos - portalCamera.transform.position;

            // カメラの前方方向に対して destination plane が十分前方にあるかを検証
            // （平面がカメラの背後や近傍にある場合は、射影の歪みやクリップ破綻を防ぐため通常プロジェクションへ戻す）
            float forwardDist = Vector3.Dot(portalCamera.transform.forward, camToPlane);
            float minNearDist = portalCamera.nearClipPlane > 0f ? portalCamera.nearClipPlane + 0.01f : 0.05f;
            if (forwardDist <= minNearDist)
            {
                RestoreDefaultProjection(portalCamera);
                return;
            }

            float dot = Vector3.Dot(planeNormal, camToPlane);

            // カメラが平面に極端に近い、または平行な場合は安全のため通常プロジェクションのままとする
            if (Mathf.Abs(dot) < 0.01f)
            {
                return;
            }

            // sideSign: カメラがポータルの手前（dot > 0）か奥（dot < 0）か
            float sideSign = Mathf.Sign(dot);

            // クリップ位置: destinationPortal.forward 基準で一定方向（ポータル手前側）に微小オフセットし、
            // ポータル面自身の描画が near clip で欠けないようにする
            Vector3 clipPos = planePos - planeNormal * nearClipOffset;

            // カメラ空間（View Matrix）への変換
            Matrix4x4 worldToCam = portalCamera.worldToCameraMatrix;
            Vector3 cpos = worldToCam.MultiplyPoint(clipPos);
            Vector3 cnormal = worldToCam.MultiplyVector(planeNormal).normalized * sideSign;

            float cdistance = -Vector3.Dot(cpos, cnormal);
            Vector4 cameraSpacePlane = new Vector4(cnormal.x, cnormal.y, cnormal.z, cdistance);

            // NaN や Infinity のチェック
            if (float.IsNaN(cameraSpacePlane.x) || float.IsNaN(cameraSpacePlane.y) || float.IsNaN(cameraSpacePlane.z) || float.IsNaN(cameraSpacePlane.w) ||
                float.IsInfinity(cameraSpacePlane.x) || float.IsInfinity(cameraSpacePlane.y) || float.IsInfinity(cameraSpacePlane.z) || float.IsInfinity(cameraSpacePlane.w))
            {
                RestoreDefaultProjection(portalCamera);
                return;
            }

            Matrix4x4 obliqueMatrix = portalCamera.CalculateObliqueMatrix(cameraSpacePlane);

            // 計算結果の行列が有限値であることを検証
            for (int i = 0; i < 16; i++)
            {
                float val = obliqueMatrix[i];
                if (float.IsNaN(val) || float.IsInfinity(val))
                {
                    RestoreDefaultProjection(portalCamera);
                    return;
                }
            }

            // 候補の oblique 射影行列を適用
            portalCamera.projectionMatrix = obliqueMatrix;

            // HDRPで実際にカリングパラメータが生成可能かを検証
            // （Screen position out of view frustum 等の射影破綻エラーを確実に防ぐ）
            if (!portalCamera.TryGetCullingParameters(false, out _))
            {
                RestoreDefaultProjection(portalCamera);
            }
        }

        private void RestoreDefaultProjection(Camera portalCamera)
        {
            if (portalCamera == null) return;
            portalCamera.ResetProjectionMatrix();
            if (_mainCamera != null)
            {
                portalCamera.projectionMatrix = _mainCamera.nonJitteredProjectionMatrix;
            }
        }

        /// <summary>
        /// メインカメラの設定を引き継ぎポータルに映さないレイヤーを除外
        /// </summary>
        /// <param name="source">メインカメラ</param>
        /// <param name="target">ポータルカメラ</param>
        private void CopyCameraSettings(Camera source, Camera target)
        {
            RenderTexture rt = target.targetTexture;
            target.CopyFrom(source);
            // ブラックホール敵の見た目をポータルの再帰描画から除外
            int hiddenLayer = LayerMask.NameToLayer("PortalHidden");
            if (hiddenLayer >= 0) target.cullingMask &= ~(1 << hiddenLayer);

            target.enabled = false; // 再帰描画中は自動レンダリングさせない
            target.targetTexture = rt;
            target.farClipPlane = Mathf.Max(source.farClipPlane, 150f);
            target.projectionMatrix = source.nonJitteredProjectionMatrix;

            // 背景はスカイボックスではなく黒の単色でクリアする
            target.clearFlags = CameraClearFlags.SolidColor;
            target.backgroundColor = Color.black;

            // プレイヤーレイヤーを除外する
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer != -1)
            {
                target.cullingMask = target.cullingMask & ~(1 << playerLayer);
            }

            /* カメラに追従するVolumeを含め主視点と同じブレンド結果を使用する */
            var sourceData = HdrpCameraUtility.GetOrAdd(source);
            var targetData = HdrpCameraUtility.CopyForPortal(source, target,
                sourceData.volumeAnchorOverride != null ? sourceData.volumeAnchorOverride : source.transform);
            targetData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            targetData.backgroundColorHDR = Color.black;
        }

        /// <summary>
        /// ポータルの表示マテリアルを作成し、未描画の開口は黒で初期化する
        /// </summary>
        /// <returns>ゲート専用の表示マテリアル</returns>
        private Material CreatePortalMaterial()
        {
            Material mat;
            if (portalMaterialTemplate != null)
            {
                mat = new Material(portalMaterialTemplate) { name = $"PortalProjectionMaterial_{gameObject.name}" };
            }
            else
            {
                var shader = Shader.Find("Custom/PortalProjection");
                if (shader == null)
                {
                    shader = Shader.Find("HDRP/Unlit");
                }
                mat = new Material(shader) { name = $"PortalProjectionMaterial_{gameObject.name}" };
            }
            // 初回描画前に別のポータルから見えた場合の白い面を防ぐ
            mat.SetTexture("_MainTex", Texture2D.blackTexture);
            mat.SetFloat(PortalExposureMultiplierId, PortalExposureMultiplier);
            _lastAppliedExposureEV = 0f;
            return mat;
        }

        /// <summary>
        /// ポータル開口のマスク用マテリアルを生成する
        /// </summary>
        /// <returns>生成したマテリアル</returns>
        private Material CreatePortalMaskMaterial()
        {
            if (maskMaterialTemplate != null)
            {
                return new Material(maskMaterialTemplate) { name = $"PortalMaskMaterial_{gameObject.name}" };
            }
            var shader = Shader.Find("Custom/PortalMask");
            if (shader == null)
            {
                shader = Shader.Find("HDRP/Unlit");
            }
            return new Material(shader) { name = $"PortalMaskMaterial_{gameObject.name}" };
        }

        private Mesh CreateDoubleSidedQuad(float width, float height)
        {
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            var mesh = new Mesh
            {
                name = "Portal_Quad_Mesh",
                vertices = new[]
                {
                    new Vector3(-halfWidth, -halfHeight, 0.0f),
                    new Vector3(halfWidth, -halfHeight, 0.0f),
                    new Vector3(halfWidth, halfHeight, 0.0f),
                    new Vector3(-halfWidth, halfHeight, 0.0f)
                },
                uv = new[]
                {
                    new Vector2(0.0f, 0.0f),
                    new Vector2(1.0f, 0.0f),
                    new Vector2(1.0f, 1.0f),
                    new Vector2(0.0f, 1.0f)
                },
                triangles = new[]
                {
                    0, 3, 2,
                    0, 2, 1,
                    0, 2, 3,
                    0, 1, 2
                }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 再帰段数と解像度に合わせてHDR描画先を確保する
        /// </summary>
        /// <param name="depth">再帰段数</param>
        /// <param name="width">画像の幅</param>
        /// <param name="height">画像の高さ</param>
        private void EnsureRecursionRenderTextures(int depth, int width, int height)
        {
            if (_recursionRTs != null && _recursionRTs.Length == depth)
            {
                bool valid = true;
                for (int i = 0; i < depth; i++)
                {
                    if (_recursionRTs[i] == null || _recursionRTs[i].width != width || _recursionRTs[i].height != height || !_recursionRTs[i].IsCreated())
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid) return;
            }

            ReleaseRecursionRenderTextures();

            _recursionRTs = new RenderTexture[depth];
            for (int i = 0; i < depth; i++)
            {
                _recursionRTs[i] = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
                {
                    name = $"Portal_RecursionRT_{gameObject.name}_{i}",
                    useMipMap = false
                };
                _recursionRTs[i].Create();
            }
        }

        private void ReleaseRecursionRenderTextures()
        {
            foreach (Camera camera in _recursionCameras)
                if (camera != null) camera.targetTexture = null;
            if (_recursionRTs != null)
            {
                for (int i = 0; i < _recursionRTs.Length; i++)
                {
                    if (_recursionRTs[i] != null)
                    {
                        _recursionRTs[i].Release();
                        Destroy(_recursionRTs[i]);
                        _recursionRTs[i] = null;
                    }
                }
                _recursionRTs = null;
            }
        }

        private static bool IsBakedLight(Light light)
        {
#if UNITY_EDITOR
            return light.lightmapBakeType == LightmapBakeType.Baked;
#else
            // The authoring property is editor-only; players retain the baked output metadata.
            return light.bakingOutput.lightmapBakeType == LightmapBakeType.Baked;
#endif
        }

        private void RefreshCandidateLights()
        {
            float now = Time.time;
            if (now - _lastLightScanTime < 1.0f && _cachedPlayerRoot != null && _cachedCameraRoot != null)
            {
                return;
            }
            _lastLightScanTime = now;

            _candidateLights.Clear();

            // PlayerView を検索・キャッシュ（PlayerView は View レイヤー内なので直接参照可能）
            if (_cachedPlayerView == null)
            {
                _cachedPlayerView = FindFirstObjectByType<PlayerView>();
            }

            Transform playerRoot = _cachedPlayerView != null ? _cachedPlayerView.transform : null;
            Transform cameraRoot = _mainCamera != null ? _mainCamera.transform.root : null;

            _cachedPlayerRoot = playerRoot;
            _cachedCameraRoot = cameraRoot;

            // 主検索ルート: PlayerView 配下（FlashlightView, Spot Light等）
            if (playerRoot != null)
            {
                _tempLightScanList.Clear();
                playerRoot.GetComponentsInChildren(true, _tempLightScanList);
                for (int i = 0; i < _tempLightScanList.Count; i++)
                {
                    Light l = _tempLightScanList[i];
                    if (l != null && l.type != LightType.Directional && !IsBakedLight(l))
                    {
                        if (!_candidateLights.Contains(l))
                        {
                            _candidateLights.Add(l);
                        }
                    }
                }
            }

            // 副検索ルート: Main Camera のルート（Player と別系統の場合）
            if (cameraRoot != null && cameraRoot != playerRoot)
            {
                _tempLightScanList.Clear();
                cameraRoot.GetComponentsInChildren(true, _tempLightScanList);
                for (int i = 0; i < _tempLightScanList.Count; i++)
                {
                    Light l = _tempLightScanList[i];
                    if (l != null && l.type != LightType.Directional && !IsBakedLight(l))
                    {
                        if (!_candidateLights.Contains(l))
                        {
                            _candidateLights.Add(l);
                        }
                    }
                }
            }
        }

        /// <summary>再帰段ごとに独立した照明履歴を持つカメラを再利用する</summary>
        /// <param name="level">ゼロ始まりの再帰段</param>
        /// <returns>描画先カメラ</returns>
        private Camera GetRecursionCamera(int level)
        {
            if (level == 0) return _portalCamera;
            while (_recursionCameras.Count < level)
            {
                var holder = new GameObject($"Portal Recursion Camera {_recursionCameras.Count + 1}");
                holder.transform.SetParent(transform, false);
                Camera camera = holder.AddComponent<Camera>();
                camera.enabled = false;
                _recursionCameras.Add(camera);
            }
            return _recursionCameras[level - 1];
        }

        /// <summary>
        /// ポータル内の再帰像を奥から描画し、最前面を表示用テクスチャへ直接出力する
        /// </summary>
        /// <param name="depth">ポータルの再帰描画回数</param>
        private void RenderPortalRecursive(int depth)
        {
            // レンダリング直前にもオンデマンド初期化を実行し、オブジェクトの生存を確実に
            InitializePortalResources();

            if (depth <= 0 || _portalCamera == null || targetGate == null) return;

            // 可視と判定されたフレームだけ表示バッファを確保する
            _lastVisibleTime = Time.unscaledTime;
            if (_portalRT == null || !_portalRT.IsCreated()
                || _portalRT.width != Mathf.Max(1, _mainCamera.pixelWidth)
                || _portalRT.height != Mathf.Max(1, _mainCamera.pixelHeight))
            {
                CreateRenderTexture();
            }

            // カメラ設定のコピー
            CopyCameraSettings(_mainCamera, _portalCamera);

            int width = _portalRT != null ? _portalRT.width : Screen.width;
            int height = _portalRT != null ? _portalRT.height : Screen.height;
            if (width <= 0) width = 1024;
            if (height <= 0) height = 576;

            // 奥の再帰像は縦横半分にして描画負荷とメモリを抑える
            EnsureRecursionRenderTextures(depth - 1, Mathf.Max(1, width / 2), Mathf.Max(1, height / 2));

            // ポータル面が占める画面範囲だけを各再帰カメラのカリング対象にする
            Matrix4x4 portalCrop = GetPortalCullingCrop();

            // プレイヤー/カメラ配下のローカルライト一覧を更新
            RefreshCandidateLights();

            // 奥から順番にレンダリングする
            for (int i = depth - 1; i >= 0; i--)
            {
                int currentLevel = i + 1;
                Camera renderCamera = GetRecursionCamera(i);
                CopyCameraSettings(_mainCamera, renderCamera);
                // 再帰レベルに応じてカメラ位置を奥にシフトし、正しいドロステ効果を生成
                MatchPortalCameraTransformForDepth(renderCamera.transform, transform, targetGate.transform, currentLevel);

                if (useDynamicNearClip)
                {
                    SetObliqueNearClipPlane(renderCamera, targetGate.transform);
                }
                else
                {
                    RestoreDefaultProjection(renderCamera);
                }

                renderCamera.cullingMatrix = portalCrop * renderCamera.projectionMatrix
                    * renderCamera.worldToCameraMatrix;
                renderCamera.targetTexture = i == 0 ? _portalRT : _recursionRTs[i - 1];

                if (i < depth - 1)
                {
                    // 1つ奥のレベルのRTを、自分自身の投影マテリアルに適用
                    if (PortalRenderer != null && PortalRenderer.sharedMaterial != null)
                    {
                        PortalRenderer.sharedMaterial.SetTexture("_MainTex", _recursionRTs[i]);
                    }
                }
                else
                {
                    // 最深部は前フレームの映像による時間差にじみを防ぐため、
                    // クリアカラーである黒を貼り、奥の暗闇に自然に溶け込ませる
                    if (PortalRenderer != null && PortalRenderer.sharedMaterial != null)
                    {
                        PortalRenderer.sharedMaterial.SetTexture("_MainTex", Texture2D.blackTexture);
                    }
                }

                // 相手側ゲートを一時的に非アクティブ化して描画の写り込みを防ぐ
                bool targetGateWasActive = targetGate._portalRenderer != null && targetGate._portalRenderer.enabled;
                bool targetMaskWasActive = targetGate._portalMaskRenderer != null && targetGate._portalMaskRenderer.enabled;

                if (targetGate._portalRenderer != null) targetGate._portalRenderer.enabled = false;
                if (targetGate._portalMaskRenderer != null) targetGate._portalMaskRenderer.enabled = false;

                int activeLightCount = 0;
                try
                {
                    // 再帰レベルに応じたポータル空間オフセットを計算
                    Vector3 singleOffset = targetGate.transform.position - transform.position;
                    Vector3 totalOffset = singleOffset * currentLevel;

                    // プレイヤー配下の有効な非Directionalライトを一時的にポータルカメラ空間へ移動
                    for (int l = 0; l < _candidateLights.Count; l++)
                    {
                        Light light = _candidateLights[l];
                        if (light != null && light.isActiveAndEnabled && light.type != LightType.Directional)
                        {
                            if (activeLightCount >= _activeLights.Length) break;

                            _savedLightPositions[activeLightCount] = light.transform.position;
                            _savedLightRotations[activeLightCount] = light.transform.rotation;
                            _activeLights[activeLightCount] = light;

                            light.transform.position = _savedLightPositions[activeLightCount] + totalOffset;
                            activeLightCount++;
                        }
                    }

                    // 最深部から同期描画し次の再帰段へ渡す
                    var request = new RenderPipeline.StandardRequest { destination = renderCamera.targetTexture };
                    RenderPipeline.SubmitRenderRequest(renderCamera, request);
                }
                finally
                {
                    // 移動したライトのワールド座標・回転を100%確実に復元（途中例外でも移動済み分のみ安全に復元）
                    for (int l = 0; l < activeLightCount; l++)
                    {
                        if (_activeLights[l] != null)
                        {
                            _activeLights[l].transform.position = _savedLightPositions[l];
                            _activeLights[l].transform.rotation = _savedLightRotations[l];
                            _activeLights[l] = null;
                        }
                    }

                    // 相手側ゲートの表示状態を確実に復元
                    if (targetGate._portalRenderer != null) targetGate._portalRenderer.enabled = targetGateWasActive;
                    if (targetGate._portalMaskRenderer != null) targetGate._portalMaskRenderer.enabled = targetMaskWasActive;
                }
            }

            // 自分自身のマテリアル設定を本来のメインRTに戻す
            if (PortalRenderer != null && PortalRenderer.sharedMaterial != null)
            {
                PortalRenderer.sharedMaterial.SetTexture("_MainTex", _portalRT);
            }
        }

        /// <summary>
        /// ポータル外へ描かれる物体とライトを除外するカリング用の射影補正を求める
        /// </summary>
        /// <returns>画面内のポータル領域をクリップ空間全体へ広げる行列</returns>
        private Matrix4x4 GetPortalCullingCrop()
        {
            // ニア面をまたぐ通過中は通常の視錐台を使い、画面端の欠けを防ぐ
            Bounds bounds = _portalRenderer.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector2 lower = Vector2.one;
            Vector2 upper = Vector2.zero;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 viewport = _mainCamera.WorldToViewportPoint(point);
                if (viewport.z <= _mainCamera.nearClipPlane) return Matrix4x4.identity;

                lower = Vector2.Min(lower, new Vector2(viewport.x, viewport.y));
                upper = Vector2.Max(upper, new Vector2(viewport.x, viewport.y));
            }

            // アンチエイリアス分の余白を残し、実際の描画用射影行列は変更しない
            float left = Mathf.Clamp01(lower.x - 2f / _portalRT.width);
            float right = Mathf.Clamp01(upper.x + 2f / _portalRT.width);
            float bottom = Mathf.Clamp01(lower.y - 2f / _portalRT.height);
            float top = Mathf.Clamp01(upper.y + 2f / _portalRT.height);
            if (right <= left || top <= bottom) return Matrix4x4.identity;

            Matrix4x4 crop = Matrix4x4.identity;
            crop.m00 = 1f / (right - left);
            crop.m03 = (1f - right - left) / (right - left);
            crop.m11 = 1f / (top - bottom);
            crop.m13 = (1f - top - bottom) / (top - bottom);
            return crop;
        }

        private void MatchPortalCameraTransformForDepth(Transform cameraTransform, Transform source, Transform destination, int depthLevel)
        {
            Vector3 singleOffset = destination.position - source.position;
            Vector3 totalOffset = singleOffset * depthLevel;
            cameraTransform.position = _mainCamera.transform.position + totalOffset;
            cameraTransform.rotation = _mainCamera.transform.rotation;
        }
    }
}
