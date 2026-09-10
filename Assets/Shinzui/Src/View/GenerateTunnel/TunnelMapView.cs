using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>
    /// トンネル・通路・小部屋・ワープトリガーのUnity GameObject階層を
    /// MapRoot配下に具体構築し、NavMeshSurfaceのベイクを実行するViewコンポーネント。
    /// ドメイン層やプレゼンテーション層に依存せず、純粋なUnityオブジェクト構築責務を持つ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TunnelMapView : MonoBehaviour
    {
        private const string MapRootName = "MapRoot";
        private const string GeometryRootName = "Tunnel Map Geometry";
        private const string TunnelAddressableKey = "TunnelBaseModel";
        private const string CorridorTemplateAssetPath = "Assets/Shinzui/3DModels/Tunnel_Path_Long.fbx";
        private const float ShellThickness = 0.2f;

        private static readonly string[] ExitPathNames =
        {
            "tunnel_path_+x_+y", // 0: Left +Z
            "tunnel_path__0_+y", // 1: Left Center
            "tunnel_path_-x_+y", // 2: Left -Z
            "tunnel_path_+x_-y", // 3: Right +Z
            "tunnel_path__0_-y", // 4: Right Center
            "tunnel_path_-x_-y"  // 5: Right -Z
        };

        private GameObject _mapRootObject;
        private Transform _geometryRoot;
        private NavMeshData _ownedNavMeshData;

        private Material _specialMaterial;
        private Material _tunnelMaterial;
        private Material _corridorMaterial;
        private Material _smallRoomMaterial;
        private Material _warpCorridorMaterial;
        private Material _warpTriggerMaterial;
        private Material _markerMaterial;

        private AsyncOperationHandle<GameObject> _tunnelPrefabHandle;
        private GameObject _tunnelPrefab;
        private bool _tunnelPrefabLoadAttempted;
        private GameObject _corridorTemplate;
        private GameObject _smallRoomTemplate;
        private GameObject _warpCorridorTemplate;
        private bool _corridorTemplateLongAxisIsX;
        private bool _warpCorridorTemplateLongAxisIsX;
        private TunnelGenerator _generator;

        public GameObject MapRootObject => _mapRootObject;
        public Transform GeometryRoot => _geometryRoot;
        public event Action GeometryClearing;
        public event Action<Transform> GeometryReady;

        // Explicit engine templates allow the same geometry builder to run without an Addressables load.
        public void SetBuildTemplates(GameObject tunnel, GameObject corridor)
        {
            _tunnelPrefab = tunnel; _corridorTemplate = corridor; _tunnelPrefabLoadAttempted = true;
        }

        public void Configure(TunnelGenerator generator)
        {
            _generator = generator;
            _tunnelPrefab = null;
            _tunnelPrefabLoadAttempted = false;
            _corridorTemplate = null;
            _smallRoomTemplate = null;
            _warpCorridorTemplate = null;
        }

        private void OnDestroy()
        {
            GeometryClearing?.Invoke();
            foreach (var material in new[] { _specialMaterial, _tunnelMaterial, _corridorMaterial,
                _smallRoomMaterial, _warpCorridorMaterial, _warpTriggerMaterial, _markerMaterial })
                if (material) Destroy(material);
            if (_ownedNavMeshData)
            {
                if (_mapRootObject && _mapRootObject.TryGetComponent<NavMeshSurface>(out var surface) && surface.navMeshData == _ownedNavMeshData)
                    surface.RemoveData();
                Destroy(_ownedNavMeshData);
            }
            if (_tunnelPrefabHandle.IsValid())
            {
                Addressables.Release(_tunnelPrefabHandle);
            }
        }

        /// <summary>
        /// シーンまたはプロジェクトアセットから見本モデルを取得し、実測寸法を返す。
        /// </summary>
        public void ResolveTemplateDimensions(
            ref float tunnelLength,
            ref float tunnelWidth,
            ref float tunnelHeight,
            ref float corridorLength,
            ref float corridorWidth)
        {
            FindTemplates();

            if (_generator != null && !_generator.UseModelBoundsForLayout)
            {
                return;
            }

            if (_tunnelPrefab != null && TryCalculateRendererBounds(_tunnelPrefab, Vector3.one, out Bounds tunnelBounds))
            {
                tunnelLength = Mathf.Max(tunnelBounds.size.x, tunnelBounds.size.z);
                tunnelWidth = Mathf.Min(tunnelBounds.size.x, tunnelBounds.size.z);
                tunnelHeight = Mathf.Max(tunnelHeight, tunnelBounds.size.y);
            }

            if (_corridorTemplate != null && TryCalculateRendererBounds(_corridorTemplate, Vector3.one, out Bounds corridorBounds))
            {
                _corridorTemplateLongAxisIsX = corridorBounds.size.x >= corridorBounds.size.z;
                corridorLength = Mathf.Max(corridorBounds.size.x, corridorBounds.size.z);
                corridorWidth = Mathf.Max(1.0f, Mathf.Min(corridorBounds.size.x, corridorBounds.size.z));
                tunnelHeight = Mathf.Max(tunnelHeight, corridorBounds.size.y);
            }

            if (_warpCorridorTemplate != null && TryCalculateRendererBounds(_warpCorridorTemplate, Vector3.one, out Bounds warpBounds))
            {
                _warpCorridorTemplateLongAxisIsX = warpBounds.size.x >= warpBounds.size.z;
            }

            if (_corridorTemplate != null && _corridorTemplate.scene.IsValid())
            {
                _corridorTemplate.SetActive(false);
            }

            if (_warpCorridorTemplate != null && _warpCorridorTemplate.scene.IsValid())
            {
                _warpCorridorTemplate.SetActive(false);
            }
        }

        /// <summary>
        /// MapRoot の存在を保証し、再利用または新規作成する。MapRootは決して無効化しない。
        /// </summary>
        public GameObject EnsureMapRoot()
        {
            if (_mapRootObject != null)
            {
                _mapRootObject.SetActive(true);
                return _mapRootObject;
            }

            _mapRootObject = GameObject.Find(MapRootName);
            if (_mapRootObject == null)
            {
                _mapRootObject = new GameObject(MapRootName);
            }

            _mapRootObject.SetActive(true);
            return _mapRootObject;
        }

        /// <summary>
        /// マテリアルと階層ルートを準備する。
        /// </summary>
        public void PrepareBuild()
        {
            GeometryClearing?.Invoke();
            EnsureMapRoot();
            CreateMaterials();
            FindTemplates();

            if (_geometryRoot != null)
            {
                Destroy(_geometryRoot.gameObject);
            }

            var geoObj = new GameObject(GeometryRootName);
            _geometryRoot = geoObj.transform;
            _geometryRoot.SetParent(_mapRootObject.transform, false);
            _geometryRoot.localPosition = Vector3.zero;
            _geometryRoot.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// トンネルGameObjectを作成する。
        /// </summary>
        public Transform CreateTunnelNode(
            Vector3 position,
            string objectName,
            bool isSpecial,
            float width,
            float height,
            float length,
            IReadOnlyList<bool> openEntrances = null)
        {
            var root = new GameObject(objectName).transform;
            root.SetParent(_geometryRoot, false);
            root.localPosition = position;

            Material shellMaterial = isSpecial ? _specialMaterial : _tunnelMaterial;
            if (_tunnelPrefab != null)
            {
                GameObject model = Instantiate(_tunnelPrefab, root, false);
                model.name = "TunnelBaseModel";
                model.SetActive(true);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = GetTunnelModelRotation();
                model.transform.localScale = GetTunnelModelScale();

                if (_generator == null || _generator.ConfigureBasicTunnelExitParts)
                {
                    ConfigureTunnelExits(model, openEntrances);
                }

                if (isSpecial)
                {
                    foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.sharedMaterial = shellMaterial;
                    }
                }

                ApplyStageCollisionRecursive(model);
            }
            else
            {
                CreateStageBox(root, "Floor", Vector3.zero, new Vector3(width, ShellThickness, length), shellMaterial);
                CreateStageBox(root, "Ceiling", new Vector3(0.0f, height, 0.0f), new Vector3(width, ShellThickness, length), shellMaterial);
                CreateStageBox(root, "Left Wall", new Vector3(-width * 0.5f, height * 0.5f, 0.0f), new Vector3(ShellThickness, height, length), shellMaterial);
                CreateStageBox(root, "Right Wall", new Vector3(width * 0.5f, height * 0.5f, 0.0f), new Vector3(ShellThickness, height, length), shellMaterial);
            }

            return root;
        }

        /// <summary>
        /// トンネルインスタンスの6出口について、接続使用中なら開放、未使用ならcoverで塞ぐ。
        /// </summary>
        private void ConfigureTunnelExits(GameObject model, IReadOnlyList<bool> openEntrances)
        {
            Transform modelTransform = model.transform;
            Transform protoCoverTransform = modelTransform.Find("tunnel_cover__0_-y");
            GameObject protoCover = protoCoverTransform != null ? protoCoverTransform.gameObject : null;

            if (protoCover == null)
            {
                Debug.LogError($"[TunnelMapView] Missing prototype cover GameObject 'tunnel_cover__0_-y' in tunnel instance '{model.name}'. Closed exits will not be sealed properly.", this);
            }
            else
            {
                // 原型 cover は元アセットとして常に非表示にし、閉鎖出口ごとに clone を作成して配置する
                protoCover.SetActive(false);
            }

            Transform[] pathTransforms = new Transform[ExitPathNames.Length];
            MeshFilter[] pathMeshFilters = new MeshFilter[ExitPathNames.Length];
            for (int i = 0; i < ExitPathNames.Length; i++)
            {
                pathTransforms[i] = modelTransform.Find(ExitPathNames[i]);
                if (pathTransforms[i] != null)
                {
                    pathMeshFilters[i] = pathTransforms[i].GetComponent<MeshFilter>();
                }
                else
                {
                    Debug.LogError($"[TunnelMapView] Missing required path GameObject '{ExitPathNames[i]}' in tunnel instance '{model.name}'.", this);
                }
            }

            // prototype cover は Right Center (index 4: tunnel_path__0_-y) の形状に基づいている
            const int sourceIndex = 4;
            Vector3 sourceCenter = pathMeshFilters[sourceIndex] != null && pathMeshFilters[sourceIndex].sharedMesh != null
                ? pathMeshFilters[sourceIndex].sharedMesh.bounds.center
                : new Vector3(0.0f, 2.657f, 6.817f);

            for (int i = 0; i < ExitPathNames.Length; i++)
            {
                // null または 6 未満の openEntrances は安全側（閉鎖 / false）として扱う
                bool isOpen = openEntrances != null && i < openEntrances.Count && openEntrances[i];

                if (pathTransforms[i] != null)
                {
                    pathTransforms[i].gameObject.SetActive(isOpen);
                }

                if (isOpen)
                {
                    // 開放出口: path のみ有効、cover なし
                    continue;
                }

                // 閉鎖出口: cover で塞ぐ
                if (protoCover != null)
                {
                    GameObject cover = Instantiate(protoCover, modelTransform, false);
                    cover.name = $"tunnel_cover_{i}_{ExitPathNames[i]}";

                    // Left 側 (0, 1, 2) は Y 軸 180 度回転、Right 側 (3, 4, 5) は identity
                    Quaternion rotation = i < 3
                        ? Quaternion.Euler(0.0f, 180.0f, 0.0f)
                        : Quaternion.identity;

                    cover.transform.localRotation = rotation;

                    Vector3 targetCenter = pathMeshFilters[i] != null && pathMeshFilters[i].sharedMesh != null
                        ? pathMeshFilters[i].sharedMesh.bounds.center
                        : rotation * sourceCenter;

                    cover.transform.localPosition = targetCenter - rotation * sourceCenter;
                    cover.SetActive(true);
                }
            }
        }

        /// <summary>
        /// トンネルの出入口候補マーカーを作成する。
        /// </summary>
        public GameObject CreateEntranceMarker(Transform tunnelRoot, string markerName, Vector3 localPosition)
        {
            return CreateBox(tunnelRoot, markerName, localPosition, new Vector3(0.7f, 0.12f, 0.7f), _markerMaterial);
        }

        /// <summary>
        /// 通常通路GameObjectを作成する。
        /// </summary>
        public GeneratedCorridorInfo CreateNormalCorridor(
            Vector3 center,
            Vector3 direction,
            float length,
            int index,
            float corridorWidth,
            float tunnelHeight,
            float standardCorridorLength)
        {
            var corridor = new GameObject($"Corridor {index:00}").transform;
            corridor.SetParent(_geometryRoot, false);
            corridor.localPosition = center;
            corridor.localRotation = direction.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;

            var info = corridor.gameObject.AddComponent<GeneratedCorridorInfo>();
            info.Initialize(GeneratedCorridorKind.Normal);

            CreatePassageShell(corridor, length, _corridorMaterial, corridorWidth, tunnelHeight, standardCorridorLength);
            return info;
        }

        /// <summary>
        /// 小部屋接続GameObjectを作成する。
        /// </summary>
        public GeneratedCorridorInfo CreateSmallRoomConnection(
            Vector3 center,
            Vector3 direction,
            int roomNumber,
            float roomWidth,
            float roomLength,
            float passageLength,
            float corridorWidth,
            float tunnelHeight,
            float standardCorridorLength)
        {
            var connection = new GameObject($"Small Room Connection {roomNumber:00}").transform;
            connection.SetParent(_geometryRoot, false);
            connection.localPosition = center;
            connection.localRotation = direction.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;

            var info = connection.gameObject.AddComponent<GeneratedCorridorInfo>();
            info.Initialize(GeneratedCorridorKind.Normal);

            CreateRoomPassage(connection, "Corridor Before Room", -(roomLength + passageLength) * 0.5f, passageLength, corridorWidth, tunnelHeight, standardCorridorLength);
            CreateRoomPassage(connection, "Corridor After Room", (roomLength + passageLength) * 0.5f, passageLength, corridorWidth, tunnelHeight, standardCorridorLength);

            var room = new GameObject($"Small Room {roomNumber:00}").transform;
            room.SetParent(connection, false);

            if (_smallRoomTemplate != null)
            {
                GameObject model = Instantiate(_smallRoomTemplate, room, false);
                model.name = _smallRoomTemplate.name;
                model.SetActive(true);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = _generator != null ? _generator.SmallRoomModelRotation : Quaternion.identity;
                model.transform.localScale = _generator != null ? _generator.SmallRoomModelScale : Vector3.one;
                AlignModelToPassageLocalSpace(model.transform, room);
                ApplyStageCollisionRecursive(model);
                CreateInvisibleStageCollider(room, "Walkable Room Floor Collider", Vector3.zero,
                    new Vector3(roomWidth, ShellThickness * 1.5f, roomLength));
            }
            else
            {
                float floorThickness = ShellThickness * 1.5f;
                CreateStageBox(room, "Walkable Room Floor", Vector3.zero, new Vector3(roomWidth, floorThickness, roomLength), _smallRoomMaterial);
            }

            return info;
        }

        /// <summary>
        /// ワープ通路GameObjectを作成する。
        /// </summary>
        public GeneratedCorridorInfo CreateWarpCorridor(
            Vector3 center,
            Vector3 direction,
            float length,
            int pairId,
            string sideName,
            float corridorWidth,
            float tunnelHeight,
            float standardCorridorLength)
        {
            var corridor = new GameObject($"Warp Corridor Pair {pairId:00} {sideName}").transform;
            corridor.SetParent(_geometryRoot, false);
            corridor.localPosition = center;
            corridor.localRotation = direction.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;

            CreatePassageShell(corridor, length, _warpCorridorMaterial, corridorWidth, tunnelHeight, standardCorridorLength, true);

            GeneratedCorridorInfo info = corridor.gameObject.AddComponent<GeneratedCorridorInfo>();
            info.Initialize(GeneratedCorridorKind.Warp, pairId);

            CreateWarpTrigger(corridor, info, length, corridorWidth, tunnelHeight);
            return info;
        }

        /// <summary>
        /// メインカメラの位置・角度・クリップ距離をトンネル全体に合わせる。
        /// </summary>
        public void FrameSceneCamera(Vector3 boundsCenter, Vector3 boundsSize)
        {
            Camera camera = Camera.main;
            if (camera == null || boundsSize.sqrMagnitude < 1e-6f)
            {
                return;
            }

            camera.transform.position = boundsCenter + new Vector3(0.0f, Mathf.Max(55.0f, boundsSize.magnitude * 0.8f), -boundsSize.z * 0.25f);
            camera.transform.rotation = Quaternion.LookRotation(boundsCenter - camera.transform.position, Vector3.up);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, boundsSize.magnitude * 3.0f);
        }

        /// <summary>
        /// MapRoot 自身の NavMeshSurface を再利用（無ければ MapRoot 自身へ追加）し、
        /// collectObjects = CollectObjects.Children および useGeometry = NavMeshCollectGeometry.PhysicsColliders を設定して
        /// 全ジオメトリ生成完了後に一度だけ BuildNavMesh を実行する。
        /// 失敗時は原因をログ出力する。
        /// </summary>
        public bool BuildNavMesh()
        {
            EnsureMapRoot();

            NavMeshSurface surface = _mapRootObject.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = _mapRootObject.AddComponent<NavMeshSurface>();
            }

            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            try
            {
                var previousData = surface.navMeshData;
                surface.BuildNavMesh();
                if (surface.navMeshData != previousData)
                {
                    // Only destroy data created by this view; never release an authored NavMesh asset.
                    if (_ownedNavMeshData) Destroy(_ownedNavMeshData);
                    _ownedNavMeshData = surface.navMeshData;
                }
                Debug.Log($"[TunnelMapView] Successfully built NavMesh on '{_mapRootObject.name}'.", this);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TunnelMapView] Failed to build NavMesh on '{_mapRootObject.name}': {ex.Message}\n{ex.StackTrace}", this);
                return false;
            }
            finally { GeometryReady?.Invoke(_geometryRoot); }
        }

        private void FindTemplates()
        {
            if (_generator != null && _generator.BasicTunnelModel != null)
            {
                _tunnelPrefab = _generator.BasicTunnelModel;
                _tunnelPrefabLoadAttempted = true;
            }

            if (!_tunnelPrefabLoadAttempted)
            {
                _tunnelPrefabLoadAttempted = true;
                try
                {
                    _tunnelPrefabHandle = Addressables.LoadAssetAsync<GameObject>(TunnelAddressableKey);
                    _tunnelPrefab = _tunnelPrefabHandle.WaitForCompletion();
                    if (_tunnelPrefab == null)
                    {
                        Debug.LogError($"[TunnelMapView] Failed to load Addressable asset '{TunnelAddressableKey}': Result is null. Releasing handle and falling back to procedural geometry.", this);
                        if (_tunnelPrefabHandle.IsValid())
                        {
                            Addressables.Release(_tunnelPrefabHandle);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TunnelMapView] Exception while loading Addressable asset '{TunnelAddressableKey}': {ex.Message}. Releasing handle and falling back to procedural geometry.", this);
                    _tunnelPrefab = null;
                    if (_tunnelPrefabHandle.IsValid())
                    {
                        Addressables.Release(_tunnelPrefabHandle);
                    }
                }
            }

            if (_corridorTemplate == null)
            {
                _corridorTemplate = _generator != null && _generator.CorridorModel != null
                    ? _generator.CorridorModel
                    : GameObject.Find("Tunnel_Path_Long");
            }
            if (_corridorTemplate == null)
            {
                _corridorTemplate = LoadProjectAsset(CorridorTemplateAssetPath);
            }

            if (_smallRoomTemplate == null && _generator != null)
            {
                _smallRoomTemplate = _generator.SmallRoomModel;
            }

            if (_warpCorridorTemplate == null && _generator != null)
            {
                _warpCorridorTemplate = _generator.WarpCorridorModel;
            }
        }

        private void CreateMaterials()
        {
            // Rebuilding geometry reuses this view's materials; dispose them with the view.
            if (_specialMaterial) return;
            _specialMaterial = CreateMaterial("Special Tunnel", new Color(0.16f, 0.48f, 0.68f));
            _tunnelMaterial = CreateMaterial("Tunnel", new Color(0.23f, 0.26f, 0.29f));
            _corridorMaterial = CreateMaterial("Connecting Corridor", new Color(0.72f, 0.48f, 0.13f));
            _smallRoomMaterial = CreateMaterial("Small Room Floor", new Color(0.38f, 0.34f, 0.25f));
            _warpCorridorMaterial = CreateMaterial("Warp Corridor", new Color(0.62f, 0.2f, 0.82f));
            _warpTriggerMaterial = CreateMaterial("Warp Trigger Center", new Color(0.01f, 0.0f, 0.015f));
            _markerMaterial = CreateMaterial("Entrance Candidate", new Color(0.1f, 0.9f, 0.65f));
        }

        private void CreateRoomPassage(
            Transform connectionRoot,
            string objectName,
            float localZ,
            float length,
            float corridorWidth,
            float tunnelHeight,
            float standardCorridorLength)
        {
            var passage = new GameObject(objectName).transform;
            passage.SetParent(connectionRoot, false);
            passage.localPosition = new Vector3(0.0f, 0.0f, localZ);
            CreatePassageShell(passage, length, _corridorMaterial, corridorWidth, tunnelHeight, standardCorridorLength, false);
        }

        private void CreatePassageShell(
            Transform corridor,
            float length,
            Material material,
            float corridorWidth,
            float tunnelHeight,
            float standardCorridorLength,
            bool isWarpCorridor = false)
        {
            GameObject template = isWarpCorridor && _warpCorridorTemplate != null ? _warpCorridorTemplate : _corridorTemplate;
            bool longAxisIsX = isWarpCorridor && _warpCorridorTemplate != null ? _warpCorridorTemplateLongAxisIsX : _corridorTemplateLongAxisIsX;

            if (template != null)
            {
                GameObject model = Instantiate(template, corridor, false);
                model.name = template.name;
                model.SetActive(true);
                Quaternion configuredRotation = GetCorridorModelRotation(isWarpCorridor);
                model.transform.localRotation = longAxisIsX
                    ? configuredRotation * Quaternion.Euler(0.0f, 90.0f, 0.0f)
                    : configuredRotation;
                float lengthScale = standardCorridorLength > Mathf.Epsilon ? length / standardCorridorLength : 1.0f;
                Vector3 baseScale = GetCorridorModelScale(isWarpCorridor);
                model.transform.localScale = longAxisIsX
                    ? new Vector3(baseScale.x * lengthScale, baseScale.y, baseScale.z)
                    : new Vector3(baseScale.x, baseScale.y, baseScale.z * lengthScale);
                AlignModelToPassageLocalSpace(model.transform, corridor);

                ApplyStageCollisionRecursive(model);
                CreateInvisibleStageCollider(corridor, "Walkable Floor Collider", Vector3.zero,
                    new Vector3(corridorWidth, ShellThickness * 1.5f, length));
                return;
            }

            CreateStageBox(corridor, "Floor", Vector3.zero, new Vector3(corridorWidth, ShellThickness * 1.5f, length), material);
            CreateStageBox(corridor, "Ceiling", new Vector3(0.0f, tunnelHeight, 0.0f), new Vector3(corridorWidth, ShellThickness, length), material);
        }

        private void CreateWarpTrigger(
            Transform corridor,
            GeneratedCorridorInfo info,
            float length,
            float corridorWidth,
            float tunnelHeight)
        {
            float triggerLength = Mathf.Min(Mathf.Max(2.0f, length * 0.18f), 6.0f);
            GameObject trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trigger.name = "Warp Trigger Center";
            trigger.transform.SetParent(corridor, false);
            trigger.transform.localPosition = new Vector3(0.0f, tunnelHeight * 0.5f, 0.0f);
            trigger.transform.localScale = new Vector3(corridorWidth * 0.85f, tunnelHeight, triggerLength);

            Renderer renderer = trigger.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _warpTriggerMaterial;
            }

            Collider collider = trigger.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            int stageLayer = LayerMask.NameToLayer("Stage");
            if (stageLayer >= 0)
            {
                trigger.layer = stageLayer;
            }

            trigger.AddComponent<GeneratedWarpCorridorTrigger>().Initialize(info);

            const float backstopThickness = 0.35f;
            CreateInvisibleStageCollider(
                corridor,
                "Warp Backstop Collider",
                new Vector3(0.0f, tunnelHeight * 0.5f, triggerLength * 0.5f + backstopThickness * 0.5f),
                new Vector3(corridorWidth * 1.1f, tunnelHeight, backstopThickness));
        }

        private static Material CreateMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = materialName, color = color };
            return material;
        }

        private static GameObject CreateBox(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            box.GetComponent<Renderer>().sharedMaterial = material;

            Collider collider = box.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
            return box;
        }

        private static GameObject CreateStageBox(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            box.GetComponent<Renderer>().sharedMaterial = material;

            int stageLayer = LayerMask.NameToLayer("Stage");
            if (stageLayer >= 0)
            {
                box.layer = stageLayer;
            }

            return box;
        }

        private static GameObject CreateInvisibleStageCollider(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;

            Renderer renderer = box.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            int stageLayer = LayerMask.NameToLayer("Stage");
            if (stageLayer >= 0)
            {
                box.layer = stageLayer;
            }

            return box;
        }

        private Vector3 GetTunnelModelScale()
        {
            return _generator != null ? _generator.BasicTunnelModelScale : Vector3.one;
        }

        private Quaternion GetTunnelModelRotation()
        {
            return _generator != null ? _generator.BasicTunnelModelRotation : Quaternion.Euler(0.0f, 90.0f, 0.0f);
        }

        private Vector3 GetCorridorModelScale(bool isWarpCorridor)
        {
            if (_generator == null)
            {
                return Vector3.one;
            }

            return isWarpCorridor && _generator.WarpCorridorModel != null
                ? _generator.WarpCorridorModelScale
                : _generator.CorridorModelScale;
        }

        private Quaternion GetCorridorModelRotation(bool isWarpCorridor)
        {
            if (_generator == null)
            {
                return Quaternion.identity;
            }

            return isWarpCorridor && _generator.WarpCorridorModel != null
                ? _generator.WarpCorridorModelRotation
                : _generator.CorridorModelRotation;
        }

        private static bool TryCalculateRendererBounds(GameObject template, Vector3 additionalScale, out Bounds bounds)
        {
            bounds = default;
            if (template == null)
            {
                return false;
            }

            Renderer[] renderers = template.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Bounds rendererBounds = renderer.bounds;
                Vector3 center = Vector3.Scale(rendererBounds.center, additionalScale);
                Vector3 size = Vector3.Scale(rendererBounds.size, Abs(additionalScale));
                Bounds scaledBounds = new(center, size);

                if (!hasBounds)
                {
                    bounds = scaledBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(scaledBounds);
                }
            }

            return hasBounds;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        /// <summary>
        /// Imported corridor assets may use an arbitrary pivot. Align their horizontal center
        /// and lowest mesh point to the generated passage origin after rotation and scaling.
        /// </summary>
        private static void AlignModelToPassageLocalSpace(Transform model, Transform localSpace)
        {
            if (!TryGetMeshBoundsInLocalSpace(model, localSpace, out Bounds localBounds))
            {
                return;
            }

            Vector3 localPosition = model.localPosition;
            localPosition.x -= localBounds.center.x;
            localPosition.y -= localBounds.min.y;
            localPosition.z -= localBounds.center.z;
            model.localPosition = localPosition;
        }

        private static bool TryGetMeshBoundsInLocalSpace(Transform model, Transform localSpace, out Bounds localBounds)
        {
            localBounds = default;
            MeshFilter[] meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
            bool hasBounds = false;

            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null) continue;

                Bounds meshBounds = mesh.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;
                Vector3[] corners =
                {
                    new(min.x, min.y, min.z),
                    new(min.x, min.y, max.z),
                    new(min.x, max.y, min.z),
                    new(min.x, max.y, max.z),
                    new(max.x, min.y, min.z),
                    new(max.x, min.y, max.z),
                    new(max.x, max.y, min.z),
                    new(max.x, max.y, max.z)
                };

                foreach (Vector3 corner in corners)
                {
                    Vector3 worldCorner = meshFilter.transform.TransformPoint(corner);
                    Vector3 localCorner = localSpace.InverseTransformPoint(worldCorner);
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localCorner);
                    }
                }
            }

            return hasBounds;
        }

        private static void ApplyStageCollisionRecursive(GameObject root)
        {
            int stageLayer = LayerMask.NameToLayer("Stage");
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (stageLayer >= 0)
                {
                    transform.gameObject.layer = stageLayer;
                }
            }

            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (meshFilter.sharedMesh == null || meshFilter.GetComponent<Collider>() != null)
                {
                    continue;
                }

                MeshCollider collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
            }
        }

        private static GameObject LoadProjectAsset(string assetPath)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#else
            return null;
#endif
        }
    }
}
