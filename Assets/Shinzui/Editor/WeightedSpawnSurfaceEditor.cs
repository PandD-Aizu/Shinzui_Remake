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
        private ToolMode _mode;
        private bool _paintMode;
        private bool _showWeights = true;
        private float _brushSize = 1f;
        private float _brushStrength = 0.25f;
        private float _targetWeight = 1f;
        private int _selectedTriangle = -1;
        private bool _undoRecorded;

        private WeightedSpawnSurface Surface => (WeightedSpawnSurface)target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (!Surface.IsDataValid)
            {
                EditorGUILayout.HelpBox(
                    "保存済みWeightと現在のMeshが一致しません。古いWeightは適用されません。\n" +
                    "Meshを戻すか、「Rebuild Weight Data」で再生成してください。",
                    MessageType.Warning);
                if (GUILayout.Button("Rebuild Weight Data")) RebuildData();
                return;
            }

            MeshCollider meshCollider = Surface.GetComponent<MeshCollider>();
            if (meshCollider == null || meshCollider.sharedMesh != Surface.SourceMesh)
            {
                EditorGUILayout.HelpBox(
                    "Scene ViewでTriangleを特定するには、同じGameObjectに" +
                    "Source Meshと同じMeshを参照するMeshColliderが必要です。",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                _paintMode = EditorGUILayout.ToggleLeft("Paint Mode", _paintMode);
                _showWeights = EditorGUILayout.ToggleLeft("Show Weights", _showWeights);
                _brushSize = Mathf.Max(0.01f, EditorGUILayout.FloatField("Brush Size", _brushSize));
                _brushStrength = Mathf.Clamp01(EditorGUILayout.FloatField("Brush Strength", _brushStrength));
                _targetWeight = Mathf.Max(0f, EditorGUILayout.FloatField("Target Weight", _targetWeight));
                _mode = (ToolMode)EditorGUILayout.EnumPopup("Tool", _mode);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fill")) ApplyAll("Fill Spawn Weights", _targetWeight);
                if (GUILayout.Button("Clear")) ApplyAll("Clear Spawn Weights", 0f);
            }

            string selected = _selectedTriangle >= 0
                ? $"Selected Triangle: {_selectedTriangle}  Weight: {Surface.GetWeight(_selectedTriangle):0.###}"
                : "Selected Triangle: -";
            EditorGUILayout.HelpBox(
                $"{selected}\nTriangles: {Surface.TriangleCount}\n" +
                "Scene View: left drag to paint. Hold Alt to orbit the camera.", MessageType.Info);
            SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            WeightedSpawnSurface surface = Surface;
            if (_showWeights && surface.IsDataValid) DrawWeights(surface);
            if (!_paintMode || !surface.IsDataValid) return;

            Event e = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if (e.alt || e.button != 0) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!TryRaycast(surface, ray, out RaycastHit hit)) return;
            _selectedTriangle = hit.triangleIndex;
            DrawBrush(hit.point, surface);

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

        private void PaintAt(WeightedSpawnSurface surface, Vector3 center, Event e)
        {
            if (!_undoRecorded)
            {
                Undo.RecordObject(surface, $"{_mode} Spawn Weights");
                _undoRecorded = true;
            }

            Mesh mesh = surface.SourceMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            var affected = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = surface.transform.TransformPoint(vertices[triangles[i]]);
                Vector3 b = surface.transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 c = surface.transform.TransformPoint(vertices[triangles[i + 2]]);
                Vector3 centroid = (a + b + c) / 3f;
                float distance = Vector3.Distance(center, centroid);
                if (distance > _brushSize) continue;
                affected.Add(i / 3);
                float falloff = 1f - Mathf.Clamp01(distance / _brushSize);
                float amount = _brushStrength * falloff;
                float current = surface.GetWeight(i / 3);
                float next = _mode == ToolMode.Smooth
                    ? Mathf.Lerp(current, GetNeighbourAverage(surface, i / 3, triangles), amount)
                    : Mathf.Lerp(current, _mode == ToolMode.Erase ? 0f : _targetWeight, amount);
                surface.SetWeight(i / 3, next);
            }
            if (affected.Count > 0) EditorUtility.SetDirty(surface);
            e.Use();
            SceneView.RepaintAll();
        }

        private static float GetNeighbourAverage(WeightedSpawnSurface surface, int index, int[] triangles)
        {
            int a = triangles[index * 3], b = triangles[index * 3 + 1], c = triangles[index * 3 + 2];
            float sum = 0f; int count = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (i / 3 == index) continue;
                if (SharesVertex(a, b, c, triangles[i], triangles[i + 1], triangles[i + 2]))
                {
                    sum += surface.GetWeight(i / 3); count++;
                }
            }
            return count == 0 ? surface.GetWeight(index) : sum / count;
        }

        private static bool SharesVertex(int a, int b, int c, int x, int y, int z) =>
            a == x || a == y || a == z || b == x || b == y || b == z || c == x || c == y || c == z;

        private static bool TryRaycast(WeightedSpawnSurface surface, Ray ray, out RaycastHit hit)
        {
            MeshCollider collider = surface.GetComponent<MeshCollider>();
            if (collider != null && collider.sharedMesh == surface.SourceMesh && collider.Raycast(ray, out hit, 10000f)) return true;
            hit = default;
            return false;
        }

        private static void DrawWeights(WeightedSpawnSurface surface)
        {
            Mesh mesh = surface.SourceMesh;
            Vector3[] vertices = mesh.vertices; int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = surface.transform.TransformPoint(vertices[triangles[i]]);
                Vector3 b = surface.transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 c = surface.transform.TransformPoint(vertices[triangles[i + 2]]);
                Handles.color = WeightColor(surface.GetWeight(i / 3));
                Handles.DrawAAConvexPolygon(a, b, c);
            }
        }

        private static Color WeightColor(float weight)
        {
            float normalized = Mathf.Clamp01(weight);
            Color color = Color.Lerp(new Color(0.1f, 0.1f, 0.1f, 0.2f), Color.Lerp(Color.yellow, Color.red, normalized), normalized);
            color.a = 0.28f;
            return color;
        }

        private void DrawBrush(Vector3 center, WeightedSpawnSurface surface)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.8f);
            Handles.DrawWireDisc(center, SceneView.currentDrawingSceneView.camera.transform.forward, _brushSize);
            Handles.Label(center, $"Triangle {_selectedTriangle}: {surface.GetWeight(_selectedTriangle):0.###}");
        }

        private void ApplyAll(string undoName, float value)
        {
            if (!Surface.IsDataValid || !EditorUtility.DisplayDialog(undoName, "対象Mesh全体のWeightを変更します。", "実行", "キャンセル")) return;
            Undo.RecordObject(Surface, undoName);
            Surface.SetAllWeights(value);
            EditorUtility.SetDirty(Surface);
            SceneView.RepaintAll();
        }

        private void RebuildData()
        {
            Mesh mesh = Surface.GetCurrentMesh();
            if (mesh == null) return;
            if (!EditorUtility.DisplayDialog("Rebuild Weight Data", "現在のWeightを破棄して再生成します。", "実行", "キャンセル")) return;
            Undo.RecordObject(Surface, "Rebuild Spawn Weight Data");
            Surface.InitializeFromMesh(mesh);
            EditorUtility.SetDirty(Surface);
            SceneView.RepaintAll();
        }
    }
}
