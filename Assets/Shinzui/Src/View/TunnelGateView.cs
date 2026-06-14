using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shinzui.View
{
    /// <summary>
    /// トンネルの端点（ゲート）のViewクラス。
    /// URP環境において、RenderTexture投影方式によるステンシルマスク付きポータル表現をセットアップします。
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
        private Mesh _portalMesh;

        // 全アクティブなゲートのリスト（動的生成に対応）
        public static readonly List<TunnelGateView> ActiveGates = new();

        public TunnelGateView TargetGate => targetGate;
        public Collider Collider => _collider;
        public MeshRenderer PortalRenderer => _portalRenderer;
        public RenderTexture PortalRT => _portalRT;

        // ポストプロセスコンポーネントの一時退避用
        private class PostProcessState
        {
            public VolumeComponent component;
            public bool originalActive;
        }
        private readonly List<PostProcessState> _tempDisabledComponents = new();

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
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            ActiveGates.Remove(this);
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            ReleasePortalResources();
        }

        private void Start()
        {
            // Startでの直接の初期化は行わず、UpdateやOnBeginCameraRenderingの際にオンデマンドで初期化します。
            // これにより、OnDisable/OnEnableなどの切り替えによってポータルオブジェクトが消失するのを防ぎます。
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
        }

        private void Update()
        {
            if (targetGate == null) return;
            
            // ポータル用オブジェクトの存在を保証する
            InitializePortalResources();
            
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

        private void OnDestroy()
        {
            ReleasePortalResources();
        }

        private const int MaxRecursionDepth = 2; // 再帰深度

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (targetGate == null) return;

            // 1. ポータルカメラ自身のレンダリング開始時：
            // URPによる自動的な射影行列の再計算を上書きし、斜め射影ニアクリップ面を強制適用します。
            // これにより、ポータル手前にあるオブジェクトの回り込みや、再帰時の不要なオクルージョンを防ぎます。
            if (camera == _portalCamera)
            {
                if (useDynamicNearClip)
                {
                    AdjustPortalCameraNearClip(_portalCamera, targetGate.transform);
                }
                // ポータルカメラのレンダリング開始時に一時的にポストプロセスを無効化
                DisablePostProcessingForPortal();
                return;
            }

            // 2. メインカメラのレンダリング開始時のみ、再帰ポータル描画をトリガー
            if (camera == Camera.main)
            {
                _mainCamera = camera;
                RenderPortalRecursive(context, MaxRecursionDepth);
            }
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _portalCamera)
            {
                // ポータルカメラのレンダリング終了時にポストプロセスを復元
                RestorePostProcessingForPortal();
            }
        }

        private void ReleasePortalResources()
        {
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
            quadObject.layer = 2; // Ignore Raycast

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
            maskObject.layer = 2; // Ignore Raycast

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
            targetData.renderPostProcessing = true; // Volumetric Fogの描画に必要
            targetData.antialiasing = AntialiasingMode.None;
            targetData.SetRenderer(0);
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

        private void AdjustPortalCameraNearClip(Camera portalCamera, Transform destinationPortal)
        {
            if (_mainCamera == null) return;

            // ゲート面はZ軸に垂直であるため、射影行列を歪ませるOblique Matrixは使用せず、
            // 通常のニアクリップ平面（nearClipPlane）をゲート位置に設定することで、歪みのない完璧なパースペクティブを維持します。
            
            // カメラからゲート平面への距離を計算（カメラ前方方向への投影距離）
            Vector3 toGate = destinationPortal.position - portalCamera.transform.position;
            float distanceToPlane = Vector3.Dot(toGate, portalCamera.transform.forward);

            // ゲート自身が描画されるよう、nearClipOffsetだけ手前にニアクリップ面を設定します
            portalCamera.nearClipPlane = Mathf.Max(0.01f, distanceToPlane - nearClipOffset);
            
            // ニアクリップの変更に基づき、正しい射影行列を自動計算させます（上書きは不要）
            portalCamera.ResetProjectionMatrix();
        }

        private void CopyCameraSettings(Camera source, Camera target)
        {
            RenderTexture rt = target.targetTexture;
            target.CopyFrom(source);
            target.enabled = false; // 再帰描画中は自動レンダリングさせない
            target.targetTexture = rt;
            target.farClipPlane = Mathf.Max(source.farClipPlane, 150f);
            
            // 背景はスカイボックスではなく黒の単色でクリアする（トンネルの奥の暗闇を表現）
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
                // Volumetric Fogの描画に必要だが、二重Bloomを防ぐため他のポストプロセスは描画直前に一時的に無効化します
                targetData.renderPostProcessing = true;
                targetData.renderShadows = sourceData.renderShadows;
                targetData.antialiasing = sourceData.antialiasing;
                targetData.antialiasingQuality = sourceData.antialiasingQuality;
                targetData.volumeLayerMask = sourceData.volumeLayerMask;
                targetData.volumeTrigger = sourceData.volumeTrigger != null ? sourceData.volumeTrigger : source.transform;
                targetData.SetRenderer(0);
            }
        }

        private void DisablePostProcessingForPortal()
        {
            _tempDisabledComponents.Clear();
            var stack = VolumeManager.instance.stack;
            if (stack == null) return;

            DisableComponentIfActive<Bloom>(stack);
            DisableComponentIfActive<Tonemapping>(stack);
            DisableComponentIfActive<ColorAdjustments>(stack);
            DisableComponentIfActive<WhiteBalance>(stack);
            DisableComponentIfActive<LiftGammaGain>(stack);
            DisableComponentIfActive<ShadowsMidtonesHighlights>(stack);
            DisableComponentIfActive<SplitToning>(stack);
            DisableComponentIfActive<ChromaticAberration>(stack);
            DisableComponentIfActive<Vignette>(stack);
            DisableComponentIfActive<FilmGrain>(stack);
            DisableComponentIfActive<MotionBlur>(stack);
            DisableComponentIfActive<DepthOfField>(stack);
            DisableComponentIfActive<LensDistortion>(stack);
            DisableComponentIfActive<PaniniProjection>(stack);
            DisableComponentIfActive<ScreenSpaceLensFlare>(stack);
        }

        private void DisableComponentIfActive<T>(VolumeStack stack) where T : VolumeComponent
        {
            var component = stack.GetComponent<T>();
            if (component != null && component.active)
            {
                _tempDisabledComponents.Add(new PostProcessState
                {
                    component = component,
                    originalActive = component.active
                });
                component.active = false;
            }
        }

        private void RestorePostProcessingForPortal()
        {
            for (int i = 0; i < _tempDisabledComponents.Count; i++)
            {
                var state = _tempDisabledComponents[i];
                if (state.component != null)
                {
                    state.component.active = state.originalActive;
                }
            }
            _tempDisabledComponents.Clear();
        }

        private Material CreatePortalMaterial()
        {
            if (portalMaterialTemplate != null)
            {
                return new Material(portalMaterialTemplate) { name = $"PortalProjectionMaterial_{gameObject.name}" };
            }
            var shader = Shader.Find("Custom/PortalProjection");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            return new Material(shader) { name = $"PortalProjectionMaterial_{gameObject.name}" };
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

        private void RenderPortalRecursive(ScriptableRenderContext context, int depth)
        {
            // レンダリング直前にもオンデマンド初期化を実行し、オブジェクトの生存を確実にする
            InitializePortalResources();

            if (depth <= 0 || _portalCamera == null || targetGate == null) return;

            // 1. カメラ設定のコピー
            CopyCameraSettings(_mainCamera, _portalCamera);

            // 2. 各再帰レベルごとの一時的な RenderTexture (RT) を用意
            RenderTexture[] tempRTs = new RenderTexture[depth];
            int width = _portalRT != null ? _portalRT.width : Screen.width;
            int height = _portalRT != null ? _portalRT.height : Screen.height;

            for (int i = 0; i < depth; i++)
            {
                tempRTs[i] = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.DefaultHDR);
            }

            // 3. 奥から順番にレンダリングする
            for (int i = depth - 1; i >= 0; i--)
            {
                int currentLevel = i + 1;
                // 再帰レベルに応じてカメラ位置を奥にシフトし、正しいドロステ効果（縮小ネスト）を生成します。
                // 最深部（クリアカラー黒）がトンネルの奥を塞ぐため、トンネルモデルがないことによる空の露出は防がれます。
                MatchPortalCameraTransformForDepth(_portalCamera.transform, transform, targetGate.transform, currentLevel);

                if (useDynamicNearClip)
                {
                    AdjustPortalCameraNearClip(_portalCamera, targetGate.transform);
                }
                else
                {
                    _portalCamera.projectionMatrix = _mainCamera.projectionMatrix;
                }

                _portalCamera.targetTexture = tempRTs[i];

                if (i < depth - 1)
                {
                    // 1つ奥のレベルのRTを、自分自身の投影マテリアルにアプライ
                    // （ポータルカメラから見て奥に写っているのは自分自身のゲートであるため）
                    if (PortalRenderer != null && PortalRenderer.sharedMaterial != null)
                    {
                        PortalRenderer.sharedMaterial.SetTexture("_MainTex", tempRTs[i + 1]);
                    }
                }
                else
                {
                    // 最深部は黒で潰すのではなく、自分自身の前フレームのメインRT（_portalRT）を貼ることで、
                    // スカイボックス（空）が見えるのを防ぎ、かつ奥へと吸い込まれるような無限フィードバック映像（ドロステ効果）にします
                    if (PortalRenderer != null && PortalRenderer.sharedMaterial != null)
                    {
                        PortalRenderer.sharedMaterial.SetTexture("_MainTex", _portalRT != null ? _portalRT : Texture2D.blackTexture);
                    }
                }

                // 相手側ゲートを一時的に非アクティブ化して描画の写り込み（手前の遮蔽）を防ぐ
                bool targetGateWasActive = targetGate._portalRenderer != null && targetGate._portalRenderer.enabled;
                bool targetMaskWasActive = targetGate._portalMaskRenderer != null && targetGate._portalMaskRenderer.enabled;

                if (targetGate._portalRenderer != null) targetGate._portalRenderer.enabled = false;
                if (targetGate._portalMaskRenderer != null) targetGate._portalMaskRenderer.enabled = false;

                // URP の機能でカメラを明示的に即時描画
                UniversalRenderPipeline.RenderSingleCamera(context, _portalCamera);

                // 描画後に表示状態を復元します
                if (targetGate._portalRenderer != null) targetGate._portalRenderer.enabled = targetGateWasActive;
                if (targetGate._portalMaskRenderer != null) targetGate._portalMaskRenderer.enabled = targetMaskWasActive;
            }

            // 4. 最終結果をメインの RenderTexture にコピー
            if (_portalRT != null && tempRTs[0] != null)
            {
                Graphics.Blit(tempRTs[0], _portalRT);
            }

            // 自分自身のマテリアル設定を本来のメインRTに戻す
            if (PortalRenderer != null && PortalRenderer.sharedMaterial != null)
            {
                PortalRenderer.sharedMaterial.SetTexture("_MainTex", _portalRT);
            }

            // 5. テンポラリRTの解放
            for (int i = 0; i < depth; i++)
            {
                RenderTexture.ReleaseTemporary(tempRTs[i]);
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
