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

        public bool IsPlayerDetected => isPlayerDetected;

        private void Awake()
        {
            AutoSetupReferences();
        }

        private void Reset()
        {
            AutoSetupReferences();
        }

        private void OnValidate()
        {
            AutoSetupReferences();
        }

        [ContextMenu("Auto Setup Vision References")]
        public void AutoSetupReferences()
        {
            // eyePoint が null、または自分・自分の子要素以外（外部のWaypoint等）を指している場合は自動再設定
            if (eyePoint == null || (eyePoint != transform && !eyePoint.IsChildOf(transform)))
            {
                // 子要素から Eye, Head, EnemyHead 等の視点オブジェクトを検索
                Transform foundEye = transform.Find("Eye");
                if (foundEye == null) foundEye = transform.Find("Head");
                if (foundEye == null) foundEye = transform.Find("EnemyEye");
                if (foundEye == null) foundEye = transform.Find("CameraTarget");

                if (foundEye != null)
                {
                    eyePoint = foundEye;
                }
                else
                {
                    // 視点用子オブジェクトが無ければ作成して頭の高さ(1.6m)に配置
                    Transform newEye = transform.Find("VisionEyePoint");
                    if (newEye == null)
                    {
                        GameObject eyeObj = new GameObject("VisionEyePoint");
                        eyeObj.transform.SetParent(transform, false);
                        eyeObj.transform.localPosition = new Vector3(0, 1.6f, 0);
                        newEye = eyeObj.transform;
                    }
                    eyePoint = newEye;
                }
            }

            FindPlayerIfNeeded();
        }

        private void Update()
        {
            FindPlayerIfNeeded();
            DetectPlayer();
        }

        private void FindPlayerIfNeeded()
        {
            if (playerTarget == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    playerTarget = playerObj.transform;
                }
            }
        }

        private void DetectPlayer()
        {
            isPlayerDetected = false;

            if (playerTarget == null || eyePoint == null)
            {
                return;
            }

            // ターゲットへの方向と距離を計算
            Vector3 origin = eyePoint.position;
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
            float angleToTarget = Vector3.Angle(eyePoint.forward, directionToTarget);
            if (angleToTarget > viewAngle * 0.5f)
            {
                return;
            }

            // 3. 遮蔽物レイキャストチェック（目元からプレイヤーまでの距離でレイキャスト）
            // プレイヤーまでの直線上に障害物があるかチェックする
            if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, distanceToTarget, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                // ヒットしたオブジェクトがプレイヤー本人またはその子要素か確認
                if (hit.transform == playerTarget || hit.transform.IsChildOf(playerTarget))
                {
                    isPlayerDetected = true;
                }
                // プレイヤー手前で壁などの障害物に当たった場合は視界遮断（isPlayerDetected = false）
            }
        }

        private Vector3 GetTargetCheckPosition()
        {
            // プレイヤーの子要素にHeadやCameraTargetがあればその位置をチェック、無ければ重心
            if (playerTarget != null)
            {
                Transform head = playerTarget.Find("PlayerHead");
                if (head != null) return head.position;

                Transform target = playerTarget.Find("CameraTarget");
                if (target != null) return target.position;

                // CharacterControllerやCapsuleColliderの中心点
                CharacterController controller = playerTarget.GetComponent<CharacterController>();
                if (controller != null) return playerTarget.position + controller.center;

                CapsuleCollider capsule = playerTarget.GetComponent<CapsuleCollider>();
                if (capsule != null) return playerTarget.position + capsule.center;
            }

            return playerTarget != null ? playerTarget.position : transform.position;
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
