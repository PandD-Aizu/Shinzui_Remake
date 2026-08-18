using UnityEngine;

namespace Shinzui.Temp
{
    /// <summary>
    /// 敵の視覚（視界・扇形判定および遮蔽物レイキャスト）によるプレイヤー索敵スクリプト
    /// </summary>
    public class TempEnemyVision : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField] private Transform playerTarget;

        [Header("Vision Settings")]
        [SerializeField] private float viewDistance = 10.0f;
        [SerializeField] private float viewAngle = 90.0f;
        [SerializeField] private Transform eyePoint;
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Detection State")]
        [SerializeField] private bool isPlayerDetected;

        public Transform PlayerTarget => playerTarget;
        public bool IsPlayerDetected => isPlayerDetected;

        private void Awake()
        {
            if (eyePoint == null)
            {
                eyePoint = transform;
            }
        }

        /// <summary>
        /// ターゲット（プレイヤー）を指定する
        /// </summary>
        public void SetTarget(Transform target)
        {
            playerTarget = target;
        }

        private void Update()
        {
            DetectPlayer();
        }

        private void DetectPlayer()
        {
            isPlayerDetected = false;

            if (playerTarget == null)
            {
                return;
            }

            Transform eye = eyePoint != null ? eyePoint : transform;

            // ターゲットへの方向と距離を計算
            Vector3 origin = eye.position;
            Vector3 targetPosition = GetTargetCheckPosition();
            Vector3 directionToTarget = targetPosition - origin;
            float distanceToTarget = directionToTarget.magnitude;

            // 1. 距離チェック
            if (distanceToTarget > viewDistance)
            {
                return;
            }

            // 2. 視界角（扇形）チェック
            directionToTarget.Normalize();
            float angleToTarget = Vector3.Angle(eye.forward, directionToTarget);
            if (angleToTarget > viewAngle * 0.5f)
            {
                return;
            }

            // 3. 遮蔽物レイキャストチェック（目元からプレイヤーまでの距離でレイキャスト）
            if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, distanceToTarget, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                // ヒットしたオブジェクトがプレイヤー本人またはその子要素か確認
                if (hit.transform == playerTarget || hit.transform.IsChildOf(playerTarget))
                {
                    isPlayerDetected = true;
                }
            }
        }

        private Vector3 GetTargetCheckPosition()
        {
            if (playerTarget == null) return transform.position;

            // CharacterController または CapsuleCollider の中心点があれば使用
            CharacterController controller = playerTarget.GetComponent<CharacterController>();
            if (controller != null) return playerTarget.position + controller.center;

            CapsuleCollider capsule = playerTarget.GetComponent<CapsuleCollider>();
            if (capsule != null) return playerTarget.position + capsule.center;

            return playerTarget.position;
        }

        private void OnDrawGizmosSelected()
        {
            DrawVisionGizmos();
        }

        private void OnDrawGizmos()
        {
            DrawVisionGizmos();
        }

        private void DrawVisionGizmos()
        {
            Transform originTransform = eyePoint != null ? eyePoint : transform;
            Vector3 origin = originTransform.position;

            // 索敵状態に応じたGizmoカラー
            Gizmos.color = isPlayerDetected ? new Color(1.0f, 0.0f, 0.0f, 0.4f) : new Color(1.0f, 1.0f, 0.0f, 0.2f);

            // 視界の境界線を描画
            Vector3 leftRayDirection = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * originTransform.forward;
            Vector3 rightRayDirection = Quaternion.Euler(0, viewAngle * 0.5f, 0) * originTransform.forward;

            Gizmos.DrawRay(origin, leftRayDirection * viewDistance);
            Gizmos.DrawRay(origin, rightRayDirection * viewDistance);
            Gizmos.DrawRay(origin, originTransform.forward * viewDistance);

            // プレイヤー発見時は赤い線を描画
            if (isPlayerDetected && playerTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, GetTargetCheckPosition());
            }
        }
    }
}
