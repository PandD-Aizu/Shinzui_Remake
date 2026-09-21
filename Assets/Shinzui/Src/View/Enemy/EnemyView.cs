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
