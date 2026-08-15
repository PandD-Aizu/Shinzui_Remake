using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shinzui.View.GenerateTunnel
{
    public enum GeneratedCorridorKind
    {
        Normal,
        Warp
    }

    /// <summary>生成後の通路種別と、ワープ通路の対応相手を保持する識別用コンポーネント。</summary>
    public sealed class GeneratedCorridorInfo : MonoBehaviour
    {
        [SerializeField] private GeneratedCorridorKind kind;
        [SerializeField] private int warpPairId = -1;
        [SerializeField] private GeneratedCorridorInfo pairedCorridor;

        public GeneratedCorridorKind Kind => kind;
        public int WarpPairId => warpPairId;
        public GeneratedCorridorInfo PairedCorridor => pairedCorridor;

        // 通路の種別とワープペアIDを初期化する。
        public void Initialize(GeneratedCorridorKind corridorKind, int pairId = -1)
        {
            kind = corridorKind;
            warpPairId = pairId;
        }

        // このワープ通路と対になる通路情報を登録する。
        public void SetPair(GeneratedCorridorInfo pair)
        {
            pairedCorridor = pair;
        }
    }

    public sealed class GeneratedWarpCorridorTrigger : MonoBehaviour
    {
        private const float WarpCooldown = 0.35f;
        private static float _lastWarpTime = -WarpCooldown;

        private GeneratedCorridorInfo _sourceCorridor;

        // ワープ判定の起点になる通路情報を登録する。
        public void Initialize(GeneratedCorridorInfo sourceCorridor)
        {
            _sourceCorridor = sourceCorridor;
        }

        // プレイヤーが中心トリガーに入ったら、対応するワープ通路へ移動させる。
        private void OnTriggerEnter(Collider other)
        {
            if (Time.time - _lastWarpTime < WarpCooldown)
            {
                return;
            }

            if (_sourceCorridor == null || _sourceCorridor.PairedCorridor == null)
            {
                return;
            }

            PlayerView player = other.GetComponentInParent<PlayerView>();
            if (player == null)
            {
                return;
            }

            Vector3 targetForward = _sourceCorridor.PairedCorridor.transform.forward;
            Vector3 offset = _sourceCorridor.PairedCorridor.transform.position
                             - targetForward * 1.25f
                             - _sourceCorridor.transform.position;
            player.Warp(offset);
            _lastWarpTime = Time.time;
        }
    }

    /// <summary>
    /// GenerateTunnelTest を単独で再生したときだけ、確認用のトンネル生成器を追加する。
    /// シーンに専用オブジェクトを保存しないため、生成パラメータの実験でシーンが汚れない。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GenerateTunnelTestBootstrap : MonoBehaviour
    {
        private static readonly HashSet<string> TargetSceneNames = new()
        {
            "GenerateTunnelTest",
            "LatestStageGenerateTemp"
        };
        private const string InstanceName = "[Generated Tunnel Map]";

        [Header("Generation")]
        [Min(5)] [SerializeField] private int tunnelCount = 8;
        [SerializeField] private int seed = 2777;
        [Min(1)] [SerializeField] private int placementAttemptsPerTunnel = 80;

        [Header("Small rooms")]
        [Min(0)] [SerializeField] private int smallRoomCount = 1;
        [Min(1.0f)] [SerializeField] private float smallRoomWidth = 8.0f;
        [Min(1.0f)] [SerializeField] private float smallRoomLength = 6.0f;

        [Header("Fixed tunnel size")]
        [Min(4.0f)] [SerializeField] private float tunnelLength = 153.12695f;
        [Min(3.0f)] [SerializeField] private float tunnelWidth = 14.800003f;
        [Min(2.0f)] [SerializeField] private float tunnelHeight = 5.0f;
        [Min(1.0f)] [SerializeField] private float corridorLength = 8.0f;
        [Min(1.0f)] [SerializeField] private float corridorWidth = 3.0f;
        [Min(0.0f)] [SerializeField] private float placementMargin = 2.0f;

        [Header("Overlap prevention")]
        [Min(0.1f)] [SerializeField] private float tunnelOverlapSizeMultiplier = 1.0f;
        [Min(0.1f)] [SerializeField] private float corridorOverlapSizeMultiplier = 1.0f;
        [Min(0.1f)] [SerializeField] private float smallRoomOverlapSizeMultiplier = 1.0f;

        // シーンに保存された設定コンポーネントから、実際の生成器を起動する。
        private void Awake()
        {
            if (GetComponent<GenerateTunnelTestRuntime>() == null)
            {
                gameObject.AddComponent<GenerateTunnelTestRuntime>();
            }
        }

        internal void ApplyTo(GenerateTunnelTestRuntime runtime)
        {
            runtime.Configure(tunnelCount, seed, placementAttemptsPerTunnel,
                smallRoomCount, smallRoomWidth, smallRoomLength,
                tunnelLength, tunnelWidth, tunnelHeight,
                corridorLength, corridorWidth, placementMargin,
                tunnelOverlapSizeMultiplier, corridorOverlapSizeMultiplier, smallRoomOverlapSizeMultiplier);
        }

        // シーン読み込みイベントへ登録し、対象シーンで生成器を自動作成できるようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoaded()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // すでに読み込まれているアクティブシーンに対して生成器の作成を試みる。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapActiveScene()
        {
            TryCreate(SceneManager.GetActiveScene());
        }

        // シーン読み込み完了時に、対象シーンなら生成器の作成を試みる。
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreate(scene);
        }

        // 対象シーンで未生成の場合だけ、ランダムトンネル生成用ルートを作成する。
        private static void TryCreate(Scene scene)
        {
            if (!scene.IsValid() || !TargetSceneNames.Contains(scene.name) || GameObject.Find(InstanceName) != null)
            {
                return;
            }

            var root = new GameObject(InstanceName);
            root.AddComponent<GenerateTunnelTestRuntime>();
        }
    }

    /// <summary>
    /// 6 個の固定出入口候補を持つトンネルを、必ず既存トンネルから枝分かれさせて配置する。
    /// そのため生成された全トンネルは通路によって一つの連結グラフになる。
    /// </summary>
    public sealed class GenerateTunnelTestRuntime : MonoBehaviour
    {
        [Header("Generation")]
        [Min(5)] [SerializeField] private int tunnelCount = 8;
        [SerializeField] private int seed = 2777;
        [Min(1)] [SerializeField] private int placementAttemptsPerTunnel = 80;

        [Header("Small rooms")]
        [Min(0)] [SerializeField] private int smallRoomCount = 1;
        [Min(1.0f)] [SerializeField] private float smallRoomWidth = 8.0f;
        [Min(1.0f)] [SerializeField] private float smallRoomLength = 6.0f;

        [Header("Fixed tunnel size")]
        [Min(4.0f)] [SerializeField] private float tunnelLength = 153.12695f;
        [Min(3.0f)] [SerializeField] private float tunnelWidth = 14.800003f;
        [Min(2.0f)] [SerializeField] private float tunnelHeight = 5.0f;
        [Min(1.0f)] [SerializeField] private float corridorLength = 8.0f;
        [Min(1.0f)] [SerializeField] private float corridorWidth = 3.0f;
        [Min(0.0f)] [SerializeField] private float placementMargin = 2.0f;

        [Header("Overlap prevention")]
        [Min(0.1f)] [SerializeField] private float tunnelOverlapSizeMultiplier = 1.0f;
        [Min(0.1f)] [SerializeField] private float corridorOverlapSizeMultiplier = 1.0f;
        [Min(0.1f)] [SerializeField] private float smallRoomOverlapSizeMultiplier = 1.0f;

        private const float ShellThickness = 0.2f;
        private const string TunnelTemplateAssetPath = "Assets/Shinzui/3DModels/tunnelBase_tmp.fbx";
        private const string CorridorTemplateAssetPath = "Assets/Shinzui/3DModels/Tunnel_Path_Long.fbx";

        private readonly List<TunnelNode> _tunnels = new();
        private readonly List<OpenEntrance> _openEntrances = new();
        private readonly List<NormalCorridor> _normalCorridors = new();
        private readonly HashSet<int> _smallRoomConnectionIndices = new();
        private readonly List<OccupiedArea> _occupiedAreas = new();
        private Transform _geometryRoot;
        private Material _specialMaterial;
        private Material _tunnelMaterial;
        private Material _corridorMaterial;
        private Material _smallRoomMaterial;
        private Material _warpCorridorMaterial;
        private Material _warpTriggerMaterial;
        private Material _markerMaterial;
        private System.Random _random;
        private GameObject _tunnelTemplate;
        private GameObject _corridorTemplate;
        private bool _corridorTemplateLongAxisIsX;
        private TunnelNode _specialTunnel;

        private static readonly Entrance[] Entrances =
        {
            new("Left +Z", new Vector2(-0.5f, 1.0f / 3.0f), Vector2.left),
            new("Left Center", new Vector2(-0.5f, 0.0f), Vector2.left),
            new("Left -Z", new Vector2(-0.5f, -1.0f / 3.0f), Vector2.left),
            new("Right +Z", new Vector2(0.5f, 1.0f / 3.0f), Vector2.right),
            new("Right Center", new Vector2(0.5f, 0.0f), Vector2.right),
            new("Right -Z", new Vector2(0.5f, -1.0f / 3.0f), Vector2.right)
        };

        // コンポーネント生成時にトンネルマップを作成する。
        private void Awake()
        {
            GenerateTunnelTestBootstrap settings = GetComponent<GenerateTunnelTestBootstrap>();
            if (settings != null)
            {
                settings.ApplyTo(this);
            }
            Generate();
        }

        internal void Configure(int configuredTunnelCount, int configuredSeed, int configuredPlacementAttempts,
            int configuredSmallRoomCount, float configuredSmallRoomWidth, float configuredSmallRoomLength,
            float configuredTunnelLength, float configuredTunnelWidth, float configuredTunnelHeight,
            float configuredCorridorLength, float configuredCorridorWidth, float configuredPlacementMargin,
            float configuredTunnelOverlapSizeMultiplier, float configuredCorridorOverlapSizeMultiplier,
            float configuredSmallRoomOverlapSizeMultiplier)
        {
            tunnelCount = configuredTunnelCount;
            seed = configuredSeed;
            placementAttemptsPerTunnel = configuredPlacementAttempts;
            smallRoomCount = configuredSmallRoomCount;
            smallRoomWidth = configuredSmallRoomWidth;
            smallRoomLength = configuredSmallRoomLength;
            tunnelLength = configuredTunnelLength;
            tunnelWidth = configuredTunnelWidth;
            tunnelHeight = configuredTunnelHeight;
            corridorLength = configuredCorridorLength;
            corridorWidth = configuredCorridorWidth;
            placementMargin = configuredPlacementMargin;
            tunnelOverlapSizeMultiplier = configuredTunnelOverlapSizeMultiplier;
            corridorOverlapSizeMultiplier = configuredCorridorOverlapSizeMultiplier;
            smallRoomOverlapSizeMultiplier = configuredSmallRoomOverlapSizeMultiplier;
        }

        // 現在の生成物を消して、シードと設定に基づくトンネルマップを再生成する。
        [ContextMenu("Regenerate")]
        public void Generate()
        {
            ClearGeneratedObjects();
            _random = new System.Random(seed);
            ConfigureFromTunnelTemplate();
            ConfigureFromCorridorTemplate();
            CreateMaterials();

            _geometryRoot = new GameObject("Tunnel Map Geometry").transform;
            _geometryRoot.SetParent(transform, false);

            // 原点は Player の開始地点。Special はこれとは別のトンネルとして一つだけ生成する。
            AddTunnel(Vector3.zero, false, "Player Start Tunnel 00");

            int requestedTunnelCount = Mathf.Max(5, tunnelCount);
            SelectSmallRoomConnections(requestedTunnelCount - 1);
            for (int i = 1; i < requestedTunnelCount; i++)
            {
                // 最後の1本は既存の端へ接続し、Special候補となる「通常接続2本」の
                // トンネルが少なくとも一つ存在するようにする。
                TunnelNode requiredParent = i == requestedTunnelCount - 1 ? GetRandomNonStartEndTunnel() : null;
                if (!TryAddConnectedTunnel(i, requiredParent))
                {
                    Debug.LogWarning($"[GenerateTunnelTest] Tunnel {i:00} could not be placed without overlap.", this);
                    break;
                }
            }

            AssignRandomSpecialTunnel();
            CreateSmallRooms();
            CreateSpecialWarpCorridors();
            CreateWarpCorridorsBetweenEnds();
            FrameSceneCamera();
            Debug.Log($"[GenerateTunnelTest] Generated {_tunnels.Count} connected tunnels and {_tunnels.Count - 1} corridors (seed: {seed}).", this);
        }

        // 既存トンネルの空き入口から新しいトンネルを1つ接続して配置する。
        private bool TryAddConnectedTunnel(int index, TunnelNode requiredParent = null)
        {
            for (int attempt = 0; attempt < placementAttemptsPerTunnel && _openEntrances.Count > 0; attempt++)
            {
                int parentOpenIndex = requiredParent != null
                    ? GetRandomOpenEntranceIndex(requiredParent)
                    : _random.Next(_openEntrances.Count);
                if (parentOpenIndex < 0)
                {
                    return false;
                }
                OpenEntrance parentOpen = _openEntrances[parentOpenIndex];
                Entrance parentEntrance = Entrances[parentOpen.EntranceIndex];
                Vector3 parentPort = GetPortPosition(parentOpen.Tunnel.Position, parentEntrance);
                Vector3 outward = ToWorldDirection(parentEntrance.Direction);

                var candidates = GetOppositeEntrances(parentEntrance.Direction);
                int childEntranceIndex = candidates[_random.Next(candidates.Count)];
                Entrance childEntrance = Entrances[childEntranceIndex];
                Vector3 childPortOffset = GetPortOffset(childEntrance);
                float connectionLength = _smallRoomConnectionIndices.Contains(index)
                    ? corridorLength * 2.0f + Mathf.Max(1.0f, smallRoomLength)
                    : corridorLength;
                Vector3 childPosition = parentPort + outward * connectionLength - childPortOffset;

                Vector3 childPort = GetPortPosition(childPosition, childEntrance);
                if (!CanPlaceTunnelAndConnection(parentOpen.Tunnel, childPosition, parentPort, childPort,
                        _smallRoomConnectionIndices.Contains(index)))
                {
                    continue;
                }

                TunnelNode child = AddTunnel(childPosition, false, $"Tunnel {index:00}");
                CreateCorridor(parentPort, childPort, index);
                parentOpen.Tunnel.ConnectionCount++;
                child.ConnectionCount++;
                parentOpen.Tunnel.Neighbors.Add(child);
                child.Neighbors.Add(parentOpen.Tunnel);
                RemoveOpenEntrance(parentOpenIndex);
                RemoveOpenEntrance(child, childEntranceIndex);
                return true;
            }

            return false;
        }

        // 生成予定の通常接続から、小部屋を挟む接続番号を重複なしで先に選ぶ。
        private void SelectSmallRoomConnections(int connectionCount)
        {
            _smallRoomConnectionIndices.Clear();
            int count = Mathf.Min(Mathf.Max(0, smallRoomCount), connectionCount);
            var indices = new List<int>(connectionCount);
            for (int i = 1; i <= connectionCount; i++)
            {
                indices.Add(i);
            }

            for (int i = indices.Count - 1; i > 0; i--)
            {
                int swapIndex = _random.Next(i + 1);
                (indices[i], indices[swapIndex]) = (indices[swapIndex], indices[i]);
            }

            for (int i = 0; i < count; i++)
            {
                _smallRoomConnectionIndices.Add(indices[i]);
            }
        }

        // Special Tunnel の接続先候補にできる、開始地点以外の端トンネルをランダムに取得する。
        private TunnelNode GetRandomNonStartEndTunnel()
        {
            var candidates = new List<TunnelNode>();
            for (int i = 1; i < _tunnels.Count; i++)
            {
                if (_tunnels[i].ConnectionCount == 1 && HasOpenEntrance(_tunnels[i]))
                {
                    candidates.Add(_tunnels[i]);
                }
            }
            return candidates.Count == 0 ? null : candidates[_random.Next(candidates.Count)];
        }

        // 通常接続が2本ありワープ先候補も足りているトンネルを、Special Tunnel として選ぶ。
        private void AssignRandomSpecialTunnel()
        {
            var candidates = new List<TunnelNode>();
            for (int i = 1; i < _tunnels.Count; i++)
            {
                TunnelNode tunnel = _tunnels[i];
                if (tunnel.ConnectionCount == 2 && CountWarpDestinationCandidates(tunnel) >= 2)
                {
                    candidates.Add(tunnel);
                }
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[GenerateTunnelTest] No tunnel with exactly two normal connections was available for the Special Tunnel.", this);
                return;
            }

            _specialTunnel = candidates[_random.Next(candidates.Count)];
            int specialIndex = _tunnels.IndexOf(_specialTunnel);
            _specialTunnel.Root.name = $"Special Tunnel {specialIndex:00}";

            foreach (Renderer renderer in _specialTunnel.Root.GetComponentsInChildren<Renderer>())
            {
                // 出入口候補の緑色は維持し、トンネル本体だけを Special の色にする。
                if (!renderer.gameObject.name.StartsWith("Entrance Candidate"))
                {
                    renderer.sharedMaterial = _specialMaterial;
                }
            }
        }

        // 指定した Special Tunnel からワープ接続できる候補トンネル数を数える。
        private int CountWarpDestinationCandidates(TunnelNode specialTunnel)
        {
            int count = 0;
            foreach (TunnelNode tunnel in _tunnels)
            {
                if (tunnel != specialTunnel && !specialTunnel.Neighbors.Contains(tunnel) && HasOpenEntrance(tunnel))
                {
                    count++;
                }
            }
            return count;
        }

        // 指定位置にトンネル本体を生成し、入口候補マーカーを登録する。
        private TunnelNode AddTunnel(Vector3 position, bool isSpecial, string objectName)
        {
            var root = new GameObject(objectName).transform;
            root.SetParent(_geometryRoot, false);
            root.localPosition = position;

            Material shellMaterial = isSpecial ? _specialMaterial : _tunnelMaterial;
            if (_tunnelTemplate != null)
            {
                GameObject model = Instantiate(_tunnelTemplate, root, false);
                model.name = "tunnelBase_tmp";
                model.SetActive(true);
                CenterModelOnLocalSpace(model.transform, root);
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = shellMaterial;
                }
                ApplyStageCollisionRecursive(model);
            }
            else
            {
                CreateStageBox(root, "Floor", Vector3.zero, new Vector3(tunnelWidth, ShellThickness, tunnelLength), shellMaterial);
                CreateStageBox(root, "Ceiling", new Vector3(0.0f, tunnelHeight, 0.0f), new Vector3(tunnelWidth, ShellThickness, tunnelLength), shellMaterial);
                CreateStageBox(root, "Left Wall", new Vector3(-tunnelWidth * 0.5f, tunnelHeight * 0.5f, 0.0f), new Vector3(ShellThickness, tunnelHeight, tunnelLength), shellMaterial);
                CreateStageBox(root, "Right Wall", new Vector3(tunnelWidth * 0.5f, tunnelHeight * 0.5f, 0.0f), new Vector3(ShellThickness, tunnelHeight, tunnelLength), shellMaterial);
            }

            var node = new TunnelNode(position, root);
            _tunnels.Add(node);
            _occupiedAreas.Add(CreateTunnelArea(position, node));

            for (int i = 0; i < Entrances.Length; i++)
            {
                _openEntrances.Add(new OpenEntrance(node, i));
                node.EntranceMarkers[i] = CreateEntranceMarker(root, Entrances[i], i);
            }

            return node;
        }

        // トンネルの入口候補位置を可視化する小さなマーカーを作成する。
        private GameObject CreateEntranceMarker(Transform tunnelRoot, Entrance entrance, int index)
        {
            Vector3 localPosition = GetPortOffset(entrance);
            localPosition.y = 0.16f;
            return CreateBox(tunnelRoot, $"Entrance Candidate {index + 1}: {entrance.Name}", localPosition,
                new Vector3(0.7f, 0.12f, 0.7f), _markerMaterial);
        }

        // 2つの入口位置の間に通常通路を生成する。
        private void CreateCorridor(Vector3 start, Vector3 end, int index)
        {
            Vector3 delta = end - start;
            Vector3 center = (start + end) * 0.5f;
            var corridor = new GameObject($"Corridor {index:00}").transform;
            corridor.SetParent(_geometryRoot, false);
            corridor.localPosition = center;
            corridor.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            corridor.gameObject.AddComponent<GeneratedCorridorInfo>().Initialize(GeneratedCorridorKind.Normal);

            CreatePassageShell(corridor, delta.magnitude, _corridorMaterial);
            _normalCorridors.Add(new NormalCorridor(corridor, delta.magnitude, index));
            AddConnectionAreas(start, end, _smallRoomConnectionIndices.Contains(index));
        }

        // 通常通路を重複なしで選び、その中央に実際に歩ける小部屋の床を生成する。
        private void CreateSmallRooms()
        {
            int roomNumber = 0;
            foreach (NormalCorridor candidate in _normalCorridors)
            {
                if (!_smallRoomConnectionIndices.Contains(candidate.ConnectionIndex))
                {
                    continue;
                }

                roomNumber++;
                float width = Mathf.Max(corridorWidth, smallRoomWidth);
                float roomLength = Mathf.Max(1.0f, smallRoomLength);

                // 先に作られていた通常通路モデルを除去し、同じ接続区間を3分割して作り直す。
                for (int childIndex = candidate.Root.childCount - 1; childIndex >= 0; childIndex--)
                {
                    Destroy(candidate.Root.GetChild(childIndex).gameObject);
                }

                candidate.Root.name = $"Small Room Connection {roomNumber:00}";
                float passageLength = corridorLength;
                CreateRoomPassage(candidate.Root, "Corridor Before Room",
                    -(roomLength + passageLength) * 0.5f, passageLength);
                CreateRoomPassage(candidate.Root, "Corridor After Room",
                    (roomLength + passageLength) * 0.5f, passageLength);

                var room = new GameObject($"Small Room {roomNumber:00}").transform;
                room.SetParent(candidate.Root, false);
                float floorThickness = ShellThickness * 1.5f;
                CreateStageBox(room, "Walkable Room Floor",
                    Vector3.zero,
                    new Vector3(width, floorThickness, roomLength), _smallRoomMaterial);
            }
        }

        private void CreateRoomPassage(Transform connectionRoot, string objectName, float localZ, float length)
        {
            var passage = new GameObject(objectName).transform;
            passage.SetParent(connectionRoot, false);
            passage.localPosition = new Vector3(0.0f, 0.0f, localZ);
            CreatePassageShell(passage, length, _corridorMaterial);
        }

        // Special Tunnel から離れたトンネルへ向かうワープ通路ペアを作成する。
        private void CreateSpecialWarpCorridors()
        {
            if (_specialTunnel == null)
            {
                return;
            }

            var destinations = new List<TunnelNode>();
            foreach (TunnelNode tunnel in _tunnels)
            {
                if (tunnel != _specialTunnel && !_specialTunnel.Neighbors.Contains(tunnel) && HasOpenEntrance(tunnel))
                {
                    destinations.Add(tunnel);
                }
            }

            for (int i = destinations.Count - 1; i > 0; i--)
            {
                int swapIndex = _random.Next(i + 1);
                (destinations[i], destinations[swapIndex]) = (destinations[swapIndex], destinations[i]);
            }

            List<int> specialEntrances = GetOpenEntranceIndices(_specialTunnel);
            for (int i = specialEntrances.Count - 1; i > 0; i--)
            {
                int swapIndex = _random.Next(i + 1);
                (specialEntrances[i], specialEntrances[swapIndex]) = (specialEntrances[swapIndex], specialEntrances[i]);
            }

            var warpEntrances = new List<int> { specialEntrances[0], specialEntrances[1] };
            DisableUnusedSpecialEntrances(warpEntrances);

            for (int pairId = 0; pairId < 2; pairId++)
            {
                GeneratedCorridorInfo specialSide = CreateWarpCorridorStub(_specialTunnel, pairId, "Special", warpEntrances[pairId]);
                GeneratedCorridorInfo destinationSide = CreateWarpCorridorStub(destinations[pairId], pairId, "Destination");
                if (specialSide != null && destinationSide != null)
                {
                    specialSide.SetPair(destinationSide);
                    destinationSide.SetPair(specialSide);
                }
            }
        }

        // 通常接続が1本だけの端トンネル同士を、ワープ通路ペアとして接続する。
        private void CreateWarpCorridorsBetweenEnds()
        {
            var endTunnels = new List<TunnelNode>();
            foreach (TunnelNode tunnel in _tunnels)
            {
                // ConnectionCount は通常通路だけを数えるため、Special用ワープの生成後でも
                // 元のマップ構造における「端」を正しく判定できる。
                if (tunnel.ConnectionCount == 1 && HasOpenEntrance(tunnel))
                {
                    endTunnels.Add(tunnel);
                }
            }

            for (int i = endTunnels.Count - 1; i > 0; i--)
            {
                int swapIndex = _random.Next(i + 1);
                (endTunnels[i], endTunnels[swapIndex]) = (endTunnels[swapIndex], endTunnels[i]);
            }

            // 0, 1 は Special Tunnel の2組で使用するため、端同士は100番台に分ける。
            int pairId = 100;
            for (int i = 0; i + 1 < endTunnels.Count; i += 2)
            {
                PairWarpCorridors(endTunnels[i], endTunnels[i + 1], pairId++);
            }

            // 端が奇数の場合も取り残さず、最後の端を先頭の端と追加で接続する。
            if (endTunnels.Count >= 3 && endTunnels.Count % 2 != 0)
            {
                PairWarpCorridors(endTunnels[^1], endTunnels[0], pairId);
            }
        }

        // 2つの端トンネルにワープ通路を作成し、互いをペアとして登録する。
        private void PairWarpCorridors(TunnelNode firstTunnel, TunnelNode secondTunnel, int pairId)
        {
            GeneratedCorridorInfo first = CreateWarpCorridorStub(firstTunnel, pairId, "End A");
            GeneratedCorridorInfo second = CreateWarpCorridorStub(secondTunnel, pairId, "End B");
            if (first != null && second != null)
            {
                first.SetPair(second);
                second.SetPair(first);
            }
        }

        // トンネルの空き入口から外側へ伸びるワープ通路を1本作成する。
        private GeneratedCorridorInfo CreateWarpCorridorStub(TunnelNode tunnel, int pairId, string sideName, int requiredEntranceIndex = -1)
        {
            int openIndex = FindAvailableWarpEntranceIndex(tunnel, requiredEntranceIndex);
            if (openIndex < 0)
            {
                Debug.LogWarning($"[GenerateTunnelTest] No non-overlapping warp corridor entrance was available on {tunnel.Root.name}.", this);
                return null;
            }

            OpenEntrance open = _openEntrances[openIndex];
            Entrance entrance = Entrances[open.EntranceIndex];
            Vector3 start = GetPortPosition(tunnel.Position, entrance);
            Vector3 outward = ToWorldDirection(entrance.Direction);
            Vector3 end = start + outward * corridorLength;
            Vector3 delta = end - start;

            var corridor = new GameObject($"Warp Corridor Pair {pairId:00} {sideName}").transform;
            corridor.SetParent(_geometryRoot, false);
            corridor.localPosition = (start + end) * 0.5f;
            corridor.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            CreatePassageShell(corridor, delta.magnitude, _warpCorridorMaterial);

            GeneratedCorridorInfo info = corridor.gameObject.AddComponent<GeneratedCorridorInfo>();
            info.Initialize(GeneratedCorridorKind.Warp, pairId);
            CreateWarpTrigger(corridor, info, delta.magnitude);
            AddConnectionAreas(start, end, false);
            RemoveOpenEntrance(openIndex);
            return info;
        }

        private int FindAvailableWarpEntranceIndex(TunnelNode tunnel, int requiredEntranceIndex)
        {
            var candidates = new List<int>();
            if (requiredEntranceIndex >= 0)
            {
                int requiredOpenIndex = FindOpenEntranceIndex(tunnel, requiredEntranceIndex);
                if (requiredOpenIndex >= 0)
                {
                    candidates.Add(requiredOpenIndex);
                }
            }
            else
            {
                for (int i = 0; i < _openEntrances.Count; i++)
                {
                    if (_openEntrances[i].Tunnel == tunnel)
                    {
                        candidates.Add(i);
                    }
                }
                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int swapIndex = _random.Next(i + 1);
                    (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
                }
            }

            foreach (int candidateIndex in candidates)
            {
                OpenEntrance open = _openEntrances[candidateIndex];
                Entrance entrance = Entrances[open.EntranceIndex];
                Vector3 start = GetPortPosition(tunnel.Position, entrance);
                Vector3 end = start + ToWorldDirection(entrance.Direction) * corridorLength;
                bool overlaps = false;
                foreach (OccupiedArea area in BuildConnectionAreas(start, end, false))
                {
                    if (OverlapsAny(area, tunnel))
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (!overlaps)
                {
                    return candidateIndex;
                }
            }
            return -1;
        }

        // 指定トンネルに残っている空き入口インデックスをすべて取得する。
        private List<int> GetOpenEntranceIndices(TunnelNode tunnel)
        {
            var result = new List<int>();
            foreach (OpenEntrance open in _openEntrances)
            {
                if (open.Tunnel == tunnel)
                {
                    result.Add(open.EntranceIndex);
                }
            }
            return result;
        }

        // 指定トンネルの指定入口が、空き入口リストの何番目にあるかを探す。
        private int FindOpenEntranceIndex(TunnelNode tunnel, int entranceIndex)
        {
            for (int i = 0; i < _openEntrances.Count; i++)
            {
                OpenEntrance open = _openEntrances[i];
                if (open.Tunnel == tunnel && open.EntranceIndex == entranceIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        // Special Tunnel でワープに使わない入口候補を消し、通常接続対象からも外す。
        private void DisableUnusedSpecialEntrances(List<int> warpEntranceIndices)
        {
            for (int i = _openEntrances.Count - 1; i >= 0; i--)
            {
                OpenEntrance open = _openEntrances[i];
                if (open.Tunnel != _specialTunnel || warpEntranceIndices.Contains(open.EntranceIndex))
                {
                    continue;
                }

                GameObject marker = _specialTunnel.EntranceMarkers[open.EntranceIndex];
                if (marker != null)
                {
                    Destroy(marker);
                }
                _openEntrances.RemoveAt(i);
            }
        }

        // 指定トンネルにまだ使える入口候補が残っているか確認する。
        private bool HasOpenEntrance(TunnelNode tunnel)
        {
            foreach (OpenEntrance open in _openEntrances)
            {
                if (open.Tunnel == tunnel)
                {
                    return true;
                }
            }
            return false;
        }

        // 指定トンネルに残っている空き入口をランダムに1つ選ぶ。
        private int GetRandomOpenEntranceIndex(TunnelNode tunnel)
        {
            var candidates = new List<int>();
            for (int i = 0; i < _openEntrances.Count; i++)
            {
                if (_openEntrances[i].Tunnel == tunnel)
                {
                    candidates.Add(i);
                }
            }
            return candidates.Count == 0 ? -1 : candidates[_random.Next(candidates.Count)];
        }

        // 通路モデルまたはフォールバック形状を、通路ルートの子として配置する。
        private void CreatePassageShell(Transform corridor, float length, Material material)
        {
            if (_corridorTemplate != null)
            {
                GameObject model = Instantiate(_corridorTemplate, corridor, false);
                model.name = "Tunnel_Path_Long";
                model.SetActive(true);
                model.transform.localRotation = _corridorTemplateLongAxisIsX
                    ? Quaternion.Euler(0.0f, 90.0f, 0.0f)
                    : Quaternion.identity;
                float lengthScale = corridorLength > Mathf.Epsilon ? length / corridorLength : 1.0f;
                model.transform.localScale = _corridorTemplateLongAxisIsX
                    ? new Vector3(lengthScale, 1.0f, 1.0f)
                    : new Vector3(1.0f, 1.0f, lengthScale);
                CenterModelOnLocalSpace(model.transform, corridor);

                // 廊下の高さ調整用
                model.transform.localPosition += new Vector3(0.0f, -0.8f, 0.0f);

                ApplyStageCollisionRecursive(model);
                CreateInvisibleStageCollider(corridor, "Walkable Floor Collider", Vector3.zero,
                    new Vector3(corridorWidth, ShellThickness * 1.5f, length));
                return;
            }

            CreateStageBox(corridor, "Floor", Vector3.zero, new Vector3(corridorWidth, ShellThickness * 1.5f, length), material);
            CreateStageBox(corridor, "Ceiling", new Vector3(0.0f, tunnelHeight, 0.0f), new Vector3(corridorWidth, ShellThickness, length), material);
        }

        // ワープ通路の中心に暗いトリガー領域と逆走防止用の見えない壁を作成する。
        private void CreateWarpTrigger(Transform corridor, GeneratedCorridorInfo info, float length)
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

        // 新しく置こうとしているトンネルが既存トンネルの範囲と重なるか調べる。
        private bool CanPlaceTunnelAndConnection(TunnelNode parent, Vector3 childPosition,
            Vector3 start, Vector3 end, bool hasSmallRoom)
        {
            if (OverlapsAny(CreateTunnelArea(childPosition, null), null))
            {
                return false;
            }

            foreach (OccupiedArea area in BuildConnectionAreas(start, end, hasSmallRoom))
            {
                if (OverlapsAny(area, parent))
                {
                    return false;
                }
            }
            return true;
        }

        private OccupiedArea CreateTunnelArea(Vector3 position, TunnelNode owner)
        {
            Vector2 size = new Vector2(tunnelWidth, tunnelLength)
                           * Mathf.Max(0.1f, tunnelOverlapSizeMultiplier)
                           + Vector2.one * placementMargin;
            return new OccupiedArea(new Vector2(position.x, position.z), size, owner);
        }

        private List<OccupiedArea> BuildConnectionAreas(Vector3 start, Vector3 end, bool hasSmallRoom)
        {
            var result = new List<OccupiedArea>(hasSmallRoom ? 3 : 1);
            Vector3 delta = end - start;
            float totalLength = delta.magnitude;
            if (totalLength <= Mathf.Epsilon)
            {
                return result;
            }

            Vector3 direction = delta / totalLength;
            if (!hasSmallRoom)
            {
                result.Add(CreateSegmentArea((start + end) * 0.5f, direction, totalLength,
                    corridorWidth, corridorOverlapSizeMultiplier));
                return result;
            }

            float roomLength = Mathf.Max(1.0f, smallRoomLength);
            float passageLength = corridorLength;
            result.Add(CreateSegmentArea(start + direction * (passageLength * 0.5f), direction,
                passageLength, corridorWidth, corridorOverlapSizeMultiplier));
            result.Add(CreateSegmentArea(start + direction * (passageLength + roomLength * 0.5f), direction,
                roomLength, Mathf.Max(corridorWidth, smallRoomWidth), smallRoomOverlapSizeMultiplier));
            result.Add(CreateSegmentArea(start + direction * (passageLength + roomLength + passageLength * 0.5f),
                direction, passageLength, corridorWidth, corridorOverlapSizeMultiplier));
            return result;
        }

        private OccupiedArea CreateSegmentArea(Vector3 center, Vector3 direction, float length,
            float width, float sizeMultiplier)
        {
            var size = new Vector2(
                Mathf.Abs(direction.x) * length + Mathf.Abs(direction.z) * width,
                Mathf.Abs(direction.z) * length + Mathf.Abs(direction.x) * width);
            size = size * Mathf.Max(0.1f, sizeMultiplier) + Vector2.one * placementMargin;
            return new OccupiedArea(new Vector2(center.x, center.z), size, null);
        }

        private bool OverlapsAny(OccupiedArea candidate, TunnelNode ignoredTunnel)
        {
            foreach (OccupiedArea occupied in _occupiedAreas)
            {
                if (ignoredTunnel != null && occupied.OwnerTunnel == ignoredTunnel)
                {
                    continue;
                }
                if (candidate.Overlaps(occupied))
                {
                    return true;
                }
            }
            return false;
        }

        private void AddConnectionAreas(Vector3 start, Vector3 end, bool hasSmallRoom)
        {
            _occupiedAreas.AddRange(BuildConnectionAreas(start, end, hasSmallRoom));
        }

        // 指定方向と反対側を向いている入口候補のインデックス一覧を取得する。
        private List<int> GetOppositeEntrances(Vector2 direction)
        {
            var result = new List<int>(3);
            for (int i = 0; i < Entrances.Length; i++)
            {
                if (Vector2.Dot(Entrances[i].Direction, direction) < -0.99f)
                {
                    result.Add(i);
                }
            }
            return result;
        }

        // トンネル中心座標と入口定義から、ワールド上の入口位置を計算する。
        private Vector3 GetPortPosition(Vector3 tunnelPosition, Entrance entrance)
        {
            return tunnelPosition + GetPortOffset(entrance);
        }

        // 入口定義の正規化座標を、現在のトンネル寸法に合わせたローカルオフセットへ変換する。
        private Vector3 GetPortOffset(Entrance entrance)
        {
            return new Vector3(entrance.NormalizedPosition.x * tunnelWidth, 0.0f,
                entrance.NormalizedPosition.y * tunnelLength);
        }

        // 2Dの入口方向を、XZ平面上の3D方向ベクトルへ変換する。
        private static Vector3 ToWorldDirection(Vector2 direction)
        {
            return new Vector3(direction.x, 0.0f, direction.y);
        }

        // 空き入口リストから指定位置の要素を削除する。
        private void RemoveOpenEntrance(int index)
        {
            _openEntrances.RemoveAt(index);
        }

        // 指定トンネルの指定入口を空き入口リストから削除する。
        private void RemoveOpenEntrance(TunnelNode tunnel, int entranceIndex)
        {
            for (int i = _openEntrances.Count - 1; i >= 0; i--)
            {
                OpenEntrance open = _openEntrances[i];
                if (open.Tunnel == tunnel && open.EntranceIndex == entranceIndex)
                {
                    _openEntrances.RemoveAt(i);
                    return;
                }
            }
        }

        // 生成物に使う一時マテリアルを作成する。
        private void CreateMaterials()
        {
            _specialMaterial = CreateMaterial("Special Tunnel", new Color(0.16f, 0.48f, 0.68f));
            _tunnelMaterial = CreateMaterial("Tunnel", new Color(0.23f, 0.26f, 0.29f));
            _corridorMaterial = CreateMaterial("Connecting Corridor", new Color(0.72f, 0.48f, 0.13f));
            _smallRoomMaterial = CreateMaterial("Small Room Floor", new Color(0.38f, 0.34f, 0.25f));
            _warpCorridorMaterial = CreateMaterial("Warp Corridor", new Color(0.62f, 0.2f, 0.82f));
            _warpTriggerMaterial = CreateMaterial("Warp Trigger Center", new Color(0.01f, 0.0f, 0.015f));
            _markerMaterial = CreateMaterial("Entrance Candidate", new Color(0.1f, 0.9f, 0.65f));
        }

        // シーン内またはプロジェクト内のトンネルモデルを取得し、生成寸法へ反映する。
        private void ConfigureFromTunnelTemplate()
        {
            if (_tunnelTemplate == null)
            {
                _tunnelTemplate = GameObject.Find("tunnelBase_tmp");
            }
            if (_tunnelTemplate == null)
            {
                _tunnelTemplate = LoadProjectAsset(TunnelTemplateAssetPath);
            }
            if (_tunnelTemplate == null)
            {
                Debug.LogWarning("[GenerateTunnelTest] tunnelBase_tmp was not found. Using the fallback tunnel dimensions.", this);
                return;
            }

            Renderer templateRenderer = _tunnelTemplate.GetComponentInChildren<Renderer>();
            if (templateRenderer != null)
            {
                // Renderer.bounds は Transform の (5, 1, 1) と Y=90° を適用済み。
                // これにより FBX 側の寸法が変わっても、生成トンネルは常に見本と同じ長さになる。
                Bounds bounds = templateRenderer.bounds;
                tunnelLength = Mathf.Max(bounds.size.x, bounds.size.z);
                tunnelWidth = Mathf.Min(bounds.size.x, bounds.size.z);
                tunnelHeight = Mathf.Max(tunnelHeight, bounds.size.y);
            }

            if (_tunnelTemplate.scene.IsValid())
            {
                _tunnelTemplate.SetActive(false);
            }
            DisableTemplateMapRootForGeneratedStage();
        }

        // シーン内またはプロジェクト内の通路モデルを取得し、接続距離と通路幅へ反映する。
        private void ConfigureFromCorridorTemplate()
        {
            if (_corridorTemplate == null)
            {
                _corridorTemplate = GameObject.Find("Tunnel_Path_Long");
            }
            if (_corridorTemplate == null)
            {
                _corridorTemplate = LoadProjectAsset(CorridorTemplateAssetPath);
            }
            if (_corridorTemplate == null)
            {
                Debug.LogWarning("[GenerateTunnelTest] Tunnel_Path_Long was not found. Using the fallback corridor boxes.", this);
                return;
            }

            Renderer templateRenderer = _corridorTemplate.GetComponentInChildren<Renderer>();
            if (templateRenderer != null)
            {
                Bounds bounds = templateRenderer.bounds;
                _corridorTemplateLongAxisIsX = bounds.size.x >= bounds.size.z;
                corridorLength = Mathf.Max(bounds.size.x, bounds.size.z);
                corridorWidth = Mathf.Max(1.0f, Mathf.Min(bounds.size.x, bounds.size.z));
                tunnelHeight = Mathf.Max(tunnelHeight, bounds.size.y);
            }

            if (_corridorTemplate.scene.IsValid())
            {
                _corridorTemplate.SetActive(false);
            }
        }

        // LatestStageGenerateTemp に残っている手動配置の MapRoot を、生成マップと重ならないよう非表示にする。
        private void DisableTemplateMapRootForGeneratedStage()
        {
            if (SceneManager.GetActiveScene().name != "LatestStageGenerateTemp" || _tunnelTemplate == null)
            {
                return;
            }

            Transform parent = _tunnelTemplate.transform.parent;
            if (parent != null && parent.name == "MapRoot")
            {
                parent.gameObject.SetActive(false);
            }
        }

        // 指定色のランタイム用マテリアルを作成する。
        private static Material CreateMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = materialName, color = color };
            return material;
        }

        // 見た目用のCubeを作成し、コライダーを削除して配置する。
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

        // Stageレイヤーの当たり判定を持つCubeを作成する。
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

        // 見えないStageレイヤーのBoxColliderを作成する。
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

        // モデルのメッシュ中心が指定ローカル空間の原点に合うよう、モデル位置を補正する。
        private static void CenterModelOnLocalSpace(Transform model, Transform localSpace)
        {
            if (!TryGetMeshBoundsInLocalSpace(model, localSpace, out Bounds localBounds))
            {
                return;
            }

            Vector3 localPosition = model.localPosition;
            localPosition.x -= localBounds.center.x;
            localPosition.z -= localBounds.center.z;
            model.localPosition = localPosition;
        }

        // モデル配下の全メッシュBoundsを、指定ローカル空間でまとめて取得する。
        private static bool TryGetMeshBoundsInLocalSpace(
            Transform model,
            Transform localSpace,
            out Bounds localBounds)
        {
            localBounds = default;
            MeshFilter[] meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
            bool hasBounds = false;

            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

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

        // モデル配下をStageレイヤーにし、不足しているMeshColliderを追加する。
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

            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null || meshFilter.GetComponent<Collider>() != null)
                {
                    continue;
                }

                MeshCollider collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
            }
        }

        // Unity Editor上でプロジェクト内アセットをGameObjectとして読み込む。
        private static GameObject LoadProjectAsset(string assetPath)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#else
            return null;
#endif
        }

        // 生成されたトンネル全体が見えるよう、メインカメラの位置とクリップ距離を調整する。
        private void FrameSceneCamera()
        {
            Camera camera = Camera.main;
            if (camera == null || _tunnels.Count == 0)
            {
                return;
            }

            Bounds bounds = new Bounds(_tunnels[0].Position, new Vector3(tunnelWidth, tunnelHeight, tunnelLength));
            for (int i = 1; i < _tunnels.Count; i++)
            {
                bounds.Encapsulate(new Bounds(_tunnels[i].Position, new Vector3(tunnelWidth, tunnelHeight, tunnelLength)));
            }

            camera.transform.position = bounds.center + new Vector3(0.0f, Mathf.Max(55.0f, bounds.size.magnitude * 0.8f), -bounds.size.z * 0.25f);
            camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, bounds.size.magnitude * 3.0f);
        }

        // 現在保持している生成データと生成済みオブジェクトを破棄する。
        private void ClearGeneratedObjects()
        {
            _tunnels.Clear();
            _openEntrances.Clear();
            _normalCorridors.Clear();
            _smallRoomConnectionIndices.Clear();
            _occupiedAreas.Clear();
            _specialTunnel = null;
            if (_geometryRoot != null)
            {
                Destroy(_geometryRoot.gameObject);
                _geometryRoot = null;
            }
        }

        private readonly struct OccupiedArea
        {
            public Vector2 Center { get; }
            public Vector2 Size { get; }
            public TunnelNode OwnerTunnel { get; }

            public OccupiedArea(Vector2 center, Vector2 size, TunnelNode ownerTunnel)
            {
                Center = center;
                Size = size;
                OwnerTunnel = ownerTunnel;
            }

            public bool Overlaps(OccupiedArea other)
            {
                Vector2 distance = Center - other.Center;
                Vector2 minimumDistance = (Size + other.Size) * 0.5f;
                return Mathf.Abs(distance.x) < minimumDistance.x
                       && Mathf.Abs(distance.y) < minimumDistance.y;
            }
        }

        private readonly struct NormalCorridor
        {
            public Transform Root { get; }
            public float Length { get; }
            public int ConnectionIndex { get; }

            public NormalCorridor(Transform root, float length, int connectionIndex)
            {
                Root = root;
                Length = length;
                ConnectionIndex = connectionIndex;
            }
        }

        private readonly struct Entrance
        {
            public readonly string Name;
            public readonly Vector2 NormalizedPosition;
            public readonly Vector2 Direction;

            // 入口候補の表示名、トンネル内の正規化位置、外向き方向を保持する。
            public Entrance(string name, Vector2 normalizedPosition, Vector2 direction)
            {
                Name = name;
                NormalizedPosition = normalizedPosition;
                Direction = direction;
            }
        }

        private sealed class TunnelNode
        {
            public Vector3 Position { get; }
            public Transform Root { get; }
            public int ConnectionCount { get; set; }
            public HashSet<TunnelNode> Neighbors { get; } = new();
            public GameObject[] EntranceMarkers { get; } = new GameObject[Entrances.Length];

            // 生成済みトンネルの中心位置とルートTransformを保持する。
            public TunnelNode(Vector3 position, Transform root)
            {
                Position = position;
                Root = root;
            }
        }

        private readonly struct OpenEntrance
        {
            public readonly TunnelNode Tunnel;
            public readonly int EntranceIndex;

            // まだ接続に使えるトンネル入口を表す。
            public OpenEntrance(TunnelNode tunnel, int entranceIndex)
            {
                Tunnel = tunnel;
                EntranceIndex = entranceIndex;
            }
        }
    }
}
