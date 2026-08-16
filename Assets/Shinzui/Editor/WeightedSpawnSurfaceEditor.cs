using System;
using System.Collections.Generic;
using Shinzui.View.ItemSpawn;
using UnityEditor;
using UnityEngine;

namespace Shinzui.Editor
{
    [CustomEditor(typeof(WeightedSpawnSurface))]
    public sealed class WeightedSpawnSurfaceEditor : UnityEditor.Editor
    {
        private enum ToolMode { Paint, Erase, Smooth }

        private static readonly int[] ResolutionValues = { 64, 128, 256, 512, 1024 };
        private static readonly string[] ResolutionLabels = { "64 x 64", "128 x 128", "256 x 256 (Default)", "512 x 512", "1024 x 1024" };

        private SerializedProperty _surfaceMeshProp;
        private SerializedProperty _regionWeightProp;
        private SerializedProperty _mapResolutionProp;

        private ToolMode _mode = ToolMode.Paint;
        private bool _paintMode;
        private bool _showWeights = true;
        private bool _showWireframe = false;
        private float _brushSize = 1f;
        private float _brushStrength = 0.35f;
        private float _targetWeight = 1f;
        private int _selectedTriangle = -1;
        private Vector3 _lastHitPoint;
        private bool _hasHit;
        private bool _undoRecorded;

        // Editor専用プレビューキャッシュ
        private Texture2D _heatmapTexture;
        private Mesh _overlayMesh;
        private Material _overlayMaterial;
        private bool _textureDirty = true;
        private bool _meshDirty = true;
        private Mesh _lastSourceMesh;
        private SpawnSurfaceProjection _lastProjection;
        private int _lastResolution;

        private WeightedSpawnSurface Surface => (WeightedSpawnSurface)target;

        private void OnEnable()
        {
            _surfaceMeshProp = serializedObject.FindProperty("surfaceMesh");
            _regionWeightProp = serializedObject.FindProperty("regionWeight");
            _mapResolutionProp = serializedObject.FindProperty("mapResolution");
            _textureDirty = true;
            _meshDirty = true;
        }

        private void OnDisable()
        {
            CleanupResources();
        }

        private void CleanupResources()
        {
            if (_heatmapTexture != null)
            {
                DestroyImmediate(_heatmapTexture);
                _heatmapTexture = null;
            }
            if (_overlayMesh != null)
            {
                DestroyImmediate(_overlayMesh);
                _overlayMesh = null;
            }
            if (_overlayMaterial != null)
            {
                DestroyImmediate(_overlayMaterial);
                _overlayMaterial = null;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            WeightedSpawnSurface surface = Surface;
            Mesh surfaceMesh = surface.SurfaceMesh;

            EditorGUILayout.LabelField("Spawn Dedicated Surface (2D Weight Map)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_surfaceMeshProp, new GUIContent("Surface Mesh"));
            EditorGUILayout.PropertyField(_regionWeightProp, new GUIContent("Region Weight"));

            // 解像度設定（バイリニアリサイズ）
            int currentRes = surface.MapResolution;
            int selectedResIndex = Array.IndexOf(ResolutionValues, currentRes);
            if (selectedResIndex < 0) selectedResIndex = 2; // デフォルト 256

            EditorGUI.BeginChangeCheck();
            int newResIndex = EditorGUILayout.Popup("Map Resolution", selectedResIndex, ResolutionLabels);
            if (EditorGUI.EndChangeCheck())
            {
                int newRes = ResolutionValues[newResIndex];
                if (newRes != currentRes)
                {
                    Undo.RecordObject(surface, "Resize Spawn Weight Map");
                    surface.ResizeMap(newRes);
                    _textureDirty = true;
                    _lastResolution = newRes;
                    EditorUtility.SetDirty(surface);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
                }
            }

            // 投影平面（Projection Plane）設定
            EditorGUI.BeginChangeCheck();
            var newPlane = (SpawnSurfaceProjection)EditorGUILayout.EnumPopup("Projection Plane", surface.ProjectionPlane);
            if (EditorGUI.EndChangeCheck() && newPlane != surface.ProjectionPlane)
            {
                Undo.RecordObject(surface, "Change Projection Plane");
                surface.SetProjectionPlane(newPlane);
                _meshDirty = true;
                EditorUtility.SetDirty(surface);
                PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
            }

            if (surfaceMesh != null && GUILayout.Button("Auto Detect Projection Plane"))
            {
                Undo.RecordObject(surface, "Auto Detect Projection Plane");
                surface.SetProjectionPlane(WeightedSpawnSurface.DetermineProjectionPlane(surfaceMesh.bounds));
                _meshDirty = true;
                EditorUtility.SetDirty(surface);
                PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            if (surfaceMesh == null)
            {
                EditorGUILayout.HelpBox(
                    "Spawn専用Surface Meshが設定されていません。\n" +
                    "Surface Meshフィールドにメッシュを割り当てるか、以下のインポート機能を利用してください。",
                    MessageType.Info);

                DrawMigrationSection(surface);
                return;
            }

            if (!surface.IsDataValid)
            {
                EditorGUILayout.HelpBox(
                    "保存済みWeightデータと現在のSurface Meshのトポロジーが一致しません。\n" +
                    "「Initialize / Rebuild Weight Map」を実行してWeight Mapを初期化してください。",
                    MessageType.Warning);

                if (GUILayout.Button("Initialize / Rebuild Weight Map", GUILayout.Height(26)))
                {
                    RebuildData();
                }

                DrawMigrationSection(surface);
                return;
            }

            MeshCollider meshCollider = surface.GetComponent<MeshCollider>();
            if (meshCollider == null || meshCollider.sharedMesh != surfaceMesh)
            {
                EditorGUILayout.HelpBox(
                    "Scene ViewでのRaycast編集を行うには、同じGameObjectに" +
                    "Surface Meshと同じMeshを参照するMeshColliderが必要です。",
                    MessageType.Warning);

                if (GUILayout.Button("Setup / Sync MeshCollider for Editing"))
                {
                    SetupMeshCollider(surface, surfaceMesh);
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("2D Weight Paint Tool", EditorStyles.boldLabel);
                _paintMode = EditorGUILayout.ToggleLeft("Paint Mode (Scene View)", _paintMode);
                _showWeights = EditorGUILayout.ToggleLeft("Show Smooth Heatmap in Scene", _showWeights);
                _showWireframe = EditorGUILayout.ToggleLeft("Show Surface Wireframe", _showWireframe);

                EditorGUILayout.Space(2);
                _mode = (ToolMode)EditorGUILayout.EnumPopup("Tool Mode", _mode);
                _brushSize = Mathf.Max(0.01f, EditorGUILayout.FloatField("Brush Size (World Dist)", _brushSize));
                _brushStrength = Mathf.Clamp01(EditorGUILayout.Slider("Brush Strength", _brushStrength, 0.01f, 1f));
                _targetWeight = Mathf.Clamp01(EditorGUILayout.Slider("Target Weight", _targetWeight, 0f, 1f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fill All (Target Weight)")) ApplyAll("Fill Spawn Weights", _targetWeight);
                if (GUILayout.Button("Clear All (0.0)")) ApplyAll("Clear Spawn Weights", 0f);
            }

            EditorGUILayout.Space();
            DrawMigrationSection(surface);

            EditorGUILayout.Space();
            string hitInfo = _hasHit
                ? $"Hit Sample Weight: {surface.GetWeightAtWorldPosition(_lastHitPoint):0.###}"
                : "Hit: None";
            string selectedTri = _selectedTriangle >= 0 ? $"Triangle: {_selectedTriangle}" : "Triangle: -";
            EditorGUILayout.HelpBox(
                $"Resolution: {surface.MapResolution}x{surface.MapResolution} | Projection: {surface.ProjectionPlane} | {selectedTri} | {hitInfo}\n" +
                $"Surface Triangles: {surface.TriangleCount} (Vertices: {surfaceMesh.vertexCount})\n" +
                "Heatmap: Smooth Bilinear (0=Transparent, Low=Blue, Mid=Yellow, High=Red). No polygon seams.", MessageType.None);

            SceneView.RepaintAll();
        }

        private void DrawMigrationSection(WeightedSpawnSurface surface)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Surface Migration & Setup Helpers", EditorStyles.boldLabel);

                if (GUILayout.Button("Migrate Legacy Triangle Weights -> 2D Weight Map"))
                {
                    if (EditorUtility.DisplayDialog("Migrate Triangle Weights",
                        "旧Triangle単位のWeightデータを2D Weight Mapへ面ラスタライズ焼き付けしますか？", "移行実行", "キャンセル"))
                    {
                        Undo.RecordObject(surface, "Migrate Triangle Weights to 2D Map");
                        surface.MigrateFromLegacyTriangleWeights();
                        _textureDirty = true;
                        EditorUtility.SetDirty(surface);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
                        SceneView.RepaintAll();
                    }
                }

                var meshFilter = surface.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    if (GUILayout.Button($"Import Mesh from MeshFilter ({meshFilter.sharedMesh.name})"))
                    {
                        ImportFromMeshFilter(surface, meshFilter.sharedMesh);
                    }
                }

                var meshCollider = surface.GetComponent<MeshCollider>();
                if (meshCollider != null && meshCollider.sharedMesh != null && meshCollider.sharedMesh != surface.SurfaceMesh)
                {
                    if (GUILayout.Button($"Import Mesh from MeshCollider ({meshCollider.sharedMesh.name})"))
                    {
                        ImportFromMeshCollider(surface, meshCollider.sharedMesh);
                    }
                }

                if (surface.SurfaceMesh != null)
                {
                    if (GUILayout.Button("Sync MeshCollider with Surface Mesh"))
                    {
                        SetupMeshCollider(surface, surface.SurfaceMesh);
                    }
                }
            }
        }

        private void ImportFromMeshFilter(WeightedSpawnSurface surface, Mesh mesh)
        {
            if (mesh == null) return;
            if (!EditorUtility.DisplayDialog("Import Mesh from MeshFilter",
                $"MeshFilterのメッシュ '{mesh.name}' を専用Surface Meshとして取り込み、Weightデータを初期化しますか？\n既存のWeightデータはリセットされます。",
                "インポート実行", "キャンセル")) return;

            Undo.RecordObject(surface, "Import Surface Mesh from MeshFilter");
            surface.InitializeFromMesh(mesh);
            _meshDirty = true;
            _textureDirty = true;
            EditorUtility.SetDirty(surface);
            PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
            SceneView.RepaintAll();
        }

        private void ImportFromMeshCollider(WeightedSpawnSurface surface, Mesh mesh)
        {
            if (mesh == null) return;
            if (!EditorUtility.DisplayDialog("Import Mesh from MeshCollider",
                $"MeshColliderのメッシュ '{mesh.name}' を専用Surface Meshとして取り込み、Weightデータを初期化しますか？\n既存のWeightデータはリセットされます。",
                "インポート実行", "キャンセル")) return;

            Undo.RecordObject(surface, "Import Surface Mesh from MeshCollider");
            surface.InitializeFromMesh(mesh);
            _meshDirty = true;
            _textureDirty = true;
            EditorUtility.SetDirty(surface);
            PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
            SceneView.RepaintAll();
        }

        private static void SetupMeshCollider(WeightedSpawnSurface surface, Mesh mesh)
        {
            if (mesh == null) return;
            MeshCollider collider = surface.GetComponent<MeshCollider>();
            if (collider == null)
            {
                Undo.AddComponent<MeshCollider>(surface.gameObject);
                collider = surface.GetComponent<MeshCollider>();
            }
            Undo.RecordObject(collider, "Setup Surface MeshCollider");
            collider.sharedMesh = mesh;
            EditorUtility.SetDirty(collider);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }

        private void OnSceneGUI()
        {
            WeightedSpawnSurface surface = Surface;
            if (!surface.IsDataValid || surface.SurfaceMesh == null) return;

            if (_showWireframe) DrawWireframe(surface);
            if (_showWeights) DrawHeatmap(surface);

            if (!_paintMode) return;

            Event e = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (e.alt || e.button != 0) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!TryRaycast(surface, ray, out RaycastHit hit))
            {
                _hasHit = false;
                return;
            }

            _hasHit = true;
            _lastHitPoint = hit.point;
            _selectedTriangle = hit.triangleIndex;
            DrawBrush(hit.point, hit.normal, surface);

            if (e.type == EventType.MouseDown)
            {
                _undoRecorded = false;
                PaintAt(surface, hit.point, e);
            }
            else if (e.type == EventType.MouseDrag && e.delta.sqrMagnitude > 0f)
            {
                PaintAt(surface, hit.point, e);
            }
            else if (e.type == EventType.MouseUp)
            {
                _undoRecorded = false;
                e.Use();
            }
        }

        private void PaintAt(WeightedSpawnSurface surface, Vector3 centerWorld, Event e)
        {
            if (!_undoRecorded)
            {
                Undo.RecordObject(surface, $"{_mode} 2D Spawn Weights");
                _undoRecorded = true;
            }

            int n = surface.MapResolution;
            float[] map = surface.CopyWeightMap();
            if (map == null || map.Length != n * n)
            {
                surface.InitializeWeightMap(n, 1f);
                map = surface.CopyWeightMap();
            }

            Bounds b = surface.LocalBounds;
            Vector3 centerLocal = surface.transform.InverseTransformPoint(centerWorld);

            // ブラシのWorld半径をLocal半径へ（スケール考慮）
            Vector3 lossy = surface.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z), 0.001f);
            float brushSizeLocal = _brushSize / maxScale;

            float minX = centerLocal.x - brushSizeLocal;
            float maxX = centerLocal.x + brushSizeLocal;
            float minY = centerLocal.y - brushSizeLocal;
            float maxY = centerLocal.y + brushSizeLocal;
            float minZ = centerLocal.z - brushSizeLocal;
            float maxZ = centerLocal.z + brushSizeLocal;

            float sizeX = Mathf.Max(b.size.x, 1e-4f);
            float sizeY = Mathf.Max(b.size.y, 1e-4f);
            float sizeZ = Mathf.Max(b.size.z, 1e-4f);

            float uMin, uMax, vMin, vMax;
            switch (surface.ProjectionPlane)
            {
                case SpawnSurfaceProjection.XY:
                    uMin = Mathf.Clamp01((minX - b.min.x) / sizeX);
                    uMax = Mathf.Clamp01((maxX - b.min.x) / sizeX);
                    vMin = Mathf.Clamp01((minY - b.min.y) / sizeY);
                    vMax = Mathf.Clamp01((maxY - b.min.y) / sizeY);
                    break;

                case SpawnSurfaceProjection.YZ:
                    uMin = Mathf.Clamp01((minY - b.min.y) / sizeY);
                    uMax = Mathf.Clamp01((maxY - b.min.y) / sizeY);
                    vMin = Mathf.Clamp01((minZ - b.min.z) / sizeZ);
                    vMax = Mathf.Clamp01((maxZ - b.min.z) / sizeZ);
                    break;

                case SpawnSurfaceProjection.XZ:
                default:
                    uMin = Mathf.Clamp01((minX - b.min.x) / sizeX);
                    uMax = Mathf.Clamp01((maxX - b.min.x) / sizeX);
                    vMin = Mathf.Clamp01((minZ - b.min.z) / sizeZ);
                    vMax = Mathf.Clamp01((maxZ - b.min.z) / sizeZ);
                    break;
            }

            int x0 = Mathf.Clamp(Mathf.FloorToInt(uMin * (n - 1)), 0, n - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(uMax * (n - 1)), 0, n - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(vMin * (n - 1)), 0, n - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(vMax * (n - 1)), 0, n - 1);

            bool modified = false;

            for (int y = y0; y <= y1; y++)
            {
                float v = (float)y / (n - 1);
                for (int x = x0; x <= x1; x++)
                {
                    float u = (float)x / (n - 1);
                    Vector3 texelWorld = surface.NormalizedMapCoordsToWorld(u, v);
                    float dist = Vector3.Distance(centerWorld, texelWorld);

                    if (dist > _brushSize) continue;

                    float falloff = 1f - Mathf.Clamp01(dist / _brushSize);
                    float amount = _brushStrength * falloff;
                    int idx = y * n + x;
                    float current = map[idx];

                    float next;
                    if (_mode == ToolMode.Smooth)
                    {
                        float avg = GetNeighborAverage(map, x, y, n);
                        next = Mathf.Lerp(current, avg, amount);
                    }
                    else if (_mode == ToolMode.Erase)
                    {
                        next = Mathf.Lerp(current, 0f, amount);
                    }
                    else
                    {
                        next = Mathf.Lerp(current, _targetWeight, amount);
                    }

                    map[idx] = Mathf.Clamp01(next);
                    modified = true;
                }
            }

            if (modified)
            {
                surface.SetWeightMap(map, n);
                _textureDirty = true;
                EditorUtility.SetDirty(surface);
                PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
            }

            e.Use();
            SceneView.RepaintAll();
        }

        private static float GetNeighborAverage(float[] map, int cx, int cy, int n)
        {
            float sum = 0f;
            int count = 0;

            for (int dy = -1; dy <= 1; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= n) continue;

                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= n) continue;

                    sum += map[y * n + x];
                    count++;
                }
            }

            return count > 0 ? sum / count : map[cy * n + cx];
        }

        private static bool TryRaycast(WeightedSpawnSurface surface, Ray ray, out RaycastHit hit)
        {
            MeshCollider collider = surface.GetComponent<MeshCollider>();
            if (collider != null && collider.sharedMesh == surface.SurfaceMesh && collider.Raycast(ray, out hit, 10000f)) return true;
            hit = default;
            return false;
        }

        private void EnsureResources(WeightedSpawnSurface surface)
        {
            Mesh sourceMesh = surface.SurfaceMesh;
            if (sourceMesh == null) return;

            if (_lastSourceMesh != sourceMesh || _lastProjection != surface.ProjectionPlane)
            {
                _meshDirty = true;
                _lastSourceMesh = sourceMesh;
                _lastProjection = surface.ProjectionPlane;
            }

            int n = surface.MapResolution;
            if (_lastResolution != n)
            {
                _textureDirty = true;
                _lastResolution = n;
            }

            // Material生成
            if (_overlayMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
                _overlayMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            // Mesh生成（法線微小オフセット + 投影UV割り当て）
            if (_overlayMesh == null || _meshDirty)
            {
                if (_overlayMesh == null)
                {
                    _overlayMesh = new Mesh
                    {
                        name = "SpawnWeightHeatmapOverlay",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }

                Vector3[] origVerts = sourceMesh.vertices;
                Vector3[] normals = sourceMesh.normals;
                Vector3[] offsetVerts = new Vector3[origVerts.Length];
                Vector2[] uvs = new Vector2[origVerts.Length];

                for (int i = 0; i < origVerts.Length; i++)
                {
                    Vector3 norm = (normals != null && normals.Length > i) ? normals[i] : Vector3.up;
                    // メッシュ法線方向へ微小オフセットしてZ-fightingを完全に回避
                    offsetVerts[i] = origVerts[i] + norm * 0.003f;

                    if (surface.LocalToNormalizedMapCoords(origVerts[i], out float u, out float v))
                    {
                        uvs[i] = new Vector2(u, v);
                    }
                    else
                    {
                        uvs[i] = new Vector2(0.5f, 0.5f);
                    }
                }

                _overlayMesh.Clear();
                _overlayMesh.vertices = offsetVerts;
                _overlayMesh.uv = uvs;
                _overlayMesh.triangles = sourceMesh.triangles;
                _overlayMesh.RecalculateBounds();
                _meshDirty = false;
            }

            // Texture生成・更新
            if (_heatmapTexture == null || _heatmapTexture.width != n || _heatmapTexture.height != n)
            {
                if (_heatmapTexture != null) DestroyImmediate(_heatmapTexture);
                _heatmapTexture = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    name = "SpawnWeightHeatmapTex",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _textureDirty = true;
            }

            if (_textureDirty)
            {
                float[] map = surface.CopyWeightMap();
                Color[] pixels = new Color[n * n];

                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        int idx = y * n + x;
                        float w = (map != null && idx < map.Length) ? Mathf.Clamp01(map[idx]) : 0f;
                        pixels[idx] = EvaluateHeatmapColor(w);
                    }
                }

                _heatmapTexture.SetPixels(pixels);
                _heatmapTexture.Apply(false);
                _overlayMaterial.mainTexture = _heatmapTexture;
                _textureDirty = false;
            }
        }

        private static Color EvaluateHeatmapColor(float w)
        {
            if (w <= 0.0001f)
            {
                // 0領域: 完全透明（0領域と有効領域が明確に判別できる）
                return new Color(0f, 0f, 0f, 0f);
            }

            // 滑らかなヒートマップカラーグラデーション:
            // 0..0.25: 深青 -> シアン (低)
            // 0.25..0.5: シアン -> 緑
            // 0.5..0.75: 緑 -> 黄 (中間)
            // 0.75..1.0: 黄 -> 赤 (高)
            Color rgb;
            if (w < 0.25f)
            {
                float t = w / 0.25f;
                rgb = Color.Lerp(new Color(0f, 0.2f, 0.9f), new Color(0f, 0.9f, 0.9f), t);
            }
            else if (w < 0.5f)
            {
                float t = (w - 0.25f) / 0.25f;
                rgb = Color.Lerp(new Color(0f, 0.9f, 0.9f), new Color(0.1f, 0.9f, 0.2f), t);
            }
            else if (w < 0.75f)
            {
                float t = (w - 0.5f) / 0.25f;
                rgb = Color.Lerp(new Color(0.1f, 0.9f, 0.2f), new Color(1f, 0.9f, 0f), t);
            }
            else
            {
                float t = (w - 0.75f) / 0.25f;
                rgb = Color.Lerp(new Color(1f, 0.9f, 0f), new Color(1f, 0.05f, 0.05f), t);
            }

            // アルファ値: 0.25 -> 0.75（下地が見える半透明）
            float alpha = Mathf.Lerp(0.25f, 0.75f, w);
            return new Color(rgb.r, rgb.g, rgb.b, alpha);
        }

        private void DrawHeatmap(WeightedSpawnSurface surface)
        {
            if (surface.SurfaceMesh == null || !surface.IsDataValid) return;
            EnsureResources(surface);
            if (_overlayMesh != null && _overlayMaterial != null)
            {
                _overlayMaterial.SetPass(0);
                Graphics.DrawMeshNow(_overlayMesh, surface.transform.localToWorldMatrix);
            }
        }

        private static void DrawWireframe(WeightedSpawnSurface surface)
        {
            Mesh mesh = surface.SurfaceMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            var lines = new Vector3[triangles.Length * 2];
            int lineIdx = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = surface.transform.TransformPoint(vertices[triangles[i]]);
                Vector3 b = surface.transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 c = surface.transform.TransformPoint(vertices[triangles[i + 2]]);
                lines[lineIdx++] = a; lines[lineIdx++] = b;
                lines[lineIdx++] = b; lines[lineIdx++] = c;
                lines[lineIdx++] = c; lines[lineIdx++] = a;
            }
            Handles.DrawLines(lines);
        }

        private void DrawBrush(Vector3 center, Vector3 normal, WeightedSpawnSurface surface)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.85f);
            Vector3 discNormal = normal.sqrMagnitude > 1e-4f ? normal : Vector3.up;
            Handles.DrawWireDisc(center, discNormal, _brushSize);

            float currentWeight = surface.GetWeightAtWorldPosition(center);
            Handles.Label(center + discNormal * 0.05f, $"Weight: {currentWeight:0.###}");
        }

        private void ApplyAll(string undoName, float value)
        {
            if (!Surface.IsDataValid || !EditorUtility.DisplayDialog(undoName, "対象Surface全体のWeight Mapを変更します。", "実行", "キャンセル")) return;
            Undo.RecordObject(Surface, undoName);
            Surface.SetAllWeights(value);
            _textureDirty = true;
            EditorUtility.SetDirty(Surface);
            PrefabUtility.RecordPrefabInstancePropertyModifications(Surface);
            SceneView.RepaintAll();
        }

        private void RebuildData()
        {
            Mesh mesh = Surface.SurfaceMesh;
            if (mesh == null) return;
            if (!EditorUtility.DisplayDialog("Rebuild Weight Data", "現在のWeight Mapを破棄して再生成します。", "実行", "キャンセル")) return;
            Undo.RecordObject(Surface, "Rebuild Spawn Weight Data");
            Surface.InitializeFromMesh(mesh);
            _meshDirty = true;
            _textureDirty = true;
            EditorUtility.SetDirty(Surface);
            PrefabUtility.RecordPrefabInstancePropertyModifications(Surface);
            SceneView.RepaintAll();
        }
    }
}
