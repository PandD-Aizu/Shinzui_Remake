using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Shinzui.View
{
    /// <summary>
    /// 敵のUnityコンポーネント参照とInspector設定を保持するView
    /// </summary>
    public class EnemyView : MonoBehaviour
    {
        [Header("Player Death")]
        [SerializeField] private float playerDeathDistance = 1.2f;
        [SerializeField] private float deathAttemptCooldown = 1.0f;

        [Header("References")]
        [SerializeField] private NavMeshAgent agentOverride;

        private float _gizmoChaseDistance = -1.0f;
        private readonly Plane[] _visibilityPlanes = new Plane[6];
        private readonly RaycastHit[] _visibilityHits = new RaycastHit[32];
        private readonly List<(Renderer renderer, Material[] originals, bool visible)> _fadeRenderers = new();
        private readonly List<(Material material, Color color, float opacity)> _fadeMaterials = new();
        private Coroutine _fadeCoroutine;
        private Renderer[] _backstageRenderers;
        private Collider[] _backstageColliders;
        private Animator[] _backstageAnimators;
        private bool[] _rendererEnabled;
        private bool[] _colliderEnabled;
        private bool[] _animatorEnabled;
        private Transform _body;
        public bool IsBackstage { get; private set; }

        /// <summary>捕獲演出に映り込まないよう実時間でフェードアウト</summary>
        public void FadeOut()
        {
            if (IsBackstage || !isActiveAndEnabled || _fadeRenderers.Count > 0) return;

            // 共有マテリアルを変更せず、敵本体の描画だけを複製して操作
            var agent = ResolveAgent();
            Transform body = agent != null && transform.IsChildOf(agent.transform) ? agent.transform : transform;
            foreach (var renderer in body.GetComponentsInChildren<Renderer>())
            {
                _fadeRenderers.Add((renderer, renderer.sharedMaterials, renderer.enabled));
                foreach (var material in renderer.materials)
                {
                    if (material == null) continue;
                    Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                    float opacity = material.HasProperty("_Opacity") ? material.GetFloat("_Opacity") : 1f;
                    _fadeMaterials.Add((material, color, opacity));

                    // URPの不透明マテリアルをアルファブレンドへ切り替える
                    if (!material.HasProperty("_Surface")) continue;
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0f);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                }
            }

            _fadeCoroutine = StartCoroutine(FadeOutRoutine());
        }

        /// <summary>本体、目、屈折の表示量を同時に下げて描画を停止</summary>
        /// <returns>各フレームの待機</returns>
        private IEnumerator FadeOutRoutine()
        {
            const float duration = .6f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, elapsed / duration);
                foreach (var entry in _fadeMaterials)
                {
                    if (entry.material.HasProperty("_Opacity"))
                        entry.material.SetFloat("_Opacity", entry.opacity * alpha);
                    if (entry.material.HasProperty("_BaseColor"))
                    {
                        Color color = entry.color;
                        color.a *= alpha;
                        entry.material.SetColor("_BaseColor", color);
                    }
                }
                yield return null;
            }

            // フェード完了後は描画そのものを止める
            foreach (var entry in _fadeRenderers)
                if (entry.renderer != null) entry.renderer.enabled = false;
            _fadeCoroutine = null;
        }

        /// <summary>無効化時に元の表示状態を戻し、複製マテリアルを解放</summary>
        private void OnDisable()
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
            foreach (var entry in _fadeRenderers)
            {
                if (entry.renderer == null) continue;
                entry.renderer.sharedMaterials = entry.originals;
                entry.renderer.enabled = !IsBackstage && entry.visible;
            }

            foreach (var entry in _fadeMaterials) Destroy(entry.material);
            _fadeRenderers.Clear();
            _fadeMaterials.Clear();
        }

        /// <summary>敵本体のコンポーネントを初期化時に収集</summary>
        public void PrepareBackstage()
        {
            if (_backstageRenderers != null) return;
            var agent = ResolveAgent();
            _body = agent != null && transform.IsChildOf(agent.transform) ? agent.transform : transform;
            _backstageRenderers = _body.GetComponentsInChildren<Renderer>(true);
            _backstageColliders = _body.GetComponentsInChildren<Collider>(true);
            _backstageAnimators = _body.GetComponentsInChildren<Animator>(true);
            _rendererEnabled = new bool[_backstageRenderers.Length];
            _colliderEnabled = new bool[_backstageColliders.Length];
            _animatorEnabled = new bool[_backstageAnimators.Length];
        }

        /// <summary>元から無効な部品を有効化せず描画、接触、アニメーションを切り替える</summary>
        /// <param name="backstage">退避するか</param>
        public void SetBackstage(bool backstage)
        {
            if (IsBackstage == backstage) return;
            PrepareBackstage();
            IsBackstage = backstage;
            for (int i = 0; i < _backstageRenderers.Length; i++)
            {
                if (_backstageRenderers[i] == null) continue;
                if (backstage) _rendererEnabled[i] = _backstageRenderers[i].enabled;
                _backstageRenderers[i].enabled = !backstage && _rendererEnabled[i];
            }
            for (int i = 0; i < _backstageColliders.Length; i++)
            {
                if (_backstageColliders[i] == null) continue;
                if (backstage) _colliderEnabled[i] = _backstageColliders[i].enabled;
                _backstageColliders[i].enabled = !backstage && _colliderEnabled[i];
            }
            for (int i = 0; i < _backstageAnimators.Length; i++)
            {
                if (_backstageAnimators[i] == null) continue;
                if (backstage) _animatorEnabled[i] = _backstageAnimators[i].enabled;
                _backstageAnimators[i].enabled = !backstage && _animatorEnabled[i];
            }
        }

        /// <summary>出入り前に敵全体の範囲を仮の位置へ移して視認を確認</summary>
        /// <param name="position">候補位置</param>
        /// <param name="camera">プレイヤーカメラ</param>
        /// <param name="playerRoot">プレイヤー本体</param>
        /// <param name="blockingLayers">遮蔽レイヤー</param>
        /// <returns>カメラ不明、または敵の一部が見える場合はtrue</returns>
        public bool IsVisibleAt(Vector3 position, Camera camera, Transform playerRoot, LayerMask blockingLayers)
        {
            if (camera == null || !camera.isActiveAndEnabled) return true;
            PrepareBackstage();
            var agent = ResolveAgent();
            float height = agent != null ? agent.height : 2f;
            Bounds bounds = new(_body.position + Vector3.up * height * .5f, new Vector3(1f, height, 1f));
            for (int i = 0; i < _backstageRenderers.Length; i++)
                if (_backstageRenderers[i] != null) bounds.Encapsulate(_backstageRenderers[i].bounds);
            bounds.center += position - EnemyPosition;
            GeometryUtility.CalculateFrustumPlanes(camera, _visibilityPlanes);
            if (!GeometryUtility.TestPlanesAABB(_visibilityPlanes, bounds)) return false;
            if (bounds.Contains(camera.transform.position)) return true;
            bool hasProjectedPoint = IsInsideViewport(bounds.center, camera);
            Vector3 head = bounds.center + Vector3.up * bounds.extents.y * .8f;
            hasProjectedPoint |= IsInsideViewport(head, camera);
            if (IsPointVisible(head, camera, _body, playerRoot, blockingLayers)) return true;
            if (IsPointVisible(bounds.center, camera, _body, playerRoot, blockingLayers)) return true;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                hasProjectedPoint |= IsInsideViewport(corner, camera);
                if (IsPointVisible(corner, camera, _body, playerRoot, blockingLayers)) return true;
            }
            // 画面端と交差する大きな敵で代表点が全て画面外なら安全側へ倒す
            return !hasProjectedPoint;
        }

        /// <summary>候補点がカメラの描画範囲内にあるか確認</summary>
        /// <param name="point">対象位置</param>
        /// <param name="camera">プレイヤーカメラ</param>
        /// <returns>画面内か</returns>
        private static bool IsInsideViewport(Vector3 point, Camera camera)
        {
            Vector3 p = camera.WorldToViewportPoint(point);
            return p.z >= camera.nearClipPlane && p.z <= camera.farClipPlane && p.x >= 0f && p.x <= 1f && p.y >= 0f && p.y <= 1f;
        }

        public float PlayerDeathDistance => playerDeathDistance;
        public float DeathAttemptCooldown => deathAttemptCooldown;
        public Vector3 TransformPosition => transform.position;

        public Vector3 EnemyPosition
        {
            get
            {
                NavMeshAgent agent = ResolveAgent();
                return agent != null ? agent.transform.position : transform.position;
            }
        }

        /// <summary>
        /// 接近時の死亡判定に使う敵の体の中心位置
        /// コライダーがない敵は従来の位置を使用
        /// </summary>
        public Vector3 PlayerDeathPosition
        {
            get
            {
                // 足元原点の敵もプレイヤーと体の中心同士で比較
                NavMeshAgent agent = ResolveAgent();
                Collider bodyCollider = agent != null ? agent.GetComponent<Collider>() : null;
                if (bodyCollider == null) bodyCollider = GetComponent<Collider>();

                return bodyCollider != null && bodyCollider.enabled
                    ? bodyCollider.bounds.center
                    : EnemyPosition;
            }
        }

        /// <summary>カメラ内の敵の頭部または胴体まで遮蔽物がないか判定</summary>
        /// <param name="camera">プレイヤーの視点カメラ</param>
        /// <param name="playerRoot">視線判定から除外するプレイヤー</param>
        /// <param name="blockingLayers">視線を遮るレイヤー</param>
        /// <returns>敵を視認できる場合はtrue</returns>
        public bool IsVisibleFrom(Camera camera, Transform playerRoot, LayerMask blockingLayers)
        {
            if (camera == null || !camera.isActiveAndEnabled || !isActiveAndEnabled) return false;

            // 移動用原点ではなく体の中心と頭部を視認対象にする
            NavMeshAgent agent = ResolveAgent();
            Transform body = agent != null ? agent.transform : transform;
            Collider bodyCollider = body.GetComponent<Collider>();
            Bounds bounds = bodyCollider != null && bodyCollider.enabled
                ? bodyCollider.bounds
                : new Bounds(body.position + Vector3.up * (agent != null ? agent.height : 2f) * .5f,
                    new Vector3(1f, agent != null ? agent.height : 2f, 1f));
            return IsPointVisible(bounds.center, camera, body, playerRoot, blockingLayers)
                || IsPointVisible(bounds.center + Vector3.up * bounds.extents.y * .8f,
                    camera, body, playerRoot, blockingLayers);
        }

        /// <summary>対象点が画面内にあり視線が遮られていないか判定</summary>
        /// <param name="point">対象のワールド座標</param>
        /// <param name="camera">プレイヤーの視点カメラ</param>
        /// <param name="body">敵の本体</param>
        /// <param name="playerRoot">プレイヤーのルート</param>
        /// <param name="blockingLayers">視線を遮るレイヤー</param>
        /// <returns>対象点を視認できる場合はtrue</returns>
        private bool IsPointVisible(Vector3 point, Camera camera, Transform body, Transform playerRoot,
            LayerMask blockingLayers)
        {
            // 背後や画面外、描画距離外の敵は認知しない
            Vector3 viewport = camera.WorldToViewportPoint(point);
            if (viewport.z < camera.nearClipPlane || viewport.z > camera.farClipPlane ||
                viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) return false;

            // 自分と対象のコライダーを除外して壁越しの演出を防ぐ
            Vector3 origin = camera.transform.position;
            Vector3 offset = point - origin;
            int count = Physics.RaycastNonAlloc(origin, offset.normalized, _visibilityHits, offset.magnitude,
                blockingLayers, QueryTriggerInteraction.Ignore);
            if (count == _visibilityHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Transform hit = _visibilityHits[i].transform;
                if (hit == body || hit.IsChildOf(body) || body.IsChildOf(hit)) continue;
                if (playerRoot != null && (hit == playerRoot || hit.IsChildOf(playerRoot))) continue;
                return false;
            }

            return true;
        }

        /// <summary>
        /// NavMeshAgentを解決する
        /// agentOverrideが設定されていればそれを返し、なければ自身のコンポーネント、親、子の順に探す。別オブジェクトのAgentは取得しない
        /// </summary>
        /// <returns></returns>
        public NavMeshAgent ResolveAgent()
        {
            if (agentOverride != null)
            {
                return agentOverride;
            }

            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                return agent;
            }

            agent = GetComponentInParent<NavMeshAgent>();
            if (agent != null)
            {
                return agent;
            }

            agent = GetComponentInChildren<NavMeshAgent>();
            if (agent != null)
            {
                return agent;
            }

            return null;
        }

        /// <summary>
        /// Gizmoで描画する追跡距離を設定する
        /// </summary>
        /// <param name="chaseDistance"></param>
        public void SetGizmoChaseDistance(float chaseDistance)
        {
            _gizmoChaseDistance = chaseDistance;
        }

        /// <summary>
        /// 追跡範囲と実際の死亡判定位置を描画
        /// </summary>
        private void OnDrawGizmos()
        {
            if (_gizmoChaseDistance >= 0f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, _gizmoChaseDistance);
            }

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(PlayerDeathPosition, playerDeathDistance);
        }
    }
}
