using System;
using System.Collections.Generic;
using System.Linq;
using Shinzui.View;
using Shinzui.View.Flashlight;
using Shinzui.View.LoopTunnel;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shinzui.Editor.LookDev
{
    /// <summary>保存せずに再生中のポータル境界を同じ手順で確認する</summary>
    internal static class PortalSeamTest
    {
        private static readonly List<TunnelGateView> Gates = new();
        private static PlayerView lastPlayer;
        private static int nextGate;

        /// <summary>再生の開始と終了で検証対象の巡回状態を破棄する</summary>
        [InitializeOnLoadMethod]
        private static void RegisterReset()
        {
            EditorApplication.playModeStateChanged += Reset;
        }

        /// <summary>前回の再生状態への参照を解除する</summary>
        /// <param name="state">エディターの再生状態</param>
        private static void Reset(PlayModeStateChange state)
        {
            Gates.Clear();
            lastPlayer = null;
            nextGate = 0;
        }

        /// <summary>導入を正規APIで中断し最寄りから順にポータルの手前へ移動する</summary>
        [MenuItem("Shinzui/LookDev/6 Portal Seam Test")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "StageTemp")
            {
                Debug.LogWarning("Portal seam test skipped: enter Play Mode in StageTemp; no assets changed");
                return;
            }
            PlayerView player = UnityEngine.Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.isActiveAndEnabled && p.MainCamera == Camera.main);
            if (player == null)
            {
                Debug.LogWarning("Portal seam test skipped: active player/main camera unavailable");
                return;
            }
            int intros = 0;
            foreach (StageIntroView intro in UnityEngine.Object.FindObjectsByType<StageIntroView>(FindObjectsSortMode.None))
            {
                if (intro.Phase == StageIntroView.IntroPhase.Complete) continue;
                intro.Cancel();
                intros++;
                Debug.Log($"Portal seam test intro cancelled: {intro.name}, phase={intro.Phase}");
            }

            /* 初回は距離順を記録し二回目以降は移動後の距離で並べ直さない */
            if (lastPlayer != player || Gates.Count == 0 || Gates.Any(g => g == null))
            {
                Gates.Clear();
                Gates.AddRange(UnityEngine.Object.FindObjectsByType<TunnelGateView>(FindObjectsSortMode.None)
                    .Where(g => g.isActiveAndEnabled && g.TargetGate != null && g.TargetGate.isActiveAndEnabled && g.Collider != null)
                    .OrderBy(g => (g.Collider.bounds.center - player.CameraPosition).sqrMagnitude)
                    .ThenBy(g => Hierarchy(g.transform), StringComparer.Ordinal));
                lastPlayer = player;
                nextGate = 0;
            }
            if (Gates.Count == 0)
            {
                Debug.LogWarning("Portal seam test skipped: no generated paired portals yet; retry after map generation");
                return;
            }
            TunnelGateView gate = Gates[nextGate % Gates.Count];
            Vector3 center = gate.Collider.bounds.center;
            Vector3 inward = Vector3.ProjectOnPlane(gate.TargetGate.transform.position - gate.transform.position, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, -inward).normalized;
            if (inward.sqrMagnitude < 0.5f)
            {
                Debug.LogWarning($"Portal seam test skipped: invalid horizontal pair {gate.name}");
                return;
            }
            float width = gate.Collider is BoxCollider box
                ? box.transform.TransformVector(Vector3.right * box.size.x).magnitude
                : Mathf.Max(gate.Collider.bounds.size.x, gate.Collider.bounds.size.z);
            Vector3 stand = center + inward * 6f + right * Mathf.Min(0.75f, width * 0.1f);
            if (!FindFloor(stand, player, out RaycastHit floor))
            {
                Debug.LogWarning($"Portal seam test skipped: no walkable floor before {gate.name} at {stand}");
                return;
            }
            CharacterController controller = player.GetComponent<CharacterController>();
            float footOffset = controller != null ? controller.bounds.min.y - player.transform.position.y : 0f;
            stand.y = floor.point.y - footOffset + 0.05f;
            Vector3 edge = center + right * (width * 0.5f);
            /* 矩形の外端が壁の内部にある場合は目の高さで見える開口端へ向ける */
            Vector3 edgeOrigin = center;
            edgeOrigin.y = stand.y + player.CameraPosition.y - player.transform.position.y;
            float edgeDistance = width * 0.5f;
            foreach (RaycastHit hit in Physics.RaycastAll(edgeOrigin, right, edgeDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(player.transform) || Mathf.Abs(hit.normal.y) > 0.6f || hit.distance >= edgeDistance) continue;
                edgeDistance = hit.distance;
                edge = hit.point;
            }
            Vector3 facing = Vector3.ProjectOnPlane(edge - stand, Vector3.up).normalized;
            /* PlayerViewがCharacterControllerとCinemachineの内部状態へワープを通知する */
            player.Warp(stand - player.transform.position, facing);
            Quaternion rotation = Quaternion.LookRotation(facing, Vector3.up);
            player.MainCamera.transform.rotation = rotation;
            foreach (CinemachineCamera camera in UnityEngine.Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            {
                Transform follow = camera.Follow;
                if (follow == null || (follow != player.transform && !follow.IsChildOf(player.transform))) continue;
                camera.ForceCameraPosition(player.CameraPosition, rotation);
            }
            Physics.SyncTransforms();
            int flashlights = 0;
            foreach (FlashlightView view in UnityEngine.Object.FindObjectsByType<FlashlightView>(FindObjectsSortMode.None))
            {
                if (view.TargetCamera != player.MainCamera) continue;
                view.SetLookDevLightOverride(true);
                Light light = view.HeldLightTransform != null ? view.HeldLightTransform.GetComponent<Light>() : null;
                if (light != null) Debug.Log($"Portal seam flashlight: {light.name}, enabled={light.enabled}, intensity={light.intensity:F0} native cd, cone={light.spotAngle}, range={light.range}, cookie={light.cookie?.name}, shadows={light.shadows}");
                flashlights++;
            }
            int selected = nextGate % Gates.Count + 1;
            nextGate = (nextGate + 1) % Gates.Count;
            Debug.Log($"Portal seam test: portal={selected}/{Gates.Count}, source={Hierarchy(gate.transform)}, destination={Hierarchy(gate.TargetGate.transform)}, distance=6m, loop offset={gate.TargetGate.transform.position - gate.transform.position}, floor={floor.collider.name}@{floor.point}, player={player.transform.position}, camera={player.CameraPosition}, facing={facing}, camera FOV={player.MainCamera.fieldOfView}, cancelled intros={intros}, forced flashlights={flashlights}; battery unchanged, force-on lasts until leaving Play Mode; run menu again for next portal; no assets saved");
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlaying || gate == null || player == null) return;
                RenderTexture rt = gate.PortalRT;
                foreach (Camera portalCamera in gate.GetComponentsInChildren<Camera>(true))
                    Debug.Log($"Portal seam camera: {portalCamera.name}, position={portalCamera.transform.position}, forward={portalCamera.transform.forward}, cullingMask={portalCamera.cullingMask}, target={portalCamera.targetTexture?.name}");
                Debug.Log($"Portal seam render: source={Hierarchy(gate.transform)}, RT={(rt != null ? rt.width + "x" + rt.height + " " + rt.format : "not rendered yet")}, renderer enabled={gate.PortalRenderer != null && gate.PortalRenderer.enabled}, camera={player.MainCamera.name}, edge viewport={player.MainCamera.WorldToViewportPoint(new Vector3(edge.x, player.CameraPosition.y, edge.z))}, exposure multiplier override={gate.PortalExposureMultiplier}");
            };
        }

        /// <summary>屋根やプレイヤーを除き道路と歩道を優先して足元を探す</summary>
        /// <param name="position">予定する立ち位置</param>
        /// <param name="player">移動対象</param>
        /// <param name="floor">足元の衝突面</param>
        /// <returns>歩行可能な面が見つかった場合はtrue</returns>
        private static bool FindFloor(Vector3 position, PlayerView player, out RaycastHit floor)
        {
            floor = default;
            int bestPriority = int.MaxValue;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(position + Vector3.up * 2f, Vector3.down, 25f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < 0.7f || hit.collider.transform.IsChildOf(player.transform) || hit.collider.attachedRigidbody != null) continue;
                string name = hit.collider.name.ToLowerInvariant();
                if (name.Contains("ceiling") || name.Contains("header")) continue;
                int priority = name.Contains("floor") || name.Contains("road") || name.Contains("walkway") ? 0 : 1;
                if (priority > bestPriority || priority == bestPriority && hit.distance >= nearest) continue;
                bestPriority = priority;
                nearest = hit.distance;
                floor = hit;
            }
            return bestPriority != int.MaxValue;
        }

        /// <summary>同名ゲートを区別する階層名を作る</summary>
        /// <param name="target">対象のTransform</param>
        /// <returns>シーン内の階層名</returns>
        private static string Hierarchy(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return path;
        }
    }
}
