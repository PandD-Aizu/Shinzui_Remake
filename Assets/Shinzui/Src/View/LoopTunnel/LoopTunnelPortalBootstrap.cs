using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Shinzui.View.LoopTunnel
{
    [DefaultExecutionOrder(-100)]
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

        [Header("Warp")]
        [SerializeField] private float cameraWarpMargin = 0.08f;

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

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        private void Awake()
        {
            _halfLength = tunnelLength * 0.5f;
            _mainCamera = Camera.main;
            _player = FindPlayerTransform();

            BuildTunnel();
            BuildPortals();
            BuildPortalCameras();

            if (_player != null)
            {
                _playerController = _player.GetComponent<CharacterController>();
                ClampPlayerIntoTunnel();
            }
        }

        private void LateUpdate()
        {
            _mainCamera = Camera.main;

            if (_mainCamera == null)
            {
                return;
            }

            EnsureRenderTextures();
            
            if (_frontPortalRenderer != null && _frontPortalCamera != null)
            {
                _frontPortalCamera.enabled = _frontPortalRenderer.isVisible;
            }
            if (_backPortalRenderer != null && _backPortalCamera != null)
            {
                _backPortalCamera.enabled = _backPortalRenderer.isVisible;
            }

            WrapPlayerIfNeeded();
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture(_frontPortalTexture);
            ReleaseRenderTexture(_backPortalTexture);
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
            light.intensity = 2.0f;
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

        private Camera CreatePortalCamera(string name)
        {
            var cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = true;
            camera.depth = -100;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.useOcclusionCulling = false;
            return camera;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (_mainCamera == null)
            {
                return;
            }

            if (camera == _frontPortalCamera)
            {
                PreparePortal(_frontPortalCamera, _frontPortal, _backPortal, _frontPortalRenderer, _backPortalRenderer);
            }
            else if (camera == _backPortalCamera)
            {
                PreparePortal(_backPortalCamera, _backPortal, _frontPortal, _frontPortalRenderer, _backPortalRenderer);
            }
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _frontPortalCamera || camera == _backPortalCamera)
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

        private void CopyCameraSettings(Camera source, Camera target)
        {
            RenderTexture renderTexture = target.targetTexture;
            target.CopyFrom(source);
            target.enabled = true;
            target.targetTexture = renderTexture;
            target.stereoTargetEye = StereoTargetEyeMask.None;
            target.farClipPlane = Mathf.Max(source.farClipPlane, tunnelLength * 2.5f);
            
            var sourceData = source.GetComponent<UniversalAdditionalCameraData>();
            var targetData = target.GetComponent<UniversalAdditionalCameraData>();
            if (sourceData != null)
            {
                if (targetData == null)
                {
                    targetData = target.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }
                targetData.renderPostProcessing = sourceData.renderPostProcessing;
                targetData.renderShadows = sourceData.renderShadows;
                targetData.antialiasing = sourceData.antialiasing;
                targetData.antialiasingQuality = sourceData.antialiasingQuality;
                targetData.volumeLayerMask = sourceData.volumeLayerMask;
                targetData.volumeTrigger = sourceData.volumeTrigger != null ? sourceData.volumeTrigger : source.transform;
                
                int index = GetRendererIndex(sourceData);
                if (index >= 0)
                {
                    targetData.SetRenderer(index);
                }
            }
        }

        private static FieldInfo _rendererIndexField;

        private static int GetRendererIndex(UniversalAdditionalCameraData data)
        {
            if (_rendererIndexField == null)
            {
                _rendererIndexField = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (_rendererIndexField != null)
            {
                return (int)_rendererIndexField.GetValue(data);
            }
            return -1;
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
            float warpDistance = Mathf.Max(cameraWarpMargin, _mainCamera.nearClipPlane + cameraWarpMargin);

            if (!_cameraDistancesInitialized)
            {
                SetPreviousCameraDistances(frontDistance, backDistance);
                _cameraDistancesInitialized = true;
                return;
            }

            if (_previousFrontCameraDistance > warpDistance && frontDistance <= warpDistance)
            {
                WarpPlayer(_frontPortal, _backPortal);
                return;
            }

            if (_previousBackCameraDistance > warpDistance && backDistance <= warpDistance)
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

        private static Material CreateMaterial(string name, Color color, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
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

        private static Material CreatePortalMaterial(string name)
        {
            var shader = Shader.Find("Custom/PortalShader");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
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
