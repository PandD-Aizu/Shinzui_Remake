using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>
    /// トンネル・通路・小部屋・ワープトリガーのUnity GameObject階層を
    /// MapRoot配下に描画するViewコンポーネント。NavMeshの管理はInfrastructureが担当する。
    /// ドメイン層やプレゼンテーション層に依存せず、純粋なUnityオブジェクト構築責務を持つ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TunnelMapView : MonoBehaviour
    {
        private const string MapRootName = "MapRoot";
        private const string GeometryRootName = "Tunnel Map Geometry";
        private const string TunnelAddressableKey = "TunnelBaseModel";
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

        [SerializeField] private Transform mapRoot;
        [Tooltip("Optional overview camera for a preview scene. Leave empty in gameplay scenes.")]
        [SerializeField] private Camera overviewCamera;
        [SerializeField] private bool showDebugGeometry;

        private GameObject _mapRootObject;
        private Transform _geometryRoot;

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
        private GameObject _ceilingLightPrefab;

        [Header("Ceiling Light Shadows")]
        [SerializeField, Min(0f)] private float ceilingShadowDistance = 15f;
        [SerializeField, Range(0, 16)] private int maxCeilingShadowLights = 8;
        private readonly List<(Light light, LightShadows shadows, float strength)> _ceilingLights = new();
        private readonly int[] _nearestShadowLights = new int[16];
        private readonly float[] _nearestShadowDistances = new float[16];

        /// <summary>
        /// カメラに近い蛍光灯だけ影を描画し、照明自体は遠方でも維持する
        /// </summary>
        private void LateUpdate()
        {
            Camera camera = Camera.main;
            int budget = camera != null ? Mathf.Clamp(maxCeilingShadowLights, 0, 16) : 0;
            float distanceLimit = Mathf.Max(0f, ceilingShadowDistance);
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            for (int i = 0; i < budget; i++)
            {
                _nearestShadowLights[i] = -1;
                _nearestShadowDistances[i] = distanceLimit * distanceLimit;
            }

            // 距離順に固定長配列へ挿入し、毎フレームのソートとメモリ確保を避ける
            for (int i = 0; i < _ceilingLights.Count; i++)
            {
                var entry = _ceilingLights[i];
                if (entry.light == null || !entry.light.isActiveAndEnabled) continue;
                entry.light.shadows = LightShadows.None;
                if (entry.shadows == LightShadows.None) continue;

                float distance = (entry.light.transform.position - cameraPosition).sqrMagnitude;
                for (int slot = 0; slot < budget; slot++)
                {
                    if (distance >= _nearestShadowDistances[slot]) continue;
                    for (int next = budget - 1; next > slot; next--)
                    {
                        _nearestShadowLights[next] = _nearestShadowLights[next - 1];
                        _nearestShadowDistances[next] = _nearestShadowDistances[next - 1];
                    }

                    _nearestShadowLights[slot] = i;
                    _nearestShadowDistances[slot] = distance;
                    break;
                }
            }

            // 距離上限の手前で影を薄くして切り替えを目立ちにくくする
            for (int slot = 0; slot < budget; slot++)
            {
                int index = _nearestShadowLights[slot];
                if (index < 0) break;
                var entry = _ceilingLights[index];
                entry.light.shadows = entry.shadows;
                entry.light.shadowStrength = entry.strength * (1f - Mathf.InverseLerp(
                    distanceLimit * 0.75f, distanceLimit, Mathf.Sqrt(_nearestShadowDistances[slot])));
            }
        }

        /// <summary>
        /// 管理を停止するときに蛍光灯の元の影設定を復元する
        /// </summary>
        private void OnDisable()
        {
            foreach (var entry in _ceilingLights)
            {
                if (entry.light == null) continue;
                entry.light.shadows = entry.shadows;
                entry.light.shadowStrength = entry.strength;
            }
        }

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

            bool useBounds = _generator == null || _generator.UseModelBoundsForLayout;
            if (useBounds && _tunnelPrefab != null && TryCalculateMeshBounds(_tunnelPrefab, GetTunnelModelScale(), out Bounds tunnelBounds))
            {
                tunnelLength = Mathf.Max(tunnelBounds.size.x, tunnelBounds.size.z);
                tunnelWidth = Mathf.Min(tunnelBounds.size.x, tunnelBounds.size.z);
                tunnelHeight = Mathf.Max(tunnelHeight, tunnelBounds.size.y);
            }

            if (_corridorTemplate != null && TryCalculateMeshBounds(_corridorTemplate, GetCorridorModelScale(false), out Bounds corridorBounds))
            {
                _corridorTemplateLongAxisIsX = corridorBounds.size.x >= corridorBounds.size.z;
                if (useBounds)
                {
                    corridorLength = Mathf.Max(corridorBounds.size.x, corridorBounds.size.z);
                    corridorWidth = Mathf.Max(1.0f, Mathf.Min(corridorBounds.size.x, corridorBounds.size.z));
                    tunnelHeight = Mathf.Max(tunnelHeight, corridorBounds.size.y);
                }
            }

            if (_warpCorridorTemplate != null && TryCalculateMeshBounds(_warpCorridorTemplate, GetCorridorModelScale(true), out Bounds warpBounds))
            {
                _warpCorridorTemplateLongAxisIsX = warpBounds.size.x >= warpBounds.size.z;
            }

            HideSceneTemplate(_tunnelPrefab);
            HideSceneTemplate(_corridorTemplate);
            HideSceneTemplate(_smallRoomTemplate);
            HideSceneTemplate(_warpCorridorTemplate);
        }

        /// <summary>Measure side-mouth spacing from the same authored mesh used to draw the six exits.</summary>
        public float ResolveConnectionPointSpacing(float configuredSpacing)
        {
            if ((_generator != null && !_generator.UseModelBoundsForLayout) || !_tunnelPrefab) return configuredSpacing;
            float total = 0; int count = 0;
            // The centre mouths have zero longitudinal offset; average each outer mouth relative
            // to the centre on its side so an imported pivot does not affect the spacing.
            for (int side = 0; side < 2; side++)
            {
                int middle = side * 3 + 1;
                if (!TryMouthPosition(middle, out var centre)) continue;
                foreach (int index in new[] { middle - 1, middle + 1 })
                    if (TryMouthPosition(index, out var outer)) { total += Vector3.Distance(outer, centre); count++; }
            }
            return count == 4 && total > .01f ? total / count : configuredSpacing;
        }

        private bool TryMouthPosition(int index, out Vector3 position)
        {
            position = default;
            var part = _tunnelPrefab.transform.Find(ExitPathNames[index]);
            if (!part || !TryGetMeshBoundsInLocalSpace(part, _tunnelPrefab.transform, out var bounds)) return false;
            position = Vector3.Scale(bounds.center, GetTunnelModelScale());
            return true;
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

            if (mapRoot != null)
            {
                if (mapRoot.gameObject.scene != gameObject.scene)
                    throw new InvalidOperationException("Tunnel MapRoot must belong to the same scene as its view.");
                _mapRootObject = mapRoot.gameObject;
            }
            else
            {
                foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                {
                    if (root.name != MapRootName) continue;
                    _mapRootObject = root;
                    break;
                }
            }
            if (_mapRootObject == null)
            {
                _mapRootObject = new GameObject(MapRootName);
                SceneManager.MoveGameObjectToScene(_mapRootObject, gameObject.scene);
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
            _ceilingLights.Clear();
            EnsureMapRoot();
            CreateMaterials();
            FindTemplates();

            if (_geometryRoot != null)
            {
                // Deferred destruction must not leave old colliders in the next navigation bake.
                _geometryRoot.gameObject.SetActive(false);
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

            CreateCeilingLights(root, length, height);
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
            if (!showDebugGeometry) return null;
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
            Camera camera = overviewCamera;
            if (camera == null || camera.gameObject.scene != gameObject.scene || boundsSize.sqrMagnitude < 1e-6f)
            {
                return;
            }

            camera.transform.position = boundsCenter + new Vector3(0.0f, Mathf.Max(55.0f, boundsSize.magnitude * 0.8f), -boundsSize.z * 0.25f);
            camera.transform.rotation = Quaternion.LookRotation(boundsCenter - camera.transform.position, Vector3.up);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, boundsSize.magnitude * 3.0f);
        }

        public void NotifyGeometryReady() => GeometryReady?.Invoke(_geometryRoot);

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
                    : null;
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
                // 接続先の出口と共通のモデル基準高を保持する
                AlignModelToPassageLocalSpace(model.transform, corridor, false);

                ApplyTunnelPassageMaterial(model);
                ApplyStageCollisionRecursive(model);
                CreateInvisibleStageCollider(corridor, "Walkable Floor Collider", Vector3.zero,
                    new Vector3(corridorWidth, ShellThickness * 1.5f, length));
                CreateCeilingLights(corridor, length, tunnelHeight);
                return;
            }

            CreateStageBox(corridor, "Floor", Vector3.zero, new Vector3(corridorWidth, ShellThickness * 1.5f, length), material);
            CreateStageBox(corridor, "Ceiling", new Vector3(0.0f, tunnelHeight, 0.0f), new Vector3(corridorWidth, ShellThickness, length), material);
            CreateCeilingLights(corridor, length, tunnelHeight);
        }

        /// <summary>
        /// 天井の内面を測定して通路の中央へ蛍光灯を一定間隔で配置する
        /// </summary>
        /// <param name="passage">配置先のトンネルまたは廊下</param>
        /// <param name="length">通路の長さ</param>
        /// <param name="height">天井を探索する高さ</param>
        private void CreateCeilingLights(Transform passage, float length, float height)
        {
            // 共通プレハブを再利用し、短い通路でも器具が端からはみ出さないようにする
            if (_ceilingLightPrefab == null)
            {
                _ceilingLightPrefab = Resources.Load<GameObject>("TunnelFluorescentLight");
            }

            const float endMargin = 0.8f;
            if (_ceilingLightPrefab == null || length < endMargin * 2.0f) return;

            float spacing = _generator != null ? _generator.CeilingLightSpacing : 5.0f;
            int count = Mathf.FloorToInt((length - endMargin * 2.0f) / spacing) + 1;
            float start = -(count - 1) * spacing * 0.5f;
            Collider[] surfaces = passage.GetComponentsInChildren<Collider>(false);
            Physics.SyncTransforms();

            // 通路内から上向きに測定し、外側の境界ではなく実際の天井面へ取り付ける
            var lightRoot = new GameObject("Ceiling Lights").transform;
            lightRoot.SetParent(passage, false);
            for (int i = 0; i < count; i++)
            {
                Vector3 origin = passage.TransformPoint(new Vector3(0.0f, 1.5f, start + i * spacing));
                var ray = new Ray(origin, passage.up);
                float distance = Mathf.Infinity;
                Vector3 ceiling = default;
                foreach (Collider surface in surfaces)
                {
                    if (surface.isTrigger || !surface.enabled) continue;
                    if (surface.Raycast(ray, out RaycastHit hit, Mathf.Max(height, 2.0f) * passage.lossyScale.y)
                        && hit.distance < distance && Vector3.Dot(hit.normal, passage.up) < -0.5f)
                    {
                        distance = hit.distance;
                        ceiling = hit.point;
                    }
                }

                if (float.IsPositiveInfinity(distance)) continue;

                // プレハブの取付面を天井直下へ置き、通路の伸縮を器具の寸法へ伝えない
                GameObject light = Instantiate(_ceilingLightPrefab, lightRoot, false);
                light.name = $"Fluorescent Light {i + 1:00}";
                light.transform.position = ceiling - passage.up * 0.02f;

                // 生成した蛍光灯のみを影予算の対象にし、懐中電灯は変更しない
                foreach (Light source in light.GetComponentsInChildren<Light>(true))
                {
                    _ceilingLights.Add((source, source.shadows, source.shadowStrength));
                    source.shadows = LightShadows.None;
                }
            }
        }

        /// <summary>
        /// トンネル出口のマテリアルを接続する廊下へ適用する
        /// </summary>
        /// <param name="model">生成した廊下モデル</param>
        private void ApplyTunnelPassageMaterial(GameObject model)
        {
            // 出口と共通のマテリアルを参照して外観を揃える
            Transform exit = _tunnelPrefab != null ? _tunnelPrefab.transform.Find(ExitPathNames[1]) : null;
            Renderer source = exit != null ? exit.GetComponentInChildren<Renderer>(true) : null;
            if (source == null || source.sharedMaterial == null)
            {
                return;
            }

            // メッシュの全サブメッシュへ同じマテリアルを割り当てる
            foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = source.sharedMaterial;
                }

                renderer.sharedMaterials = materials;
            }
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
                renderer.enabled = showDebugGeometry;
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

        private static bool TryCalculateMeshBounds(GameObject template, Vector3 additionalScale, out Bounds bounds)
        {
            bounds = default;
            if (template == null || !TryGetMeshBoundsInLocalSpace(template.transform, template.transform, out Bounds meshBounds))
                return false;
            // Prefab-asset Renderer.bounds can be stale/empty and include particle/VFX bounds.
            // Transform actual mesh corners into the prefab's frame, then apply the authored scale.
            bounds = new Bounds(Vector3.Scale(meshBounds.center, additionalScale),
                Vector3.Scale(meshBounds.size, Abs(additionalScale)));
            return true;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        /// <summary>
        /// 回転と拡縮後のモデルの水平中心を通路原点へ揃え、必要に応じて最下端の高さも揃える
        /// </summary>
        /// <param name="model">配置するモデル</param>
        /// <param name="localSpace">配置基準となる通路の座標系</param>
        /// <param name="alignFloor">モデルの最下端を通路原点の高さへ揃えるか</param>
        private static void AlignModelToPassageLocalSpace(Transform model, Transform localSpace, bool alignFloor = true)
        {
            if (!TryGetMeshBoundsInLocalSpace(model, localSpace, out Bounds localBounds))
            {
                return;
            }

            Vector3 localPosition = model.localPosition;
            localPosition.x -= localBounds.center.x;
            if (alignFloor)
            {
                localPosition.y -= localBounds.min.y;
            }

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

        private static void HideSceneTemplate(GameObject template)
        {
            if (template != null && template.scene.IsValid()) template.SetActive(false);
        }
    }
}
