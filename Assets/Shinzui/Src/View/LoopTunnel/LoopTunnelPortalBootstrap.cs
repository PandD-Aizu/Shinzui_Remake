using System.Collections;
using Unity.Cinemachine;
using Shinzui.Infrastructure.Rendering;
using Shinzui.Infrastructure.Lighting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shinzui.View.LoopTunnel
{
    [DefaultExecutionOrder(10000)]
    public sealed class LoopTunnelPortalBootstrap : MonoBehaviour
    {
        private const string TargetSceneName = "LoopTunnelTest";
        private const string InstanceName = "[LoopTunnel Portal Runtime]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapActiveScene()
        {
            TryCreateForScene(SceneManager.GetActiveScene());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoaded()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateForScene(scene);
        }

        private static void TryCreateForScene(Scene scene)
        {
            if (!scene.IsValid() || scene.name != TargetSceneName || GameObject.Find(InstanceName) != null)
            {
                return;
            }

            var runtimeObject = new GameObject(InstanceName);
            runtimeObject.AddComponent<LoopTunnelPortalRuntime>();
        }
    }

    [DefaultExecutionOrder(10000)]
    public sealed class LoopTunnelPortalRuntime : MonoBehaviour
    {
        [Header("Tunnel")]
        [SerializeField] private float tunnelLength = 60.0f;
        [SerializeField] private float tunnelWidth = 16.0f;
        [SerializeField] private float tunnelHeight = 10.0f;
        [SerializeField] private float visualPadding = 40.0f;
        [SerializeField] private int markerCount = 0;

        [Header("Portal Rendering")]
        [SerializeField] private float nearClipOffset = 0.04f;

        [Header("Warp Frame Bridge")]
        [Range(1, 6)]
        [SerializeField] private int warpBridgeFrameCount = 1;

        private Camera _mainCamera;
        private Camera _frontPortalCamera;
        private Camera _backPortalCamera;
        private Transform _frontPortal;
        private Transform _backPortal;
        private Renderer _frontPortalRenderer;
        private Renderer _backPortalRenderer;
        private RenderTexture _frontPortalTexture;
        private RenderTexture _backPortalTexture;
        private CharacterController _playerController;
        private Transform _player;
        private float _halfLength;
        private bool _cameraDistancesInitialized;
        private float _previousFrontCameraDistance;
        private float _previousBackCameraDistance;
        private Canvas _warpFrameCanvas;
        private RawImage _warpFrameImage;
        private RenderTexture _warpFrameTexture;
        private Coroutine _hideWarpFrameCoroutine;

        private void Awake()
        {
            _halfLength = tunnelLength * 0.5f;
            _mainCamera = Camera.main;
            _player = FindPlayerTransform();

            BuildTunnel();
            BuildPortals();
            BuildPortalCameras();
            BuildWarpFrameBridge();

            if (_player != null)
            {
                _playerController = _player.GetComponent<CharacterController>();
                ClampPlayerIntoTunnel();
            }
        }

        /// <summary>
        /// カメラ追従後にワープを処理しポータルをメイン描画の前に更新する
        /// </summary>
        private void LateUpdate()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null || !(GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset)) return;

            // HDRPのカリング開始前に視点と露出関連設定を確定する
            WrapPlayerIfNeeded();
            EnsureRenderTextures();
            if (_frontPortalRenderer != null && _frontPortalRenderer.isVisible && _frontPortalCamera != null)
                RenderPortal(_frontPortalCamera, _frontPortal, _backPortal);
            if (_backPortalRenderer != null && _backPortalRenderer.isVisible && _backPortalCamera != null)
                RenderPortal(_backPortalCamera, _backPortal, _frontPortal);
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture(_frontPortalTexture);
            ReleaseRenderTexture(_backPortalTexture);
            ReleaseRenderTexture(_warpFrameTexture);
        }

        private Transform FindPlayerTransform()
        {
            var playerView = FindFirstObjectByType<PlayerView>();
            if (playerView != null)
            {
                return playerView.transform;
            }

            var playerObject = GameObject.Find("Player");
            return playerObject != null ? playerObject.transform : null;
        }

        private void BuildTunnel()
        {
            var root = new GameObject("Loop Tunnel Geometry");
            root.transform.SetParent(transform, false);

            var wallMaterial = CreateMaterial("Loop Tunnel Wall", new Color(0.13f, 0.15f, 0.17f), 0.42f);
            var floorMaterial = CreateMaterial("Loop Tunnel Floor", new Color(0.05f, 0.055f, 0.06f), 0.55f);
            var stripeMaterial = CreateMaterial("Loop Tunnel Guide Lines", new Color(0.95f, 0.78f, 0.28f), 0.25f);

            float visualLength = tunnelLength + visualPadding * 2.0f;
            float visualHalfLength = visualLength * 0.5f;

            CreateCube(root.transform, "Floor", new Vector3(0.0f, 0.0f, 0.0f), new Vector3(tunnelWidth, 0.16f, visualLength), floorMaterial);
            CreateCube(root.transform, "Ceiling", new Vector3(0.0f, tunnelHeight, 0.0f), new Vector3(tunnelWidth, 0.16f, visualLength), wallMaterial);
            CreateCube(root.transform, "Left Wall", new Vector3(-tunnelWidth * 0.5f, tunnelHeight * 0.5f, 0.0f), new Vector3(0.16f, tunnelHeight, visualLength), wallMaterial);
            CreateCube(root.transform, "Right Wall", new Vector3(tunnelWidth * 0.5f, tunnelHeight * 0.5f, 0.0f), new Vector3(0.16f, tunnelHeight, visualLength), wallMaterial);

            for (int i = 0; i < markerCount; i++)
            {
                float z = Mathf.Lerp(-visualHalfLength + 1.0f, visualHalfLength - 1.0f, i / Mathf.Max(1.0f, markerCount - 1.0f));
                CreateCube(root.transform, "Depth Marker", new Vector3(-tunnelWidth * 0.5f + 0.09f, tunnelHeight * 0.5f, z), new Vector3(0.04f, tunnelHeight * 0.9f, 0.08f), stripeMaterial);
                CreateCube(root.transform, "Depth Marker Mirror", new Vector3(tunnelWidth * 0.5f - 0.09f, tunnelHeight * 0.5f, z), new Vector3(0.04f, tunnelHeight * 0.9f, 0.08f), stripeMaterial);
            }

            var lightObject = new GameObject("Loop Tunnel Fill Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0.0f, tunnelHeight - 0.4f, 0.0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.75f, 0.86f, 1.0f);
            HdrpLightUtility.SetIntensity(light, 600f, LightUnit.Lumen);
            light.range = tunnelLength;
            light.shadows = LightShadows.None;
        }

        private void BuildPortals()
        {
            var portalMaterialFront = CreatePortalMaterial("Front Portal View");
            var portalMaterialBack = CreatePortalMaterial("Back Portal View");

            _frontPortal = CreatePortal("Forward Portal", new Vector3(0.0f, tunnelHeight * 0.5f, _halfLength), Quaternion.Euler(0.0f, 180.0f, 0.0f), portalMaterialFront, out _frontPortalRenderer);
            _backPortal = CreatePortal("Back Portal", new Vector3(0.0f, tunnelHeight * 0.5f, -_halfLength), Quaternion.identity, portalMaterialBack, out _backPortalRenderer);

            var frameMaterial = CreateMaterial("Portal Frame", new Color(0.18f, 0.78f, 0.9f), 0.18f);
            BuildPortalFrame(_frontPortal, frameMaterial);
            BuildPortalFrame(_backPortal, frameMaterial);
        }

        private Transform CreatePortal(string name, Vector3 position, Quaternion rotation, Material material, out Renderer renderer)
        {
            var portalObject = new GameObject(name);
            portalObject.transform.SetParent(transform, false);
            portalObject.transform.SetPositionAndRotation(position, rotation);

            var meshFilter = portalObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateDoubleSidedQuad(tunnelWidth - 0.3f, tunnelHeight - 0.3f);

            renderer = portalObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return portalObject.transform;
        }

        private void BuildPortalFrame(Transform portal, Material material)
        {
            float frameWidth = tunnelWidth - 0.05f;
            float frameHeight = tunnelHeight - 0.05f;
            float z = portal.position.z;
            //CreateCube(portal, "CollisionCube", new Vector3(frameWidth * 0.5f, frameHeight * 0.5f, 0.0f), new Vector3(frameWidth, frameHeight, 1.02f), material);
            portal.position = new Vector3(portal.position.x, portal.position.y, z);
        }

        private void BuildPortalCameras()
        {
            _frontPortalCamera = CreatePortalCamera("Forward Portal Camera");
            _backPortalCamera = CreatePortalCamera("Back Portal Camera");
        }

        /// <summary>
        /// 手動描画専用のポータルカメラを生成する
        /// </summary>
        /// <param name="name">カメラ名</param>
        /// <returns>生成したカメラ</returns>
        private Camera CreatePortalCamera(string name)
        {
            var cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.depth = -100;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.useOcclusionCulling = false;
            return camera;
        }

        private void BuildWarpFrameBridge()
        {
            var canvasObject = new GameObject(
                "Warp Frame Bridge",
                typeof(RectTransform),
                typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);

            _warpFrameCanvas = canvasObject.GetComponent<Canvas>();
            _warpFrameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _warpFrameCanvas.sortingOrder = short.MaxValue;
            _warpFrameCanvas.enabled = false;

            var imageObject = new GameObject(
                "Portal Frame Image",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);

            var rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            _warpFrameImage = imageObject.GetComponent<RawImage>();
            _warpFrameImage.raycastTarget = false;
            _warpFrameImage.color = Color.white;
        }

        /// <summary>
        /// ポータル面を隠して単独のHDRP描画要求を実行する
        /// </summary>
        /// <param name="camera">ポータルカメラ</param>
        /// <param name="sourcePortal">入口</param>
        /// <param name="destinationPortal">出口</param>
        private void RenderPortal(Camera camera, Transform sourcePortal, Transform destinationPortal)
        {
            // HDRPでは全カメラのカリングが先行するため隠蔽状態を要求ごとに復元する
            PreparePortal(camera, sourcePortal, destinationPortal, _frontPortalRenderer, _backPortalRenderer);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,
                    new RenderPipeline.StandardRequest { destination = camera.targetTexture });
            }
            finally
            {
                RestorePortalRenderers(_frontPortalRenderer, _backPortalRenderer);
            }
        }

        private bool _frontWasEnabled;
        private bool _backWasEnabled;

        private void PreparePortal(Camera portalCamera, Transform sourcePortal, Transform destinationPortal, Renderer frontRenderer, Renderer backRenderer)
        {
            CopyCameraSettings(_mainCamera, portalCamera);
            MatchPortalCameraTransform(portalCamera.transform, sourcePortal, destinationPortal);
            ApplyObliqueClipPlane(portalCamera, destinationPortal);

            _frontWasEnabled = frontRenderer != null && frontRenderer.enabled;
            _backWasEnabled = backRenderer != null && backRenderer.enabled;

            if (frontRenderer != null)
            {
                frontRenderer.enabled = false;
            }
            if (backRenderer != null)
            {
                backRenderer.enabled = false;
            }
        }

        private void RestorePortalRenderers(Renderer frontRenderer, Renderer backRenderer)
        {
            if (frontRenderer != null)
            {
                frontRenderer.enabled = _frontWasEnabled;
            }
            if (backRenderer != null)
            {
                backRenderer.enabled = _backWasEnabled;
            }
        }

        private void MatchPortalCameraTransform(
            Transform cameraTransform,
            Transform sourcePortal,
            Transform destinationPortal)
        {
            Vector3 relativePos =
                sourcePortal.InverseTransformPoint(_mainCamera.transform.position);

            relativePos =
                Quaternion.Euler(0, 180, 0) * relativePos;

            cameraTransform.position =
                destinationPortal.TransformPoint(relativePos);

            Quaternion relativeRot =
                Quaternion.Inverse(sourcePortal.rotation)
                * _mainCamera.transform.rotation;

            relativeRot =
                Quaternion.Euler(0, 180, 0) * relativeRot;

            cameraTransform.rotation =
                destinationPortal.rotation * relativeRot;
        }

        private void ApplyObliqueClipPlane(Camera portalCamera, Transform destinationPortal)
        {
            Vector3 clipNormal = destinationPortal.forward;
            Vector3 clipPosition = destinationPortal.position + clipNormal * nearClipOffset;
            
            Matrix4x4 worldToCamera = Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f)) * portalCamera.transform.worldToLocalMatrix;

            Vector3 cameraPosition = worldToCamera.MultiplyPoint(clipPosition);
            Vector3 cameraNormal = worldToCamera.MultiplyVector(clipNormal).normalized;
            Vector4 clipPlane = new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
            portalCamera.projectionMatrix = portalCamera.CalculateObliqueMatrix(clipPlane);
        }

        /// <summary>
        /// メインカメラの設定を引き継ぎポータルに映さないレイヤーを除外
        /// </summary>
        /// <param name="source">メインカメラ</param>
        /// <param name="target">ポータルカメラ</param>
        private void CopyCameraSettings(Camera source, Camera target)
        {
            RenderTexture renderTexture = target.targetTexture;
            target.CopyFrom(source);
            // ブラックホール敵の見た目を両端のポータルカメラから除外
            int hiddenLayer = LayerMask.NameToLayer("PortalHidden");
            if (hiddenLayer >= 0) target.cullingMask &= ~(1 << hiddenLayer);

            target.enabled = false;
            target.targetTexture = renderTexture;
            target.stereoTargetEye = StereoTargetEyeMask.None;
            target.projectionMatrix = source.nonJitteredProjectionMatrix;
            target.farClipPlane = Mathf.Max(source.farClipPlane, tunnelLength * 2.5f);
            
            // 連続したトンネルのVolume判定をメイン視点へ固定する
            var sourceData = HdrpCameraUtility.GetOrAdd(source);
            HdrpCameraUtility.CopyForPortal(source, target,
                sourceData.volumeAnchorOverride != null ? sourceData.volumeAnchorOverride : source.transform);
        }

        private void WrapPlayerIfNeeded()
        {
            if (_player == null)
            {
                _player = FindPlayerTransform();
                if (_player == null)
                {
                    return;
                }

                _playerController = _player.GetComponent<CharacterController>();
            }

            if (_mainCamera == null || _frontPortal == null || _backPortal == null)
            {
                return;
            }

            float frontDistance = GetCameraDistanceFromPortal(_frontPortal);
            float backDistance = GetCameraDistanceFromPortal(_backPortal);

            // ワープ地点として認識する距離の閾値を設定
            float portalDeltaDistance = 0.1f;

            if (!_cameraDistancesInitialized)
            {
                SetPreviousCameraDistances(frontDistance, backDistance);
                _cameraDistancesInitialized = true;
                return;
            }

            if (_previousFrontCameraDistance > portalDeltaDistance && frontDistance <= portalDeltaDistance)
            {
                WarpPlayer(_frontPortal, _backPortal);
                return;
            }

            if (_previousBackCameraDistance > -portalDeltaDistance && backDistance <= -portalDeltaDistance)
            {
                WarpPlayer(_backPortal, _frontPortal);
                return;
            }

            SetPreviousCameraDistances(frontDistance, backDistance);
        }

        private float GetCameraDistanceFromPortal(Transform portal)
        {
            return Vector3.Dot(_mainCamera.transform.position - portal.position, portal.forward);
        }

        private void SetPreviousCameraDistances(float frontDistance, float backDistance)
        {
            _previousFrontCameraDistance = frontDistance;
            _previousBackCameraDistance = backDistance;
        }

        private void WarpPlayer(Transform sourcePortal, Transform destinationPortal)
        {
            Vector3 positionDelta = destinationPortal.position - sourcePortal.position;

            bool controllerWasEnabled = _playerController != null && _playerController.enabled;
            if (_playerController != null)
            {
                _playerController.enabled = false;
            }

            _player.position += positionDelta;

            if (_playerController != null)
            {
                _playerController.enabled = controllerWasEnabled;
            }

            NotifyCinemachineTargets(positionDelta);

            if (_mainCamera != null && !_mainCamera.transform.IsChildOf(_player))
            {
                _mainCamera.transform.position += positionDelta;
            }

            SetPreviousCameraDistances(
                GetCameraDistanceFromPortal(_frontPortal),
                GetCameraDistanceFromPortal(_backPortal));
        }

        private void NotifyCinemachineTargets(Vector3 positionDelta)
        {
            var targets = _player.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < targets.Length; i++)
            {
                CinemachineCore.OnTargetObjectWarped(targets[i], positionDelta);
            }
        }

        private void ClampPlayerIntoTunnel()
        {
            Vector3 position = _player.position;
            position.x = Mathf.Clamp(position.x, -tunnelWidth * 0.35f, tunnelWidth * 0.35f);
            position.z = Mathf.Repeat(position.z + _halfLength, tunnelLength) - _halfLength;
            _player.position = position;
        }

        private void EnsureRenderTextures()
        {
            int width = Mathf.Clamp(Mathf.RoundToInt(_mainCamera.pixelWidth), 128, 4096);
            int height = Mathf.Clamp(Mathf.RoundToInt(_mainCamera.pixelHeight), 128, 4096);

            if (_frontPortalTexture != null && _frontPortalTexture.width == width && _frontPortalTexture.height == height)
            {
                return;
            }

            ReleaseRenderTexture(_frontPortalTexture);
            ReleaseRenderTexture(_backPortalTexture);

            _frontPortalTexture = CreateRenderTexture("Forward Portal Texture", width, height);
            _backPortalTexture = CreateRenderTexture("Back Portal Texture", width, height);

            _frontPortalCamera.targetTexture = _frontPortalTexture;
            _backPortalCamera.targetTexture = _backPortalTexture;
            SetPortalTexture(_frontPortalRenderer.sharedMaterial, _frontPortalTexture);
            SetPortalTexture(_backPortalRenderer.sharedMaterial, _backPortalTexture);
        }

        private RenderTexture CreateRenderTexture(string name, int width, int height)
        {
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = name,
                antiAliasing = 2,
                useMipMap = false
            };
            texture.Create();
            return texture;
        }

        private static void ReleaseRenderTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            Destroy(texture);
        }

        /// <summary>
        /// 指定色のHDRPマテリアルを生成する
        /// </summary>
        /// <param name="name">生成物の名前</param>
        /// <param name="color">表示色</param>
        /// <param name="smoothness">表面の滑らかさ</param>
        /// <returns>生成したマテリアル</returns>
        private static Material CreateMaterial(string name, Color color, float smoothness)
        {
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                name = name,
                color = color
            };
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        /// <summary>
        /// ポータル表示用のマテリアルを生成する
        /// </summary>
        /// <param name="name">生成物の名前</param>
        /// <returns>生成したマテリアル</returns>
        private static Material CreatePortalMaterial(string name)
        {
            var shader = Shader.Find("Custom/PortalShader");
            if (shader == null)
            {
                shader = Shader.Find("HDRP/Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            var material = new Material(shader)
            {
                name = name
            };
            return material;
        }

        private static void SetPortalTexture(Material material, Texture texture)
        {
            material.mainTexture = texture;

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

        private static void CreateCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;

            var renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
        }

        private static Mesh CreateDoubleSidedQuad(float width, float height)
        {
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            var mesh = new Mesh
            {
                name = "Loop Tunnel Portal Quad",
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
                    0, 2, 1,
                    0, 3, 2,
                    0, 1, 2,
                    0, 2, 3
                }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
