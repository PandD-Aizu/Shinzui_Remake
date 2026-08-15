using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.View
{
    /// <summary>
    /// トンネルの端点のViewクラス
    /// URP環境において、RenderTexture投影方式によるステンシルマスク付きポータル表現をセットアップ
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TunnelGateView : MonoBehaviour
    {
        [Header("Warp Configuration")]
        [Tooltip("対になるもう一方の端点のゲート（ワープ先）。")]
        [SerializeField] private TunnelGateView targetGate;

        [Header("Portal Rendering Settings")]
        [Tooltip("ニアクリップ面のオフセット量。")]
        [SerializeField] private float nearClipOffset = 0.04f;

        [Header("Portal Exposure")]
        [Tooltip("ポータル像の露出補正（EV値）。合成前のScene-Linear HDR段階で乗算されます。")]
        [SerializeField, Range(-2.0f, 4.0f)] private float portalExposureEV = 2.0f;

        [Header("Debug / Adjustments")]
        [Tooltip("ニアクリップの動的調整を適用して、ポータル手前の余計な壁をクリップするかどうか。")]
        [SerializeField] private bool useDynamicNearClip = true;

        [Header("Material Templates")]
        [SerializeField] private Material portalMaterialTemplate;
        [SerializeField] private Material maskMaterialTemplate;

        private Collider _collider;
        private Camera _mainCamera;
        private Camera _portalCamera;
        private MeshRenderer _portalRenderer;
        private MeshRenderer _portalMaskRenderer;
        private RenderTexture _portalRT;
        private RenderTexture[] _recursionRTs;
        private Mesh _portalMesh;

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

        private static FieldInfo _rendererIndexField;

        // 全アクティブなゲートのリスト
        public static readonly List<TunnelGateView> ActiveGates = new();

        public TunnelGateView TargetGate => targetGate;
        public Collider Collider => _collider;
        public MeshRenderer PortalRenderer => _portalRenderer;
        public RenderTexture PortalRT => _portalRT;
        public float PortalExposureMultiplier => Mathf.Pow(2.0f, portalExposureEV);

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
            CreateRenderTexture();
            UpdateMaterialExposure();
        }

        private void Update()
        {
            if (targetGate == null) return;

            // ポータル用オブジェクトの存在を保証する
            InitializePortalResources();

            UpdateMaterialExposure();

            _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            // 画面サイズ変更の追従
            int targetWidth = _mainCamera.pixelWidth;
            int targetHeight = _mainCamera.pixelHeight;

            if (_portalRT == null || _portalRT.width != targetWidth || _portalRT.height != targetHeight)
            {
                CreateRenderTexture();
            }
        }

        private void UpdateMaterialExposure()
        {
            if (_portalRenderer != null && _portalRenderer.sharedMaterial != null)
            {
                if (!Mathf.Approximately(_lastAppliedExposureEV, portalExposureEV))
                {
                    _portalRenderer.sharedMaterial.SetFloat(PortalExposureMultiplierId, PortalExposureMultiplier);
                    _lastAppliedExposureEV = portalExposureEV;
                }
            }
        }

        private void OnDestroy()
        {
            ReleasePortalResources();
        }

        private const int MaxRecursionDepth = 2; // 再帰深度

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (targetGate == null) return;

            // ポータルカメラ自身のレンダリング開始時：
            // URPによる自動的な射影行列の再計算を上書きし、フェイルセーフな斜め射影ニアクリップ面を適用する
            if (camera == _portalCamera)
            {
                if (useDynamicNearClip)
                {
                    SetObliqueNearClipPlane(_portalCamera, targetGate.transform);
                }
                else
                {
                    RestoreDefaultProjection(_portalCamera);
                }
                return;
            }

            // メインカメラのレンダリング開始時のみ、メインカメラから可視のゲートだけ再帰ポータル描画をトリガー
            if (camera == Camera.main)
            {
                _mainCamera = camera;
                if (IsVisibleFromMainCamera(_mainCamera))
                {
                    RenderPortalRecursive(context, MaxRecursionDepth);
                }
            }
        }

        private bool IsVisibleFromMainCamera(Camera mainCam)
        {
            if (mainCam == null) return false;

            Vector3 gateCenter = transform.position;
            if (_collider is BoxCollider box)
            {
                gateCenter = transform.TransformPoint(box.center);
            }

            // カメラがゲートの近傍（15m以内）にある場合は、通過中や斜め進入時のちらつき防止のため常に描画更新
            Vector3 camPos = mainCam.transform.position;
            float distSq = (camPos - gateCenter).sqrMagnitude;
            if (distSq < 225.0f)
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
            return GeometryUtility.TestPlanesAABB(_frustumPlanesScratch, targetBounds);
        }

        private void ReleasePortalResources()
        {
            ReleaseRecursionRenderTextures();

            if (_portalRT != null)
            {
                _portalRT.Release();
                Destroy(_portalRT);
                _portalRT = null;
            }

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
        }

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

            var targetData = _portalCamera.GetComponent<UniversalAdditionalCameraData>();
            if (targetData == null)
            {
                targetData = _portalCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            targetData.renderType = CameraRenderType.Base;
            targetData.renderPostProcessing = false; // ポストプロセスの二重適用を防ぐため無効にする
            targetData.antialiasing = AntialiasingMode.None;
        }

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

            _portalRT = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
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

            // URP で実際にカリングパラメータが生成可能かを検証
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
                portalCamera.projectionMatrix = _mainCamera.projectionMatrix;
            }
        }

        private static int GetRendererIndex(UniversalAdditionalCameraData data)
        {
            if (data == null) return -1;

            if (_rendererIndexField == null)
            {
                _rendererIndexField = typeof(UniversalAdditionalCameraData).GetField(
                    "m_RendererIndex",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (_rendererIndexField != null)
            {
                return (int)_rendererIndexField.GetValue(data);
            }
            return -1;
        }

        private void CopyCameraSettings(Camera source, Camera target)
        {
            RenderTexture rt = target.targetTexture;
            target.CopyFrom(source);
            target.enabled = false; // 再帰描画中は自動レンダリングさせない
            target.targetTexture = rt;
            target.farClipPlane = Mathf.Max(source.farClipPlane, 150f);

            // 背景はスカイボックスではなく黒の単色でクリアする
            target.clearFlags = CameraClearFlags.SolidColor;
            target.backgroundColor = Color.black;

            // プレイヤーレイヤーを除外する
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer != -1)
            {
                target.cullingMask = target.cullingMask & ~(1 << playerLayer);
            }

            var sourceData = source.GetComponent<UniversalAdditionalCameraData>();
            var targetData = target.GetComponent<UniversalAdditionalCameraData>();
            if (sourceData != null)
            {
                if (targetData == null)
                {
                    targetData = target.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }
                // ポストプロセスの二重適用を防ぐため無効
                targetData.renderPostProcessing = false;
                targetData.renderShadows = sourceData.renderShadows;
                targetData.antialiasing = sourceData.antialiasing;
                targetData.antialiasingQuality = sourceData.antialiasingQuality;
                targetData.volumeLayerMask = sourceData.volumeLayerMask;
                // Volumeのブレンド判定基準を実際の仮想視点位置にある portalCamera.transform に設定
                targetData.volumeTrigger = target.transform;

                // メインカメラのRenderer Indexと同期（-1の場合は明示指定せずDefault Rendererを使用）
                int rendererIndex = GetRendererIndex(sourceData);
                if (rendererIndex >= 0)
                {
                    targetData.SetRenderer(rendererIndex);
                }
            }
        }

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
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }
                mat = new Material(shader) { name = $"PortalProjectionMaterial_{gameObject.name}" };
            }
            mat.SetFloat(PortalExposureMultiplierId, PortalExposureMultiplier);
            _lastAppliedExposureEV = portalExposureEV;
            return mat;
        }

        private Material CreatePortalMaskMaterial()
        {
            if (maskMaterialTemplate != null)
            {
                return new Material(maskMaterialTemplate) { name = $"PortalMaskMaterial_{gameObject.name}" };
            }
            var shader = Shader.Find("Custom/PortalMask");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
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
                _recursionRTs[i] = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
                {
                    name = $"Portal_RecursionRT_{gameObject.name}_{i}",
                    useMipMap = false
                };
                _recursionRTs[i].Create();
            }
        }

        private void ReleaseRecursionRenderTextures()
        {
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
                    if (l != null && l.type != LightType.Directional && l.lightmapBakeType != LightmapBakeType.Baked)
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
                    if (l != null && l.type != LightType.Directional && l.lightmapBakeType != LightmapBakeType.Baked)
                    {
                        if (!_candidateLights.Contains(l))
                        {
                            _candidateLights.Add(l);
                        }
                    }
                }
            }
        }

        private void RenderPortalRecursive(ScriptableRenderContext context, int depth)
        {
            // レンダリング直前にもオンデマンド初期化を実行し、オブジェクトの生存を確実に
            InitializePortalResources();

            if (depth <= 0 || _portalCamera == null || targetGate == null) return;

            // カメラ設定のコピー
            CopyCameraSettings(_mainCamera, _portalCamera);

            int width = _portalRT != null ? _portalRT.width : Screen.width;
            int height = _portalRT != null ? _portalRT.height : Screen.height;
            if (width <= 0) width = 1024;
            if (height <= 0) height = 576;

            EnsureRecursionRenderTextures(depth, width, height);

            // プレイヤー/カメラ配下のローカルライト一覧を更新
            RefreshCandidateLights();

            // 奥から順番にレンダリングする
            for (int i = depth - 1; i >= 0; i--)
            {
                int currentLevel = i + 1;
                // 再帰レベルに応じてカメラ位置を奥にシフトし、正しいドロステ効果を生成
                MatchPortalCameraTransformForDepth(_portalCamera.transform, transform, targetGate.transform, currentLevel);

                if (useDynamicNearClip)
                {
                    SetObliqueNearClipPlane(_portalCamera, targetGate.transform);
                }
                else
                {
                    RestoreDefaultProjection(_portalCamera);
                }

                _portalCamera.targetTexture = _recursionRTs[i];

                if (i < depth - 1)
                {
                    // 1つ奥のレベルのRTを、自分自身の投影マテリアルに適用
                    if (PortalRenderer != null && PortalRenderer.sharedMaterial != null)
                    {
                        PortalRenderer.sharedMaterial.SetTexture("_MainTex", _recursionRTs[i + 1]);
                    }
                }
                else
                {
                    // 最深部は前フレームの映像による時間差にじみを防ぐため、
                    // クリアカラーである黒を貼り、奥の暗闇に自然に溶け込ませます
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

                    // URPの機能でカメラを明示的に即時描画
                    UniversalRenderPipeline.RenderSingleCamera(context, _portalCamera);
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

            // 最終結果（最前面レベル0の描画結果）をメインの RenderTexture にコピー
            if (_portalRT != null && _recursionRTs != null && _recursionRTs.Length > 0 && _recursionRTs[0] != null)
            {
                Graphics.Blit(_recursionRTs[0], _portalRT);
            }

            // 自分自身のマテリアル設定を本来のメインRTに戻す
            if (PortalRenderer != null && PortalRenderer.sharedMaterial != null)
            {
                PortalRenderer.sharedMaterial.SetTexture("_MainTex", _portalRT);
            }
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