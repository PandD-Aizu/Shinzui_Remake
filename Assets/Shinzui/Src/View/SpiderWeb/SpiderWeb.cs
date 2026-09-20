using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shinzui.View
{
    /// <summary>
    /// 不規則な蜘蛛の巣の生成、糸の物理、描画、プレイヤー拘束を一つにまとめたコンポーネント
    /// ローカルXY平面上に巣を生成し、ローカルZ方向を巣の法線として扱う
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    [RequireComponent(typeof(SpiderWebInteractable), typeof(SpiderWebBurnVfx))]
    public sealed class SpiderWeb : MonoBehaviour
    {
        [Serializable]
        private struct WebNode
        {
            public Vector3 Position;
            public Vector3 PreviousPosition;
            public Vector3 RestPosition;
            public float InverseMass;
            public float NoisePhase;
            public bool AttachmentCandidate;
            public bool KeepFixedWhenProjectionMisses;
        }

        [Serializable]
        private struct WebConstraint
        {
            public int A;
            public int B;
            public float RestLength;
            public float ThicknessScale;
            public float Shade;
        }

        // Rendering samples interpolate existing simulation nodes only. They never add
        // masses, constraints, collision sections or random draws to the simulation.
        private struct RenderPoint
        {
            public int A;
            public int B;
            public int C;
            public int D;
            public float Around;
            public float Along;
        }

        private struct RenderStrand
        {
            public int A;
            public int B;
            public int Segments;
            public int FirstVertex;
            public float Thickness;
            public float Shade;
            public float Sag;
            public float Depth;
            public float EndFraction;
        }

        [Header("Shape")]
        [SerializeField, Min(0.5f)] private float width = 5.5f;
        [SerializeField, Min(0.5f)] private float height = 4.0f;
        [SerializeField, Range(6, 32)] private int radialCount = 15;
        [SerializeField, Range(3, 18)] private int ringCount = 9;
        [SerializeField] private int seed = 1701;
        [SerializeField, Range(0.0f, 0.35f)] private float irregularity = 0.16f;
        [SerializeField, Range(0.0f, 0.5f)] private float missingThreadChance = 0.08f;
        [SerializeField, Range(0.0f, 0.5f)] private float centerOffset = 0.12f;
        [SerializeField, Range(0.0f, 1.0f)] private float edgeAttachmentChance = 0.58f;
        [SerializeField, Range(0.0f, 0.75f)] private float unattachedEdgeSag = 0.24f;
        [SerializeField, Range(0.0f, 1.0f)] private float outerThreadBreakChance = 0.38f;

        [Header("Manual Anchor Generation")]
        [Tooltip("3点以上を登録で、接着点の輪郭から蜘蛛の巣を生成する（アンカー数の上限なし）")]
        [SerializeField] private List<Transform> manualAnchors = new();

        [Header("Surface Attachment")]
        [Tooltip("蜘蛛の巣を貼り付ける壁・床のCollider（MeshColliderにも対応）")]
        [SerializeField] private Collider attachmentSurface;
        [SerializeField] private LayerMask attachmentLayers = ~0;
        [SerializeField, Min(0.05f)] private float surfaceProbeDistance = 2.0f;
        [SerializeField, Min(0.0f)] private float surfaceOffset = 0.012f;
        [SerializeField] private bool conformThreadsToSurface = true;
        [SerializeField] private bool conformOnRegenerate = true;
        [SerializeField] private bool followSurfaceTransform = false;

        [Header("Thread Rendering")]
        [SerializeField, Min(0.0005f)] private float threadWidth = 0.0015f;
        [SerializeField, Range(0.0f, 1.0f)] private float threadWidthVariation = 0.38f;
        [SerializeField] private Material webMaterial;
        [SerializeField] private Color webColor = new(0.82f, 0.88f, 0.9f, 0.78f);
        [Tooltip("横糸の太さ。支え糸を1とした比率")]
        [SerializeField, Range(0.35f, 0.8f)] private float captureThreadWidthRatio = 0.55f;
        [Tooltip("横糸の長さに対する描画上のたわみ。物理へは影響しない")]
        [SerializeField, Range(0.0f, 0.06f)] private float captureThreadSag = 0.025f;
        [SerializeField, Range(3, 4)] private int captureCurveSegments = 4;
        [Tooltip("細い糸の最小描画幅。拡張した幅は透明度で面積補正する")]
        [SerializeField, Range(0.0f, 2.0f)] private float minimumThreadPixels = 1.5f;
        [Tooltip("横糸の接点を放射糸に沿ってずらす量（リング間隔比）")]
        [SerializeField, Range(0.0f, 0.35f)] private float ringPhaseVariation = 0.18f;

        [Header("Simulation")]
        [SerializeField, Range(1, 16)] private int constraintIterations = 7;
        [SerializeField, Range(0.0f, 1.0f)] private float stiffness = 0.88f;
        [SerializeField, Range(0.8f, 1.0f)] private float damping = 0.972f;
        [SerializeField, Range(0.0f, 1.0f)] private float gravityScale = 0.05f;
        [SerializeField, Range(0.0f, 2.0f)] private float ambientMotion = 0.16f;
        [SerializeField, Range(0.0f, 4.0f)] private float ambientFrequency = 0.72f;
        [SerializeField, Min(0.1f)] private float maxDeflection = 1.35f;
        [SerializeField] private bool simulateOffscreen = false;

        [Header("Player Interaction")]
        [SerializeField, Min(0.1f)] private float triggerDepth = 1.15f;
        [SerializeField, Min(0.1f)] private float contactRadius = 1.05f;
        [SerializeField, Range(0.0f, 1.0f)] private float trappedSpeedMultiplier = 0.22f;
        [SerializeField, Min(0.0f)] private float pullStrength = 4.0f;
        [SerializeField, Min(0.0f)] private float maxPullSpeed = 2.2f;
        [SerializeField, Min(0.0f)] private float deformationResponse = 12.0f;
        [SerializeField, Min(0.0f)] private float releaseImpulse = 0.28f;

        [Header("Burning")]
        [SerializeField, Min(0.1f)] private float burnFadeDuration = 1.35f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private WebNode[] _nodes = Array.Empty<WebNode>();
        private WebConstraint[] _constraints = Array.Empty<WebConstraint>();
        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private BoxCollider _trigger;
        private Material _runtimeMaterial;
        private Vector3[] _vertices = Array.Empty<Vector3>();
        private Vector3[] _normals = Array.Empty<Vector3>();
        private Vector4[] _tangents = Array.Empty<Vector4>();
        private Vector2[] _uvs = Array.Empty<Vector2>();
        private Color[] _colors = Array.Empty<Color>();
        private int[] _triangles = Array.Empty<int>();
        private RenderPoint[] _renderPoints = Array.Empty<RenderPoint>();
        private Vector3[] _renderPositions = Array.Empty<Vector3>();
        private RenderStrand[] _renderStrands = Array.Empty<RenderStrand>();
        private int _renderRadialCount;
        private readonly Dictionary<PlayerView, int> _playerContactCounts = new();
        private readonly List<MeshCollider> _manualTriggers = new();
        private readonly List<Mesh> _manualTriggerMeshes = new();
        private readonly Plane[] _cameraFrustumPlanes = new Plane[6];
        private int _sourceId;
        private bool _generated;
        private bool _isRegenerating;
        private bool _isConfiguringTrigger;
        private bool _allowTriggerComponentChanges = true;
        private int _manualAnchorPoseHash;
        private bool _isBurning;
        private Material _burnMaterial;

        public float Width => width;
        public float Height => height;
        public int NodeCount => _nodes.Length;
        public int ThreadCount => _constraints.Length;
        public int RenderRadialCount => _renderRadialCount;
        public int RenderStrandCount => _renderStrands.Length;
        public Collider AttachmentSurface => attachmentSurface;
        public float SurfaceOffset => surfaceOffset;
        public IReadOnlyList<Transform> ManualAnchors => manualAnchors;
        public bool UsesManualAnchors => GetValidManualAnchorCount() >= 3;
        public bool IsBurning => _isBurning;
        public int InteractionColliderCount
        {
            get
            {
                int count = _trigger != null && _trigger.enabled ? 1 : 0;
                foreach (MeshCollider trigger in _manualTriggers)
                {
                    if (trigger != null && trigger.enabled)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        private void OnEnable()
        {
            _sourceId = GetEntityId().GetHashCode();
            CacheComponents();
            ConfigureTrigger();
            EnsureMaterial();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            if (!_generated || _nodes.Length == 0 || _mesh == null)
            {
                Regenerate();
            }
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            ReleaseAllPlayers();
            DestroyRuntimeResources();
        }

        private void OnValidate()
        {
            width = Mathf.Max(0.5f, width);
            height = Mathf.Max(0.5f, height);
            threadWidth = Mathf.Max(0.0005f, threadWidth);
            triggerDepth = Mathf.Max(0.1f, triggerDepth);
            contactRadius = Mathf.Max(0.1f, contactRadius);
            maxDeflection = Mathf.Max(0.1f, maxDeflection);
            surfaceProbeDistance = Mathf.Max(0.05f, surfaceProbeDistance);
            surfaceOffset = Mathf.Max(0.0f, surfaceOffset);
            captureThreadWidthRatio = Mathf.Clamp(captureThreadWidthRatio, 0.35f, 0.8f);
            captureThreadSag = Mathf.Clamp(captureThreadSag, 0.0f, 0.06f);
            captureCurveSegments = Mathf.Clamp(captureCurveSegments, 3, 4);
            minimumThreadPixels = Mathf.Clamp(minimumThreadPixels, 0.0f, 2.0f);
            ringPhaseVariation = Mathf.Clamp(ringPhaseVariation, 0.0f, 0.35f);

            bool previousAllowTriggerComponentChanges =
                _allowTriggerComponentChanges;
            _allowTriggerComponentChanges = false;
            try
            {
                CacheComponents();
                ConfigureTrigger();
                if (isActiveAndEnabled)
                {
                    Regenerate();
                }
            }
            finally
            {
                _allowTriggerComponentChanges =
                    previousAllowTriggerComponentChanges;
            }
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying || _nodes.Length == 0)
            {
                return;
            }

            bool hasPlayerContact = _playerContactCounts.Count > 0;
            if (!simulateOffscreen && !hasPlayerContact &&
                _meshRenderer != null && !_meshRenderer.isVisible)
            {
                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            Integrate(deltaTime);
            ApplyPlayerInteraction(deltaTime);
            SolveConstraints();
            ClampDeflection();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying && !_isRegenerating)
            {
                int anchorHash = ComputeManualAnchorPoseHash();
                if (anchorHash != _manualAnchorPoseHash)
                {
                    Regenerate();
                    return;
                }
            }

            // SRP updates immediately before each camera culls/renders the web. Avoid
            // uploading every vertex again in LateUpdate for the same camera.
            if (_generated && _mesh != null && GraphicsSettings.currentRenderPipeline == null)
            {
                UpdateMeshGeometry();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PlayerView player = other.GetComponentInParent<PlayerView>();
            if (player != null)
            {
                _playerContactCounts.TryGetValue(player, out int contactCount);
                _playerContactCounts[player] = contactCount + 1;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            PlayerView player = other.GetComponentInParent<PlayerView>();
            if (player != null && !_playerContactCounts.ContainsKey(player))
            {
                // 有効化直後など、Enterを受け取れなかった場合の保険。
                _playerContactCounts[player] = 1;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerView player = other.GetComponentInParent<PlayerView>();
            if (player == null)
            {
                return;
            }

            if (!_playerContactCounts.TryGetValue(player, out int contactCount))
            {
                return;
            }

            contactCount--;
            if (contactCount > 0)
            {
                _playerContactCounts[player] = contactCount;
                return;
            }

            _playerContactCounts.Remove(player);
            player.RemoveMotionModifier(_sourceId);
            AddReleaseImpulse(GetPlayerBounds(player).center);
        }

        /// <summary>
        /// 現在の設定とSeedから蜘蛛の巣を再生成する
        /// </summary>
        public void Regenerate()
        {
            if (_isRegenerating)
            {
                return;
            }

            _isRegenerating = true;
            try
            { 
                CacheComponents();
                ConfigureTrigger();
                EnsureMaterial();

                var random = new System.Random(seed);
                List<Vector3> anchorPositions = GetSortedManualAnchorPositions();
                int generatedRadialCount =
                    anchorPositions.Count >= 3 ? anchorPositions.Count : radialCount;
                var nodes = new List<WebNode>(1 + generatedRadialCount * ringCount);
                var constraints = new List<WebConstraint>(
                    generatedRadialCount * ringCount * 2 + generatedRadialCount);

                Vector3 center;
                if (anchorPositions.Count >= 3)
                {
                    center = Vector3.zero;
                    foreach (Vector3 anchorPosition in anchorPositions)
                    {
                        center += anchorPosition;
                    }
                    center /= anchorPositions.Count;
                }
                else
                {
                    center = new Vector3(
                        NextSigned(random) * width * centerOffset,
                        NextSigned(random) * height * centerOffset,
                        0.0f);
                }
                nodes.Add(CreateNode(center, false, random));

                float angleOffset = NextRange(random, 0.0f, Mathf.PI * 2.0f);
                Vector2 webCenter = new(center.x, center.y);
                for (int radial = 0; radial < generatedRadialCount; radial++)
                {
                    bool manual = anchorPositions.Count >= 3;
                    Vector3 anchorPosition = manual ? anchorPositions[radial] : Vector3.zero;
                    float angle;
                    float edgeDistance = 0.0f;
                    Vector2 direction;
                    if (manual)
                    {
                        Vector2 delta = new(
                            anchorPosition.x - center.x,
                            anchorPosition.y - center.y);
                        direction = delta.sqrMagnitude > 0.000001f
                            ? delta.normalized
                            : Vector2.right;
                        angle = Mathf.Atan2(direction.y, direction.x);
                    }
                    else
                    {
                        float baseAngle =
                            angleOffset + Mathf.PI * 2.0f * radial / generatedRadialCount;
                        float angleJitter = NextSigned(random) * irregularity *
                                            (Mathf.PI * 2.0f / generatedRadialCount) * 0.65f;
                        angle = baseAngle + angleJitter;
                        direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        edgeDistance = GetRectangleEdgeDistance(webCenter, direction);
                    }

                    int previousIndex = 0;
                    for (int ring = 1; ring <= ringCount; ring++)
                    {
                        float ring01 = ring / (float)ringCount;
                        float radius01 = Mathf.Pow(ring01, 0.91f);
                        float radialJitter = ring == ringCount
                            ? 0.0f
                            : NextSigned(random) * irregularity / ringCount;
                        Vector2 tangent = new(-direction.y, direction.x);
                        float tangentJitter = NextSigned(random) * irregularity *
                                              Mathf.Min(width, height) * ring01 * 0.12f;
                        Vector3 point3;
                        bool attachmentCandidate = false;
                        if (manual)
                        {
                            point3 = Vector3.Lerp(center, anchorPosition, radius01);
                            if (ring < ringCount)
                            {
                                point3.x += tangent.x * tangentJitter;
                                point3.y += tangent.y * tangentJitter;
                            }
                            attachmentCandidate = ring == ringCount;
                        }
                        else
                        {
                            float radius =
                                edgeDistance * Mathf.Clamp01(radius01 + radialJitter);
                            Vector2 point = webCenter +
                                            direction * radius +
                                            tangent * tangentJitter;
                            if (ring == ringCount)
                            {
                                point = GetRectangleEdgePoint(webCenter, direction);
                            }

                            bool attached = ring == ringCount &&
                                            random.NextDouble() < edgeAttachmentChance;
                            if (ring == ringCount && !attached)
                            {
                                point = ApplyUnattachedEdgeSag(point, webCenter, random);
                            }
                            attachmentCandidate = attached;
                            point3 = new Vector3(point.x, point.y, 0.0f);
                        }

                        int nodeIndex = nodes.Count;
                        nodes.Add(CreateNode(
                            point3,
                            attachmentCandidate,
                            random,
                            manual && ring == ringCount));
                        AddConstraint(constraints, nodes, previousIndex, nodeIndex, random, 1.15f);
                        previousIndex = nodeIndex;
                    }
                }

                for (int ring = 1; ring <= ringCount; ring++)
                {
                    for (int radial = 0; radial < generatedRadialCount; radial++)
                    {
                        int nextRadial = (radial + 1) % generatedRadialCount;
                        int a = GetNodeIndex(radial, ring);
                        int b = GetNodeIndex(nextRadial, ring);
                        float omission = ring == ringCount
                            ? Mathf.Max(missingThreadChance, outerThreadBreakChance)
                            : missingThreadChance;
                        if (random.NextDouble() >= omission)
                        {
                            AddConstraint(constraints, nodes, a, b, random, 0.9f);
                        }

                        // 所々に斜めの補助糸を入れ、規則正しいCG感を崩す
                        if (ring > 2 && ring < ringCount &&
                            random.NextDouble() < 0.13)
                        {
                            int diagonal = GetNodeIndex(nextRadial, ring - 1);
                            AddConstraint(constraints, nodes, a, diagonal, random, 0.72f);
                        }
                    }
                }

                _nodes = nodes.ToArray();
                _constraints = constraints.ToArray();
                if (conformOnRegenerate && conformThreadsToSurface &&
                    attachmentSurface != null)
                {
                    ConformNodesToSurface();
                    RecalculateConstraintLengths();
                }
                BuildRenderTopology(generatedRadialCount);
                BuildMesh();
                _generated = true;
                _manualAnchorPoseHash = ComputeManualAnchorPoseHash();
                UpdateMeshGeometry();
            }
            finally
            {
                _isRegenerating = false;
            }
        }

        public void AddManualAnchor(Transform anchor)
        {
            if (anchor == null || manualAnchors.Contains(anchor))
            {
                return;
            }

            manualAnchors.Add(anchor);
            Regenerate();
        }

        public void ClearManualAnchors()
        {
            manualAnchors.Clear();
            Regenerate();
        }

        /// <summary>
        /// 生成済みの形状を維持したまま、変形だけを初期位置へ戻す
        /// </summary>
        public void ResetSimulation()
        {
            for (int i = 0; i < _nodes.Length; i++)
            {
                WebNode node = _nodes[i];
                node.Position = node.RestPosition;
                node.PreviousPosition = node.RestPosition;
                _nodes[i] = node;
            }

            UpdateMeshGeometry();
        }

        /// <summary>
        /// 巣全体へ燃焼VFXを広げ、糸を焼失させてGameObjectを破棄する。
        /// </summary>
        public void Burn()
        {
            if (_isBurning || !Application.isPlaying)
            {
                return;
            }

            _isBurning = true;
            StartCoroutine(BurnRoutine());
        }

        private IEnumerator BurnRoutine()
        {
            ReleaseAllPlayers();
            DisableInteractionColliders();

            SpiderWebBurnVfx burnVfx = GetComponent<SpiderWebBurnVfx>();
            if (burnVfx != null)
            {
                burnVfx.Play(width, height);
            }

            Color startColor = webColor;
            if (_meshRenderer != null && _meshRenderer.sharedMaterial != null)
            {
                _burnMaterial = new Material(_meshRenderer.sharedMaterial)
                {
                    name = "Spider Web Burning Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
                _meshRenderer.material = _burnMaterial;
                if (_burnMaterial.HasProperty("_BaseColor"))
                {
                    startColor = _burnMaterial.GetColor("_BaseColor");
                }
            }

            float elapsed = 0.0f;
            while (elapsed < burnFadeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / burnFadeDuration);
                Color charred = Color.Lerp(
                    startColor,
                    new Color(0.08f, 0.015f, 0.0f, 0.0f),
                    progress);
                if (_burnMaterial != null && _burnMaterial.HasProperty("_BaseColor"))
                {
                    _burnMaterial.SetColor("_BaseColor", charred);
                }
                yield return null;
            }

            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = false;
            }

            float residualDuration = burnVfx != null
                ? Mathf.Max(0.0f, burnVfx.Duration - burnFadeDuration)
                : 0.0f;
            if (residualDuration > 0.0f)
            {
                yield return new WaitForSeconds(residualDuration);
            }

            Destroy(gameObject);
        }

        private void DisableInteractionColliders()
        {
            if (_trigger != null)
            {
                _trigger.enabled = false;
            }

            foreach (MeshCollider trigger in _manualTriggers)
            {
                if (trigger != null)
                {
                    trigger.enabled = false;
                }
            }
        }

        /// <summary>
        /// 壁・床として使用するColliderを設定する
        /// </summary>
        public void SetAttachmentSurface(Collider surface)
        {
            if (IsInteractionTrigger(surface))
            {
                Debug.LogWarning(
                    "SpiderWeb cannot attach to its own trigger collider.",
                    this);
                return;
            }

            attachmentSurface = surface;
        }

        /// <summary>
        /// 指定面の最寄り位置へTransformを移動し、ローカルZを表面法線へ合わせる
        /// 対象が未指定の場合は、周囲を探索して最も近いColliderをつかう
        /// </summary>
        public bool SnapAndAttachToSurface()
        {
            if (IsInteractionTrigger(attachmentSurface))
            {
                Debug.LogWarning(
                    "SpiderWeb cannot attach to its own trigger collider.",
                    this);
                return false;
            }

            if (!TryFindClosestSurfaceHit(out RaycastHit hit))
            {
                Debug.LogWarning(
                    "SpiderWeb could not find a surface. Assign an Attachment Surface " +
                    "Collider or increase Surface Probe Distance.",
                    this);
                return false;
            }

            if (attachmentSurface == null)
            {
                attachmentSurface = hit.collider;
            }

            Vector3 normal = hit.normal.normalized;
            Vector3 up = Vector3.ProjectOnPlane(transform.up, normal);
            if (up.sqrMagnitude < 0.0001f)
            {
                up = Vector3.ProjectOnPlane(transform.right, normal);
            }
            if (up.sqrMagnitude < 0.0001f)
            {
                up = Vector3.Cross(normal, Vector3.right);
            }

            transform.SetPositionAndRotation(
                hit.point + normal * surfaceOffset,
                Quaternion.LookRotation(normal, up.normalized));

            if (followSurfaceTransform && attachmentSurface != null &&
                attachmentSurface.transform != transform &&
                !attachmentSurface.transform.IsChildOf(transform))
            {
                transform.SetParent(attachmentSurface.transform, true);
            }

            Regenerate();
            return true;
        }

        /// <summary>
        /// 現在の巣の全ノードをAttachment Surfaceへ投影する
        /// 凹凸のあるMeshColliderにも沿わせることができる
        /// </summary>
        public bool ConformToSurface()
        {
            if (attachmentSurface == null ||
                IsInteractionTrigger(attachmentSurface))
            {
                Debug.LogWarning(
                    "Assign a wall or floor Collider as Attachment Surface " +
                    "before conforming the web.",
                    this);
                return false;
            }

            if (_nodes.Length == 0)
            {
                Regenerate();
                return _nodes.Length > 0;
            }

            int projectedCount = ConformNodesToSurface();
            RecalculateConstraintLengths();
            ResetSimulation();
            return projectedCount > 0;
        }

        private void CacheComponents()
        {
            if (_meshFilter == null)
            {
                _meshFilter = GetComponent<MeshFilter>();
            }
            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
            }
            if (_trigger == null)
            {
                _trigger = GetComponent<BoxCollider>();
            }
        }

        private void ConfigureTrigger()
        {
            if (_trigger == null || _isConfiguringTrigger)
            {
                return;
            }

            _isConfiguringTrigger = true;
            try
            {
                _trigger.isTrigger = true;
                List<Vector3> anchorPositions = GetSortedManualAnchorPositions();
                if (anchorPositions.Count >= 3)
                {
                    _trigger.enabled = false;
                    ConfigureManualTriggerColliders(anchorPositions);
                }
                else
                {
                    DisableManualTriggerColliders();
                    _trigger.enabled = true;
                    _trigger.center = Vector3.zero;
                    _trigger.size = new Vector3(width, height, triggerDepth);
                }
            }
            finally
            {
                _isConfiguringTrigger = false;
            }
        }

        private void ConfigureManualTriggerColliders(
            IReadOnlyList<Vector3> anchorPositions)
        {
            DiscoverManualTriggerColliders();
            Vector3 center = Vector3.zero;
            foreach (Vector3 anchorPosition in anchorPositions)
            {
                center += anchorPosition;
            }
            center /= anchorPositions.Count;

            for (int i = 0; i < anchorPositions.Count; i++)
            {
                Vector3 a = anchorPositions[i];
                Vector3 b = anchorPositions[(i + 1) % anchorPositions.Count];
                Vector2 centerToA = new(a.x - center.x, a.y - center.y);
                Vector2 centerToB = new(b.x - center.x, b.y - center.y);
                if (Mathf.Abs(
                        centerToA.x * centerToB.y -
                        centerToA.y * centerToB.x) < 0.00001f)
                {
                    if (i < _manualTriggers.Count &&
                        _manualTriggers[i] != null)
                    {
                        _manualTriggers[i].enabled = false;
                    }
                    continue;
                }

                if (!TryEnsureManualTrigger(i))
                {
                    continue;
                }

                MeshCollider trigger = _manualTriggers[i];
                Mesh triggerMesh = _manualTriggerMeshes[i];
                UpdateTriangularPrismMesh(triggerMesh, center, a, b, i);
                trigger.sharedMesh = null;
                trigger.sharedMesh = triggerMesh;
                trigger.convex = true;
                trigger.isTrigger = true;
                trigger.enabled = true;
            }

            for (int i = anchorPositions.Count; i < _manualTriggers.Count; i++)
            {
                if (_manualTriggers[i] != null)
                {
                    _manualTriggers[i].enabled = false;
                }
            }
        }

        private bool TryEnsureManualTrigger(int index)
        {
            while (_manualTriggers.Count <= index)
            {
                if (!_allowTriggerComponentChanges)
                {
                    return false;
                }

                var trigger = gameObject.AddComponent<MeshCollider>();
                trigger.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
                trigger.enabled = false;
                _manualTriggers.Add(trigger);
                _manualTriggerMeshes.Add(null);
            }

            while (_manualTriggerMeshes.Count <= index)
            {
                _manualTriggerMeshes.Add(null);
            }

            if (_manualTriggerMeshes[index] == null)
            {
                _manualTriggerMeshes[index] = new Mesh
                {
                    name = $"Spider Web Trigger {index + 1:00}",
                    hideFlags = HideFlags.DontSave
                };
            }
            return true;
        }

        private void UpdateTriangularPrismMesh(
            Mesh mesh,
            Vector3 center,
            Vector3 a,
            Vector3 b,
            int index)
        {
            float halfDepth = triggerDepth * 0.5f;
            Vector3 depth = Vector3.forward * halfDepth;
            mesh.Clear();
            mesh.name = $"Spider Web Trigger {index + 1:00}";
            mesh.vertices = new[]
            {
                center - depth,
                a - depth,
                b - depth,
                center + depth,
                a + depth,
                b + depth
            };
            mesh.triangles = new[]
            {
                0, 2, 1,
                3, 4, 5,
                0, 1, 4,
                0, 4, 3,
                1, 2, 5,
                1, 5, 4,
                2, 0, 3,
                2, 3, 5
            };
            mesh.RecalculateBounds();
        }

        private void DiscoverManualTriggerColliders()
        {
            if (_manualTriggers.Count > 0)
            {
                return;
            }

            foreach (MeshCollider candidate in GetComponents<MeshCollider>())
            {
                if ((candidate.hideFlags & HideFlags.DontSave) == 0 &&
                    (candidate.sharedMesh == null ||
                     !candidate.sharedMesh.name.StartsWith(
                         "Spider Web Trigger",
                         StringComparison.Ordinal)))
                {
                    continue;
                }

                _manualTriggers.Add(candidate);
                _manualTriggerMeshes.Add(candidate.sharedMesh);
            }
        }

        private void DisableManualTriggerColliders()
        {
            DiscoverManualTriggerColliders();
            foreach (MeshCollider trigger in _manualTriggers)
            {
                if (trigger != null)
                {
                    trigger.enabled = false;
                }
            }
        }

        private void ClearManualTriggerColliders()
        {
            foreach (MeshCollider trigger in _manualTriggers)
            {
                if (trigger != null)
                {
                    trigger.sharedMesh = null;
                    trigger.enabled = false;
                }
            }
            _manualTriggers.Clear();

            foreach (Mesh triggerMesh in _manualTriggerMeshes)
            {
                if (triggerMesh != null)
                {
                    DestroyRuntimeObject(triggerMesh);
                }
            }
            _manualTriggerMeshes.Clear();
        }

        private bool IsInteractionTrigger(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }
            if (collider == _trigger)
            {
                return true;
            }
            return collider is MeshCollider meshCollider &&
                   _manualTriggers.Contains(meshCollider);
        }

        private void EnsureMaterial()
        {
            if (_meshRenderer == null)
            {
                return;
            }

            if (webMaterial != null)
            {
                _meshRenderer.sharedMaterial = webMaterial;
                return;
            }

            Shader shader = Shader.Find("Shinzui/SpiderWeb");
            if (shader == null)
            {
                return;
            }

            if (_runtimeMaterial == null || _runtimeMaterial.shader != shader)
            {
                if (_runtimeMaterial != null)
                {
                    DestroyRuntimeObject(_runtimeMaterial);
                }
                _runtimeMaterial = new Material(shader)
                {
                    name = "Spider Web Runtime Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            _runtimeMaterial.SetColor("_BaseColor", webColor);
            _meshRenderer.sharedMaterial = _runtimeMaterial;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = true;
        }

        private void BuildRenderTopology(int physicalRadialCount)
        {
            var random = new System.Random(unchecked(seed ^ 0x5f3759df));
            _renderRadialCount = Mathf.Max(radialCount, physicalRadialCount);
            int pointsPerRadial = ringCount + 1;
            _renderPoints = new RenderPoint[_renderRadialCount * pointsPerRadial];
            _renderPositions = new Vector3[_renderPoints.Length];
            var strands = new List<RenderStrand>(_renderRadialCount * ringCount * 2);
            float phase = NextRange(random, 0.0f, Mathf.PI * 2.0f);
            int visualRadial = 0;

            // Every real anchor keeps a spoke. Intermediate spokes end on the straight
            // perimeter support between neighbouring anchors, including uneven counts.
            for (int physical = 0; physical < physicalRadialCount; physical++)
            {
                int nextPhysical = (physical + 1) % physicalRadialCount;
                int subdivisions = _renderRadialCount / physicalRadialCount +
                    (physical < _renderRadialCount % physicalRadialCount ? 1 : 0);
                for (int subdivision = 0; subdivision < subdivisions; subdivision++)
                {
                    float around = subdivision / (float)subdivisions;
                    float angle = visualRadial * Mathf.PI * 2.0f / _renderRadialCount;
                    float spokeWidth = NextRange(random, 0.91f, 1.09f);
                    float spokeShade = NextRange(random, 0.78f, 1.0f);
                    for (int ring = 0; ring <= ringCount; ring++)
                    {
                        // Phase changes are coherent around the web. Capture endpoints
                        // are shared with the spokes rather than floating beside them.
                        float offset = subdivision > 0 && ring > 0 && ring < ringCount
                            ? ringPhaseVariation *
                              (Mathf.Sin(angle * 2.0f + ring * 0.67f + phase) * 0.65f +
                               Mathf.Sin(angle * 5.0f - ring * 0.31f + phase) * 0.35f)
                            : 0.0f;
                        float ringPosition = Mathf.Clamp(ring + offset, 0.0f, ringCount);
                        int lower = Mathf.Min(Mathf.FloorToInt(ringPosition), ringCount - 1);
                        int upper = lower + 1;
                        int point = visualRadial * pointsPerRadial + ring;
                        _renderPoints[point] = new RenderPoint
                        {
                            A = lower == 0 ? 0 : GetNodeIndex(physical, lower),
                            B = lower == 0 ? 0 : GetNodeIndex(nextPhysical, lower),
                            C = GetNodeIndex(physical, upper),
                            D = GetNodeIndex(nextPhysical, upper),
                            Around = around,
                            Along = ringPosition - lower
                        };
                        if (ring > 0)
                        {
                            strands.Add(new RenderStrand
                            {
                                A = point - 1,
                                B = point,
                                Segments = 1,
                                Thickness = spokeWidth,
                                Shade = spokeShade,
                                EndFraction = 1.0f
                            });
                        }
                    }
                    visualRadial++;
                }
            }

            float tearAngleA = NextRange(random, 0.0f, 1.0f);
            float tearAngleB = Mathf.Repeat(tearAngleA + NextRange(random, 0.28f, 0.62f), 1.0f);
            float tearRingA = NextRange(random, 0.42f, 0.78f);
            float tearRingB = NextRange(random, 0.65f, 0.88f);
            for (int radial = 0; radial < _renderRadialCount; radial++)
            {
                int next = (radial + 1) % _renderRadialCount;
                for (int ring = 1; ring <= ringCount; ring++)
                {
                    bool support = ring == ringCount;
                    float angle01 = (radial + 0.5f) / _renderRadialCount;
                    float ring01 = ring / (float)ringCount;
                    float damage = Mathf.Max(
                        GetTearInfluence(angle01, ring01, tearAngleA, tearRingA, 0.085f, 0.24f),
                        GetTearInfluence(angle01, ring01, tearAngleB, tearRingB, 0.065f, 0.2f));
                    float omission = missingThreadChance * (0.04f + damage * 8.0f);
                    if (ring01 > 0.7f)
                    {
                        omission += outerThreadBreakChance * damage * 0.7f;
                    }
                    // The perimeter supports all interpolated spokes. Damage is applied
                    // only after this support graph is fixed, so it cannot orphan them.
                    bool torn = !support && random.NextDouble() < Mathf.Clamp01(omission);
                    if (torn && random.NextDouble() > 0.28)
                    {
                        continue;
                    }
                    float variation = 1.0f + NextSigned(random) *
                        Mathf.Min(threadWidthVariation, 0.22f);
                    strands.Add(new RenderStrand
                    {
                        A = radial * pointsPerRadial + ring,
                        B = next * pointsPerRadial + ring,
                        Segments = support ? 1 : Mathf.Clamp(captureCurveSegments, 3, 4),
                        Thickness = support ? 1.22f : captureThreadWidthRatio * variation,
                        Shade = support ? 0.93f : NextRange(random, 0.72f, 1.0f),
                        Sag = support ? 0.0f : captureThreadSag * NextRange(random, 0.65f, 1.25f),
                        Depth = NextSigned(random) * 0.2f,
                        EndFraction = torn ? NextRange(random, 0.16f, 0.32f) : 1.0f
                    });
                }
            }
            _renderStrands = strands.ToArray();
        }

        private static float GetTearInfluence(
            float angle, float ring, float tearAngle, float tearRing,
            float angularRadius, float radialRadius)
        {
            float distance = Mathf.Abs(angle - tearAngle);
            distance = Mathf.Min(distance, 1.0f - distance) / angularRadius;
            float radialDistance = (ring - tearRing) / radialRadius;
            return Mathf.Clamp01(1.0f - distance * distance - radialDistance * radialDistance);
        }

        private void BuildMesh()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh
                {
                    name = "Procedural Spider Web",
                    hideFlags = HideFlags.DontSave
                };
                _mesh.MarkDynamic();
            }
            else
            {
                _mesh.Clear();
            }

            int vertexCount = 0;
            int indexCount = 0;
            for (int i = 0; i < _renderStrands.Length; i++)
            {
                RenderStrand strand = _renderStrands[i];
                strand.FirstVertex = vertexCount;
                _renderStrands[i] = strand;
                vertexCount += (strand.Segments + 1) * 2;
                indexCount += strand.Segments * 6;
            }
            _vertices = new Vector3[vertexCount];
            _normals = new Vector3[vertexCount];
            _tangents = new Vector4[vertexCount];
            _uvs = new Vector2[vertexCount];
            _colors = new Color[vertexCount];
            _triangles = new int[indexCount];
            int triangle = 0;
            foreach (RenderStrand strand in _renderStrands)
            {
                for (int segment = 0; segment < strand.Segments; segment++)
                {
                    int vertex = strand.FirstVertex + segment * 2;
                    _triangles[triangle++] = vertex;
                    _triangles[triangle++] = vertex + 2;
                    _triangles[triangle++] = vertex + 1;
                    _triangles[triangle++] = vertex + 1;
                    _triangles[triangle++] = vertex + 2;
                    _triangles[triangle++] = vertex + 3;
                }
            }

            _mesh.indexFormat = vertexCount > ushort.MaxValue
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;
            _mesh.vertices = _vertices;
            _mesh.triangles = _triangles;
            _meshFilter.sharedMesh = _mesh;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (isActiveAndEnabled && _generated && _mesh != null &&
                _meshRenderer != null && _meshRenderer.enabled &&
                (camera.cullingMask & (1 << gameObject.layer)) != 0 &&
                MayBeVisibleToCamera(camera))
            {
                UpdateMeshForCamera(camera);
            }
        }

        private bool MayBeVisibleToCamera(Camera camera)
        {
            // A mono frustum is not the union of the two stereo views.
            if (camera.stereoEnabled || _nodes.Length == 0)
            {
                return true;
            }

            Bounds bounds = _meshRenderer.bounds;
            var nodeBounds = new Bounds(_nodes[0].Position, Vector3.zero);
            for (int i = 1; i < _nodes.Length; i++)
            {
                nodeBounds.Encapsulate(_nodes[i].Position);
            }

            // The last rendered bounds may be stale while contact or offscreen
            // simulation moves the nodes. Include their current convex envelope,
            // then allow for deflection, curved/broken strands and ribbon width.
            // 3 covers the largest sag multiplier: 1.25 * (1.8 + 0.2).
            float margin = maxDeflection + nodeBounds.size.magnitude *
                captureThreadSag * 3.0f + threadWidth * 1.5f;
            nodeBounds.Expand(margin * 2.0f);
            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            Vector3 extents = nodeBounds.extents;
            Vector3 worldExtents = new(
                Mathf.Abs(localToWorld.m00) * extents.x + Mathf.Abs(localToWorld.m01) * extents.y + Mathf.Abs(localToWorld.m02) * extents.z,
                Mathf.Abs(localToWorld.m10) * extents.x + Mathf.Abs(localToWorld.m11) * extents.y + Mathf.Abs(localToWorld.m12) * extents.z,
                Mathf.Abs(localToWorld.m20) * extents.x + Mathf.Abs(localToWorld.m21) * extents.y + Mathf.Abs(localToWorld.m22) * extents.z);
            bounds.Encapsulate(new Bounds(localToWorld.MultiplyPoint3x4(nodeBounds.center), worldExtents * 2.0f));

            // A different camera can widen subpixel ribbons beyond the previous
            // camera's mesh. Reserve that footprint before deciding to skip it.
            float distance = Vector3.Distance(camera.transform.position, bounds.center) + bounds.extents.magnitude;
            float projectedHalfHeight = camera.orthographic
                ? camera.orthographicSize
                : Mathf.Max(camera.nearClipPlane, distance) * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float pixelMargin = projectedHalfHeight * minimumThreadPixels / Mathf.Max(1, camera.pixelHeight);
            bounds.Expand(pixelMargin * 2.0f);
            GeometryUtility.CalculateFrustumPlanes(camera, _cameraFrustumPlanes);
            return GeometryUtility.TestPlanesAABB(_cameraFrustumPlanes, bounds);
        }

        private void OnWillRenderObject()
        {
            // The SRP callback runs before culling; this keeps the built-in renderer usable.
            if (GraphicsSettings.currentRenderPipeline == null && Camera.current != null)
            {
                UpdateMeshForCamera(Camera.current);
            }
        }

        private void UpdateMeshGeometry()
        {
            UpdateMeshForCamera(Camera.main);
        }

        private void UpdateMeshForCamera(Camera camera)
        {
            if (_renderStrands.Length == 0 || _mesh == null)
            {
                return;
            }

            for (int i = 0; i < _renderPoints.Length; i++)
            {
                RenderPoint point = _renderPoints[i];
                _renderPositions[i] = Vector3.LerpUnclamped(
                    Vector3.LerpUnclamped(_nodes[point.A].Position, _nodes[point.B].Position, point.Around),
                    Vector3.LerpUnclamped(_nodes[point.C].Position, _nodes[point.D].Position, point.Around),
                    point.Along);
            }

            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            Matrix4x4 normalToLocal = localToWorld.transpose;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            Vector3 cameraForward = camera != null ? camera.transform.forward : transform.forward;
            bool perspective = camera != null && !camera.orthographic;
            float pixelScale = camera == null ? 0.0f : 2.0f / Mathf.Max(1, camera.pixelHeight) *
                (perspective ? Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) : camera.orthographicSize);
            Vector3 localGravity = worldToLocal.MultiplyVector(Physics.gravity);
            // Wall-bound webs stay on their attachment plane. No added normal motion
            // pushes the decorative curve through the wall.
            if (conformThreadsToSurface && attachmentSurface != null)
            {
                localGravity.z = 0.0f;
            }
            if (localGravity.sqrMagnitude < 0.000001f)
            {
                localGravity = Vector3.down;
            }
            localGravity.Normalize();
            Vector3 minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            foreach (RenderStrand strand in _renderStrands)
            {
                Vector3 a = _renderPositions[strand.A];
                Vector3 b = _renderPositions[strand.B];
                Vector3 delta = b - a;
                float length = delta.magnitude;
                bool broken = strand.EndFraction < 1.0f;
                Vector3 sag = localGravity * (length * strand.Sag * (broken ? 1.8f : 1.0f));
                if (!(conformThreadsToSurface && attachmentSurface != null))
                {
                    sag.z += length * strand.Sag * strand.Depth;
                }
                float accumulatedLength = 0.0f;
                Vector3 previous = a;
                for (int sample = 0; sample <= strand.Segments; sample++)
                {
                    float along = sample / (float)strand.Segments;
                    float t = along * strand.EndFraction;
                    Vector3 position = a + delta * t + sag * (4.0f * t * (1.0f - t));
                    Vector3 tangent = delta + sag * (4.0f - 8.0f * t);
                    if (tangent.sqrMagnitude < 0.00000001f)
                    {
                        tangent = Vector3.up;
                    }
                    tangent.Normalize();
                    accumulatedLength += Vector3.Distance(previous, position);
                    previous = position;
                    Vector3 worldPosition = localToWorld.MultiplyPoint3x4(position);
                    Vector3 worldTangent = localToWorld.MultiplyVector(tangent).normalized;
                    Vector3 viewDirection = perspective
                        ? (worldPosition - cameraPosition).normalized
                        : cameraForward;
                    if (viewDirection.sqrMagnitude < 0.0001f)
                    {
                        viewDirection = cameraForward;
                    }
                    Vector3 side = Vector3.Cross(worldTangent, viewDirection);
                    if (side.sqrMagnitude < 0.000001f)
                    {
                        side = Vector3.Cross(worldTangent, Vector3.up);
                    }
                    if (side.sqrMagnitude < 0.000001f)
                    {
                        side = Vector3.Cross(worldTangent, Vector3.right);
                    }
                    side.Normalize();
                    Vector3 localSide = worldToLocal.MultiplyVector(side);
                    float thickness = threadWidth * strand.Thickness;
                    if (strand.Segments > 1)
                    {
                        thickness *= 1.0f + 0.07f * Mathf.Sin(t * Mathf.PI);
                    }
                    if (broken)
                    {
                        thickness *= Mathf.Lerp(1.0f, 0.12f, along * along);
                    }
                    float physicalWorldWidth = thickness / Mathf.Max(0.00001f, localSide.magnitude);
                    float depth = perspective
                        ? Mathf.Max(camera.nearClipPlane, Vector3.Dot(worldPosition - cameraPosition, cameraForward))
                        : 1.0f;
                    float drawnWorldWidth = Mathf.Max(physicalWorldWidth,
                        pixelScale * depth * minimumThreadPixels);
                    float coverage = physicalWorldWidth / Mathf.Max(0.000001f, drawnWorldWidth);
                    Vector3 offset = localSide * (drawnWorldWidth * 0.5f);
                    int vertex = strand.FirstVertex + sample * 2;
                    Vector3 left = position - offset;
                    Vector3 right = position + offset;
                    _vertices[vertex] = left;
                    _vertices[vertex + 1] = right;
                    minimum = Vector3.Min(minimum, Vector3.Min(left, right));
                    maximum = Vector3.Max(maximum, Vector3.Max(left, right));
                    Vector3 normal = normalToLocal.MultiplyVector(-viewDirection).normalized;
                    _normals[vertex] = _normals[vertex + 1] = normal;
                    Vector4 meshTangent = new(tangent.x, tangent.y, tangent.z, 1.0f);
                    _tangents[vertex] = _tangents[vertex + 1] = meshTangent;
                    _uvs[vertex] = new Vector2(0.0f, accumulatedLength);
                    _uvs[vertex + 1] = new Vector2(1.0f, accumulatedLength);
                    Color color = new(strand.Shade, strand.Shade, strand.Shade, coverage);
                    _colors[vertex] = _colors[vertex + 1] = color;
                }
            }

            // Upload reused arrays: no Mesh getters or managed temporary arrays per frame.
            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.tangents = _tangents;
            _mesh.uv = _uvs;
            _mesh.colors = _colors;
            var bounds = new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
            bounds.Expand(0.002f);
            _mesh.bounds = bounds;
        }
        private void Integrate(float deltaTime)
        {
            Vector3 localGravity = transform.InverseTransformDirection(Physics.gravity);
            float time = Time.time;
            float deltaTimeSquared = deltaTime * deltaTime;
            for (int i = 0; i < _nodes.Length; i++)
            {
                WebNode node = _nodes[i];
                if (node.InverseMass <= 0.0f)
                {
                    node.Position = node.RestPosition;
                    node.PreviousPosition = node.RestPosition;
                    _nodes[i] = node;
                    continue;
                }

                Vector3 velocity = (node.Position - node.PreviousPosition) * damping;
                node.PreviousPosition = node.Position;
                float noise = Mathf.Sin(time * ambientFrequency + node.NoisePhase);
                Vector3 acceleration = localGravity * gravityScale;
                acceleration.z += noise * ambientMotion;
                node.Position += velocity + acceleration * deltaTimeSquared;
                _nodes[i] = node;
            }
        }

        private void ApplyPlayerInteraction(float deltaTime)
        {
            if (_playerContactCounts.Count == 0)
            {
                return;
            }

            foreach (PlayerView player in _playerContactCounts.Keys)
            {
                if (player == null || !player.isActiveAndEnabled)
                {
                    continue;
                }

                Bounds playerBounds = GetPlayerBounds(player);
                Vector3 localContact = transform.InverseTransformPoint(playerBounds.center);
                float planeDistance = Mathf.Abs(localContact.z);
                float grip = 1.0f - Mathf.Clamp01(planeDistance / Mathf.Max(0.01f, triggerDepth));
                grip = grip * grip * (3.0f - 2.0f * grip);

                float radiusSquared = contactRadius * contactRadius;
                float signedTarget = Mathf.Clamp(localContact.z, -maxDeflection, maxDeflection);
                Vector2 contactPoint = new(localContact.x, localContact.y);
                for (int i = 0; i < _nodes.Length; i++)
                {
                    WebNode node = _nodes[i];
                    if (node.InverseMass <= 0.0f)
                    {
                        continue;
                    }

                    Vector2 nodePoint = new(node.Position.x, node.Position.y);
                    float distanceSquared = (nodePoint - contactPoint).sqrMagnitude;
                    if (distanceSquared >= radiusSquared)
                    {
                        continue;
                    }

                    float falloff = 1.0f - Mathf.Sqrt(distanceSquared) / contactRadius;
                    float blend = 1.0f - Mathf.Exp(
                        -deformationResponse * falloff * grip * deltaTime);
                    node.Position.z = Mathf.Lerp(node.Position.z, signedTarget, blend);
                    _nodes[i] = node;
                }

                Vector3 planePoint = transform.TransformPoint(
                    new Vector3(localContact.x, localContact.y, 0.0f));
                Vector3 pullVelocity = Vector3.ClampMagnitude(
                    (planePoint - playerBounds.center) * pullStrength * grip,
                    maxPullSpeed);
                float speedMultiplier = Mathf.Lerp(
                    1.0f,
                    trappedSpeedMultiplier,
                    grip);
                player.SetMotionModifier(_sourceId, speedMultiplier, pullVelocity);
            }
        }

        private void SolveConstraints()
        {
            for (int iteration = 0; iteration < constraintIterations; iteration++)
            {
                for (int i = 0; i < _constraints.Length; i++)
                {
                    WebConstraint constraint = _constraints[i];
                    WebNode a = _nodes[constraint.A];
                    WebNode b = _nodes[constraint.B];
                    Vector3 delta = b.Position - a.Position;
                    float length = delta.magnitude;
                    if (length < 0.00001f)
                    {
                        continue;
                    }

                    float weight = a.InverseMass + b.InverseMass;
                    if (weight <= 0.0f)
                    {
                        continue;
                    }

                    Vector3 correction = delta *
                        ((length - constraint.RestLength) / length) *
                        stiffness;
                    if (a.InverseMass > 0.0f)
                    {
                        a.Position += correction * (a.InverseMass / weight);
                    }
                    if (b.InverseMass > 0.0f)
                    {
                        b.Position -= correction * (b.InverseMass / weight);
                    }
                    _nodes[constraint.A] = a;
                    _nodes[constraint.B] = b;
                }
            }
        }

        private void ClampDeflection()
        {
            for (int i = 0; i < _nodes.Length; i++)
            {
                WebNode node = _nodes[i];
                if (node.InverseMass <= 0.0f)
                {
                    continue;
                }

                Vector3 offset = node.Position - node.RestPosition;
                if (offset.magnitude > maxDeflection)
                {
                    node.Position = node.RestPosition +
                                    offset.normalized * maxDeflection;
                    _nodes[i] = node;
                }
            }
        }

        private void AddReleaseImpulse(Vector3 worldContact)
        {
            Vector3 localContact = transform.InverseTransformPoint(worldContact);
            Vector2 point = new(localContact.x, localContact.y);
            float radiusSquared = contactRadius * contactRadius * 1.8f;
            for (int i = 0; i < _nodes.Length; i++)
            {
                WebNode node = _nodes[i];
                Vector2 nodePoint = new(node.Position.x, node.Position.y);
                if (node.InverseMass <= 0.0f ||
                    (nodePoint - point).sqrMagnitude > radiusSquared)
                {
                    continue;
                }

                // Verletでは前フレーム位置をずらすことで速度を与える
                float direction = Mathf.Sign(node.Position.z);
                if (Mathf.Approximately(direction, 0.0f))
                {
                    direction = 1.0f;
                }
                node.PreviousPosition.z += direction * releaseImpulse;
                _nodes[i] = node;
            }
        }

        private Bounds GetPlayerBounds(PlayerView player)
        {
            Collider playerCollider = player.PlayerCollider;
            return playerCollider != null
                ? playerCollider.bounds
                : new Bounds(player.transform.position + Vector3.up, Vector3.one);
        }

        private void ReleaseAllPlayers()
        {
            foreach (PlayerView player in _playerContactCounts.Keys)
            {
                if (player != null)
                {
                    player.RemoveMotionModifier(_sourceId);
                }
            }
            _playerContactCounts.Clear();
        }

        private bool TryFindClosestSurfaceHit(out RaycastHit closestHit)
        {
            closestHit = default;
            float closestDistanceSquared = float.MaxValue;
            Vector3 origin = transform.position;
            Vector3[] axes =
            {
                transform.forward,
                transform.up,
                transform.right
            };

            foreach (Vector3 axis in axes)
            {
                Vector3 rayOrigin = origin + axis.normalized * surfaceProbeDistance;
                var ray = new Ray(rayOrigin, -axis.normalized);
                if (!TryRaycastAttachment(
                        ray,
                        surfaceProbeDistance * 2.0f,
                        out RaycastHit hit))
                {
                    continue;
                }

                float distanceSquared = (hit.point - origin).sqrMagnitude;
                if (distanceSquared >= closestDistanceSquared)
                {
                    continue;
                }

                closestDistanceSquared = distanceSquared;
                closestHit = hit;
            }

            return closestDistanceSquared < float.MaxValue;
        }

        private int ConformNodesToSurface()
        {
            if (attachmentSurface == null || _nodes.Length == 0)
            {
                return 0;
            }

            int projectedCount = 0;
            Vector3 normal = transform.forward.normalized;
            float rayDistance = surfaceProbeDistance * 2.0f;
            for (int i = 0; i < _nodes.Length; i++)
            {
                WebNode node = _nodes[i];
                node.InverseMass =
                    node.AttachmentCandidate && node.KeepFixedWhenProjectionMisses
                        ? 0.0f
                        : 1.0f;
                Vector3 flatLocalPosition = new(
                    node.RestPosition.x,
                    node.RestPosition.y,
                    0.0f);
                Vector3 worldPosition = transform.TransformPoint(flatLocalPosition);
                var ray = new Ray(
                    worldPosition + normal * surfaceProbeDistance,
                    -normal);
                if (!TryRaycastAttachment(ray, rayDistance, out RaycastHit hit))
                {
                    // 表面に届かなかった外周点は固定せず、重力でたゆませる
                    _nodes[i] = node;
                    continue;
                }

                Vector3 attachedWorldPosition =
                    hit.point + hit.normal.normalized * surfaceOffset;
                Vector3 attachedLocalPosition =
                    transform.InverseTransformPoint(attachedWorldPosition);
                node.RestPosition = attachedLocalPosition;
                node.Position = attachedLocalPosition;
                node.PreviousPosition = attachedLocalPosition;
                if (node.AttachmentCandidate)
                {
                    node.InverseMass = 0.0f;
                }
                _nodes[i] = node;
                projectedCount++;
            }

            return projectedCount;
        }

        private bool TryRaycastAttachment(
            Ray ray,
            float distance,
            out RaycastHit hit)
        {
            if (attachmentSurface != null)
            {
                return attachmentSurface.Raycast(ray, out hit, distance);
            }

            return Physics.Raycast(
                ray,
                out hit,
                distance,
                attachmentLayers,
                QueryTriggerInteraction.Ignore);
        }

        private void RecalculateConstraintLengths()
        {
            for (int i = 0; i < _constraints.Length; i++)
            {
                WebConstraint constraint = _constraints[i];
                constraint.RestLength = Vector3.Distance(
                    _nodes[constraint.A].RestPosition,
                    _nodes[constraint.B].RestPosition);
                _constraints[i] = constraint;
            }
        }

        private float GetRectangleEdgeDistance(Vector2 center, Vector2 direction)
        {
            float xBoundary = direction.x >= 0.0f
                ? width * 0.5f
                : -width * 0.5f;
            float yBoundary = direction.y >= 0.0f
                ? height * 0.5f
                : -height * 0.5f;
            float xDistance = Mathf.Abs(direction.x) > 0.0001f
                ? (xBoundary - center.x) / direction.x
                : float.MaxValue;
            float yDistance = Mathf.Abs(direction.y) > 0.0001f
                ? (yBoundary - center.y) / direction.y
                : float.MaxValue;
            return Mathf.Min(xDistance, yDistance);
        }

        private Vector2 GetRectangleEdgePoint(Vector2 center, Vector2 direction)
        {
            float distance = GetRectangleEdgeDistance(center, direction);
            Vector2 result = center + direction * distance;
            result.x = Mathf.Clamp(result.x, -width * 0.5f, width * 0.5f);
            result.y = Mathf.Clamp(result.y, -height * 0.5f, height * 0.5f);
            return result;
        }

        private Vector2 ApplyUnattachedEdgeSag(
            Vector2 edgePoint,
            Vector2 center,
            System.Random random)
        {
            if (unattachedEdgeSag <= 0.0f)
            {
                return edgePoint;
            }

            Vector3 localGravity3D =
                transform.InverseTransformDirection(Physics.gravity);
            Vector2 localGravity = new(localGravity3D.x, localGravity3D.y);
            if (localGravity.sqrMagnitude < 0.0001f)
            {
                localGravity = Vector2.down;
            }
            localGravity.Normalize();

            float webScale = Mathf.Min(width, height);
            float gravitySag = webScale * unattachedEdgeSag *
                               NextRange(random, 0.35f, 1.0f);
            float inwardSlack = unattachedEdgeSag *
                                NextRange(random, 0.18f, 0.5f);
            Vector2 saggedPoint = Vector2.Lerp(edgePoint, center, inwardSlack);
            saggedPoint += localGravity * gravitySag;
            return saggedPoint;
        }

        private int GetNodeIndex(int radial, int ring)
        {
            return 1 + radial * ringCount + ring - 1;
        }

        private WebNode CreateNode(
            Vector3 position,
            bool attachmentCandidate,
            System.Random random,
            bool keepFixedWhenProjectionMisses = false)
        {
            return new WebNode
            {
                Position = position,
                PreviousPosition = position,
                RestPosition = position,
                InverseMass = attachmentCandidate ? 0.0f : 1.0f,
                NoisePhase = NextRange(random, 0.0f, Mathf.PI * 2.0f),
                AttachmentCandidate = attachmentCandidate,
                KeepFixedWhenProjectionMisses = keepFixedWhenProjectionMisses
            };
        }

        private int GetValidManualAnchorCount()
        {
            int count = 0;
            foreach (Transform anchor in manualAnchors)
            {
                if (anchor != null)
                {
                    count++;
                }
            }
            return count;
        }

        private List<Vector3> GetSortedManualAnchorPositions()
        {
            var positions = new List<Vector3>();
            foreach (Transform anchor in manualAnchors)
            {
                if (anchor == null)
                {
                    continue;
                }

                Vector3 localPosition = transform.InverseTransformPoint(anchor.position);
                bool duplicate = false;
                foreach (Vector3 existing in positions)
                {
                    if ((existing - localPosition).sqrMagnitude < 0.000001f)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    positions.Add(localPosition);
                }
            }

            if (positions.Count < 3)
            {
                return positions;
            }

            Vector3 center = Vector3.zero;
            foreach (Vector3 position in positions)
            {
                center += position;
            }
            center /= positions.Count;
            positions.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.y - center.y, a.x - center.x);
                float angleB = Mathf.Atan2(b.y - center.y, b.x - center.x);
                return angleA.CompareTo(angleB);
            });
            return positions;
        }

        private int ComputeManualAnchorPoseHash()
        {
            unchecked
            {
                int hash = 17;
                foreach (Transform anchor in manualAnchors)
                {
                    hash = hash * 31 + (anchor == null ? 0 : anchor.GetEntityId().GetHashCode());
                    if (anchor != null)
                    {
                        hash = hash * 31 + anchor.position.GetHashCode();
                    }
                }
                return hash;
            }
        }

        private void AddConstraint(
            List<WebConstraint> constraints,
            List<WebNode> nodes,
            int a,
            int b,
            System.Random random,
            float baseThickness)
        {
            float thicknessVariation = 1.0f +
                                       NextSigned(random) * threadWidthVariation;
            constraints.Add(new WebConstraint
            {
                A = a,
                B = b,
                RestLength = Vector3.Distance(nodes[a].RestPosition, nodes[b].RestPosition),
                ThicknessScale = Mathf.Max(0.35f, baseThickness * thicknessVariation),
                Shade = NextRange(random, 0.35f, 1.0f)
            });
        }

        private static float NextSigned(System.Random random)
        {
            return (float)random.NextDouble() * 2.0f - 1.0f;
        }

        private static float NextRange(System.Random random, float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        private void DestroyRuntimeResources()
        {
            ClearManualTriggerColliders();
            if (_meshFilter != null && _meshFilter.sharedMesh == _mesh)
            {
                _meshFilter.sharedMesh = null;
            }
            if (_mesh != null)
            {
                DestroyRuntimeObject(_mesh);
                _mesh = null;
            }
            if (_runtimeMaterial != null)
            {
                DestroyRuntimeObject(_runtimeMaterial);
                _runtimeMaterial = null;
            }
            if (_burnMaterial != null)
            {
                DestroyRuntimeObject(_burnMaterial);
                _burnMaterial = null;
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.45f, 0.85f, 1.0f, 0.7f);
            List<Vector3> anchorPositions = GetSortedManualAnchorPositions();
            if (anchorPositions.Count >= 3)
            {
                for (int i = 0; i < anchorPositions.Count; i++)
                {
                    Gizmos.DrawLine(
                        anchorPositions[i],
                        anchorPositions[(i + 1) % anchorPositions.Count]);
                }
            }
            else
            {
                Gizmos.DrawWireCube(
                    Vector3.zero,
                    new Vector3(width, height, triggerDepth));
            }

            if (_nodes.Length == 0)
            {
                return;
            }

            Gizmos.color = new Color(0.7f, 0.9f, 1.0f, 0.65f);
            foreach (WebConstraint constraint in _constraints)
            {
                Gizmos.DrawLine(
                    _nodes[constraint.A].Position,
                    _nodes[constraint.B].Position);
            }

            Gizmos.color = new Color(0.2f, 1.0f, 0.55f, 0.9f);
            foreach (WebNode node in _nodes)
            {
                if (node.InverseMass <= 0.0f)
                {
                    Gizmos.DrawSphere(node.Position, 0.035f);
                }
            }
        }
    }
}
