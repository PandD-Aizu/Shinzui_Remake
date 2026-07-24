using Shinzui.View;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Shinzui.Editor
{
    [CustomEditor(typeof(SpiderWeb))]
    [CanEditMultipleObjects]
    public sealed class SpiderWebEditor : UnityEditor.Editor
    {
        private bool _anchorPlacementMode;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate Web"))
                {
                    foreach (Object item in targets)
                    {
                        var web = (SpiderWeb)item;
                        Undo.RecordObject(web, "Regenerate Spider Web");
                        web.Regenerate();
                        EditorUtility.SetDirty(web);
                    }
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Reset Simulation"))
                {
                    foreach (Object item in targets)
                    {
                        ((SpiderWeb)item).ResetSimulation();
                    }
                    SceneView.RepaintAll();
                }
            }

            SpiderWeb current = (SpiderWeb)target;
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(targets.Length != 1))
            {
                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = _anchorPlacementMode
                    ? new Color(0.45f, 1.0f, 0.65f)
                    : previousColor;
                if (GUILayout.Button(
                        _anchorPlacementMode
                            ? "Finish Anchor Placement"
                            : "Place Manual Anchors",
                        GUILayout.Height(30.0f)))
                {
                    _anchorPlacementMode = !_anchorPlacementMode;
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = previousColor;

                if (_anchorPlacementMode)
                {
                    EditorGUILayout.HelpBox(
                        "Sceneビューで Ctrl + 左クリックすると、Collider表面に" +
                        "接着点を何点でも追加できます。別々のColliderにも配置でき、" +
                        "3点以上で手動輪郭に切り替わる",
                        MessageType.Info);
                }

                if (current.ManualAnchors.Count > 0 &&
                    GUILayout.Button("Clear Manual Anchors"))
                {
                    ClearManualAnchors(current);
                }
            }

            if (GUILayout.Button("Snap & Attach To Surface", GUILayout.Height(28.0f)))
            {
                foreach (Object item in targets)
                {
                    var web = (SpiderWeb)item;
                    Undo.RecordObject(web, "Attach Spider Web To Surface");
                    Undo.RecordObject(web.transform, "Attach Spider Web To Surface");
                    if (web.AttachmentSurface != null)
                    {
                        Undo.RecordObject(
                            web.AttachmentSurface.transform,
                            "Attach Spider Web To Surface");
                    }
                    web.SnapAndAttachToSurface();
                    EditorUtility.SetDirty(web);
                }
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Conform Threads To Surface"))
            {
                foreach (Object item in targets)
                {
                    var web = (SpiderWeb)item;
                    Undo.RecordObject(web, "Conform Spider Web To Surface");
                    web.ConformToSurface();
                    EditorUtility.SetDirty(web);
                }
                SceneView.RepaintAll();
            }

            EditorGUILayout.HelpBox(
                $"Nodes: {current.NodeCount} / Threads: {current.ThreadCount}\n" +
                $"Manual anchors: {current.ManualAnchors.Count} " +
                $"({(current.UsesManualAnchors ? "active" : "need 3")})\n" +
                $"Interaction colliders: {current.InteractionColliderCount}\n" +
                "Assign a wall/floor Collider to Attachment Surface, then use " +
                "Snap & Attach. MeshCollider supports uneven surfaces.\n" +
                "The web is generated on local XY. Local Z is its surface normal.",
                current.AttachmentSurface == null
                    ? MessageType.Warning
                    : MessageType.Info);
        }

        private void OnSceneGUI()
        {
            if (target == null)
            {
                return;
            }

            var web = (SpiderWeb)target;
            DrawAnchorHandles(web);

            if (!_anchorPlacementMode)
            {
                return;
            }

            HandleUtility.AddDefaultControl(
                GUIUtility.GetControlID(FocusType.Passive));
            Event currentEvent = Event.current;
            if (currentEvent.type != EventType.MouseDown ||
                currentEvent.button != 0 ||
                currentEvent.alt ||
                !(currentEvent.control || currentEvent.command))
            {
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
            if (!TryGetAnchorHit(web, ray, out RaycastHit hit))
            {
                return;
            }

            var anchorObject = new GameObject(
                $"Spider Web Anchor {web.ManualAnchors.Count + 1:00}");
            Undo.RegisterCreatedObjectUndo(
                anchorObject,
                "Create Spider Web Anchor");
            anchorObject.transform.SetParent(web.transform, true);
            anchorObject.transform.position =
                hit.point + hit.normal * web.SurfaceOffset;

            Undo.RecordObject(web, "Add Spider Web Anchor");
            web.AddManualAnchor(anchorObject.transform);
            EditorUtility.SetDirty(web);
            Selection.activeGameObject = web.gameObject;
            currentEvent.Use();
            SceneView.RepaintAll();
        }

        private static void DrawAnchorHandles(SpiderWeb web)
        {
            for (int i = 0; i < web.ManualAnchors.Count; i++)
            {
                Transform anchor = web.ManualAnchors[i];
                if (anchor == null)
                {
                    continue;
                }

                float size = HandleUtility.GetHandleSize(anchor.position) * 0.08f;
                Handles.color = new Color(0.15f, 1.0f, 0.5f, 1.0f);
                Handles.SphereHandleCap(
                    0,
                    anchor.position,
                    Quaternion.identity,
                    size,
                    EventType.Repaint);
                Handles.Label(
                    anchor.position + Vector3.up * size,
                    $"Anchor {i + 1}");

                EditorGUI.BeginChangeCheck();
                Vector3 newPosition = Handles.PositionHandle(
                    anchor.position,
                    anchor.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(anchor, "Move Spider Web Anchor");
                    anchor.position = newPosition;
                    web.Regenerate();
                    EditorUtility.SetDirty(web);
                }
            }
        }

        private static bool TryGetAnchorHit(
            SpiderWeb web,
            Ray ray,
            out RaycastHit hit)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                10000.0f,
                ~0,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(
                hits,
                (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit candidate in hits)
            {
                if (candidate.collider.transform.IsChildOf(web.transform))
                {
                    continue;
                }

                hit = candidate;
                if (web.AttachmentSurface == null)
                {
                    web.SetAttachmentSurface(candidate.collider);
                }
                return true;
            }

            hit = default;
            return false;
        }

        private static void ClearManualAnchors(SpiderWeb web)
        {
            var anchors = new Transform[web.ManualAnchors.Count];
            for (int i = 0; i < anchors.Length; i++)
            {
                anchors[i] = web.ManualAnchors[i];
            }

            Undo.RecordObject(web, "Clear Spider Web Anchors");
            web.ClearManualAnchors();
            foreach (Transform anchor in anchors)
            {
                if (anchor != null &&
                    anchor.parent == web.transform &&
                    anchor.name.StartsWith("Spider Web Anchor"))
                {
                    Undo.DestroyObjectImmediate(anchor.gameObject);
                }
            }
            EditorUtility.SetDirty(web);
            SceneView.RepaintAll();
        }

        [MenuItem("GameObject/Shinzui/Spider Web", false, 20)]
        private static void CreateSpiderWeb(MenuCommand menuCommand)
        {
            var gameObject = new GameObject("Spider Web");
            GameObjectUtility.SetParentAndAlign(
                gameObject,
                menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Spider Web");
            var web = gameObject.AddComponent<SpiderWeb>();
            web.Regenerate();
            Selection.activeGameObject = gameObject;
        }

        /// <summary>
        /// CIやバッチモードから蜘蛛の巣シェーダーを実際にコンパイルして検証します。
        /// </summary>
        [MenuItem("Tools/Shinzui/Validate Spider Web Shader")]
        public static void ValidateShaderForBatch()
        {
            const string shaderPath = "Assets/Shinzui/Shaders/SpiderWeb.shader";
            AssetDatabase.ImportAsset(
                shaderPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null)
            {
                throw new System.InvalidOperationException(
                    $"Spider web shader could not be loaded: {shaderPath}");
            }

            ShaderUtil.ClearShaderMessages(shader);
            var material = new Material(shader);
            try
            {
                ShaderUtil.CompilePass(material, 0, true);
            }
            finally
            {
                DestroyImmediate(material);
            }

            ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
            int errorCount = 0;
            foreach (ShaderMessage message in messages)
            {
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    errorCount++;
                    Debug.LogError(
                        $"Spider web shader error ({message.line}): {message.message}");
                }
                else
                {
                    Debug.LogWarning(
                        $"Spider web shader warning ({message.line}): {message.message}");
                }
            }

            if (errorCount > 0)
            {
                throw new System.InvalidOperationException(
                    $"Spider web shader compilation failed with {errorCount} error(s).");
            }

            Debug.Log("Spider web shader compilation succeeded.");
        }
    }
}
