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
        /// NavMeshAgentを解決する
        /// agentOverrideが設定されていればそれを返し、なければ自身のコンポーネント、親、子の順に探す。見つからなければ近くのNavMeshAgentを探索する
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

            return FindNearestAgent(8.0f);
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
        /// 指定されたワールド座標にZ軸をワープさせる
        /// 親が存在する場合は親のZ座標を変更する
        /// </summary>
        /// <param name="warpTarget"></param>
        public void WarpTo(Vector3 warpTarget)
        {
            if (transform.parent != null)
            {
                Vector3 pos = transform.parent.position;
                pos.z = warpTarget.z;
                transform.parent.position = pos;
                return;
            }

            Vector3 ownPos = transform.position;
            ownPos.z = warpTarget.z;
            transform.position = ownPos;
        }

        /// <summary>
        /// 指定距離内で最も近いNavMeshAgentを探索する
        /// </summary>
        /// <param name="maxDistance">最大探索距離</param>
        /// <returns>見つかった最も近いNavMeshAgent</returns>
        private NavMeshAgent FindNearestAgent(float maxDistance)
        {
            NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            NavMeshAgent nearestAgent = null;
            float nearestSqrDistance = maxDistance * maxDistance;

            foreach (NavMeshAgent agent in agents)
            {
                float sqrDistance = (agent.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance > nearestSqrDistance)
                {
                    continue;
                }

                nearestSqrDistance = sqrDistance;
                nearestAgent = agent;
            }

            return nearestAgent;
        }

        private void OnDrawGizmos()
        {
            if (_gizmoChaseDistance >= 0f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, _gizmoChaseDistance);
            }

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(EnemyPosition, playerDeathDistance);
        }
    }
}
