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
        private readonly RaycastHit[] _visibilityHits = new RaycastHit[32];
        private readonly List<(Renderer renderer, Material[] originals, bool visible)> _fadeRenderers = new();
        private readonly List<(Material material, Color color, float opacity)> _fadeMaterials = new();
        private Coroutine _fadeCoroutine;

        /// <summary>捕獲演出に映り込まないよう実時間でフェードアウト</summary>
        public void FadeOut()
        {
            if (!isActiveAndEnabled || _fadeRenderers.Count > 0) return;

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
                entry.renderer.enabled = entry.visible;
            }

            foreach (var entry in _fadeMaterials) Destroy(entry.material);
            _fadeRenderers.Clear();
            _fadeMaterials.Clear();
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
