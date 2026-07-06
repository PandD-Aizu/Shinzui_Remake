using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        public void Initialize(GeneratedCorridorKind corridorKind, int pairId = -1)
        {
            kind = corridorKind;
            warpPairId = pairId;
        }

        public void SetPair(GeneratedCorridorInfo pair)
        {
            pairedCorridor = pair;
        }
    }

    /// <summary>
    /// GenerateTunnelTest を単独で再生したときだけ、確認用のトンネル生成器を追加する。
    /// シーンに専用オブジェクトを保存しないため、生成パラメータの実験でシーンが汚れない。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GenerateTunnelTestBootstrap : MonoBehaviour
    {
        private const string TargetSceneName = "GenerateTunnelTest";
        private const string InstanceName = "[Generated Tunnel Map]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoaded()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapActiveScene()
        {
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreate(scene);
        }

        private static void TryCreate(Scene scene)
        {
            if (!scene.IsValid() || scene.name != TargetSceneName || GameObject.Find(InstanceName) != null)
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

        [Header("Fixed tunnel size")]
        [Min(4.0f)] [SerializeField] private float tunnelLength = 153.12695f;
        [Min(3.0f)] [SerializeField] private float tunnelWidth = 14.800003f;
        [Min(2.0f)] [SerializeField] private float tunnelHeight = 5.0f;
        [Min(1.0f)] [SerializeField] private float corridorLength = 8.0f;
        [Min(1.0f)] [SerializeField] private float corridorWidth = 3.0f;
        [Min(0.0f)] [SerializeField] private float placementMargin = 2.0f;

        private const float ShellThickness = 0.2f;

        private readonly List<TunnelNode> _tunnels = new();
        private readonly List<OpenEntrance> _openEntrances = new();
        private Transform _geometryRoot;
        private Material _specialMaterial;
        private Material _tunnelMaterial;
        private Material _corridorMaterial;
        private Material _warpCorridorMaterial;
        private Material _markerMaterial;
        private System.Random _random;
        private GameObject _tunnelTemplate;
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

        private void Awake()
        {
            Generate();
        }

        [ContextMenu("Regenerate")]
        public void Generate()
        {
            ClearGeneratedObjects();
            _random = new System.Random(seed);
            ConfigureFromTunnelTemplate();
            CreateMaterials();

            _geometryRoot = new GameObject("Tunnel Map Geometry").transform;
            _geometryRoot.SetParent(transform, false);

            // 原点は Player の開始地点。Special はこれとは別のトンネルとして一つだけ生成する。
            AddTunnel(Vector3.zero, false, "Player Start Tunnel 00");

            int requestedTunnelCount = Mathf.Max(5, tunnelCount);
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
            CreateSpecialWarpCorridors();
            CreateWarpCorridorsBetweenEnds();
            FrameSceneCamera();
            Debug.Log($"[GenerateTunnelTest] Generated {_tunnels.Count} connected tunnels and {_tunnels.Count - 1} corridors (seed: {seed}).", this);
        }

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
                Vector3 childPosition = parentPort + outward * corridorLength - childPortOffset;

                if (OverlapsExistingTunnel(childPosition))
                {
                    continue;
                }

                TunnelNode child = AddTunnel(childPosition, false, $"Tunnel {index:00}");
                CreateCorridor(parentPort, GetPortPosition(child.Position, childEntrance), index);
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
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = shellMaterial;
                }
            }
            else
            {
                CreateBox(root, "Floor", Vector3.zero, new Vector3(tunnelWidth, ShellThickness, tunnelLength), shellMaterial);
                CreateBox(root, "Ceiling", new Vector3(0.0f, tunnelHeight, 0.0f), new Vector3(tunnelWidth, ShellThickness, tunnelLength), shellMaterial);
                CreateBox(root, "Left Wall", new Vector3(-tunnelWidth * 0.5f, tunnelHeight * 0.5f, 0.0f), new Vector3(ShellThickness, tunnelHeight, tunnelLength), shellMaterial);
                CreateBox(root, "Right Wall", new Vector3(tunnelWidth * 0.5f, tunnelHeight * 0.5f, 0.0f), new Vector3(ShellThickness, tunnelHeight, tunnelLength), shellMaterial);
            }

            var node = new TunnelNode(position, root);
            _tunnels.Add(node);

            for (int i = 0; i < Entrances.Length; i++)
            {
                _openEntrances.Add(new OpenEntrance(node, i));
                node.EntranceMarkers[i] = CreateEntranceMarker(root, Entrances[i], i);
            }

            return node;
        }

        private GameObject CreateEntranceMarker(Transform tunnelRoot, Entrance entrance, int index)
        {
            Vector3 localPosition = GetPortOffset(entrance);
            localPosition.y = 0.16f;
            return CreateBox(tunnelRoot, $"Entrance Candidate {index + 1}: {entrance.Name}", localPosition,
                new Vector3(0.7f, 0.12f, 0.7f), _markerMaterial);
        }

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
        }

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

        private GeneratedCorridorInfo CreateWarpCorridorStub(TunnelNode tunnel, int pairId, string sideName, int requiredEntranceIndex = -1)
        {
            int openIndex = requiredEntranceIndex >= 0
                ? FindOpenEntranceIndex(tunnel, requiredEntranceIndex)
                : GetRandomOpenEntranceIndex(tunnel);
            if (openIndex < 0)
            {
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
            RemoveOpenEntrance(openIndex);
            return info;
        }

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

        private void CreatePassageShell(Transform corridor, float length, Material material)
        {
            CreateBox(corridor, "Floor", Vector3.zero, new Vector3(corridorWidth, ShellThickness * 1.5f, length), material);
            CreateBox(corridor, "Ceiling", new Vector3(0.0f, tunnelHeight, 0.0f), new Vector3(corridorWidth, ShellThickness, length), material);
        }

        private bool OverlapsExistingTunnel(Vector3 candidate)
        {
            float minimumX = tunnelWidth + placementMargin;
            float minimumZ = tunnelLength + placementMargin;
            foreach (TunnelNode tunnel in _tunnels)
            {
                Vector3 delta = candidate - tunnel.Position;
                if (Mathf.Abs(delta.x) < minimumX && Mathf.Abs(delta.z) < minimumZ)
                {
                    return true;
                }
            }

            return false;
        }

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

        private Vector3 GetPortPosition(Vector3 tunnelPosition, Entrance entrance)
        {
            return tunnelPosition + GetPortOffset(entrance);
        }

        private Vector3 GetPortOffset(Entrance entrance)
        {
            return new Vector3(entrance.NormalizedPosition.x * tunnelWidth, 0.0f,
                entrance.NormalizedPosition.y * tunnelLength);
        }

        private static Vector3 ToWorldDirection(Vector2 direction)
        {
            return new Vector3(direction.x, 0.0f, direction.y);
        }

        private void RemoveOpenEntrance(int index)
        {
            _openEntrances.RemoveAt(index);
        }

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

        private void CreateMaterials()
        {
            _specialMaterial = CreateMaterial("Special Tunnel", new Color(0.16f, 0.48f, 0.68f));
            _tunnelMaterial = CreateMaterial("Tunnel", new Color(0.23f, 0.26f, 0.29f));
            _corridorMaterial = CreateMaterial("Connecting Corridor", new Color(0.72f, 0.48f, 0.13f));
            _warpCorridorMaterial = CreateMaterial("Warp Corridor", new Color(0.62f, 0.2f, 0.82f));
            _markerMaterial = CreateMaterial("Entrance Candidate", new Color(0.1f, 0.9f, 0.65f));
        }

        private void ConfigureFromTunnelTemplate()
        {
            if (_tunnelTemplate == null)
            {
                _tunnelTemplate = GameObject.Find("tunnelBase_tmp");
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

            _tunnelTemplate.SetActive(false);
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

        private void ClearGeneratedObjects()
        {
            _tunnels.Clear();
            _openEntrances.Clear();
            _specialTunnel = null;
            if (_geometryRoot != null)
            {
                Destroy(_geometryRoot.gameObject);
                _geometryRoot = null;
            }
        }

        private readonly struct Entrance
        {
            public readonly string Name;
            public readonly Vector2 NormalizedPosition;
            public readonly Vector2 Direction;

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

            public OpenEntrance(TunnelNode tunnel, int entranceIndex)
            {
                Tunnel = tunnel;
                EntranceIndex = entranceIndex;
            }
        }
    }
}
