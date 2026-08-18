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
        [SerializeField] private float viewDistance = 7.5f;
        [SerializeField] private float viewAngle = 65.0f;
        [SerializeField] private Transform eyePoint;
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Detection Grace & Sensitivity")]
        [Tooltip("発見までの猶予時間")]
        [SerializeField] private float detectionDelay = 0.35f;

        [Header("Detection State")]
        [SerializeField] private bool isPlayerDetected;

        private float _currentDetectionTimer = 0.0f;

        public Transform PlayerTarget => playerTarget;
        public bool IsPlayerDetected => isPlayerDetected;

        private void Awake()
        {
            viewDistance = 7.5f;
            viewAngle = 65.0f;
            detectionDelay = 0.35f;

            if (eyePoint == null)
            {
                eyePoint = transform;
            }
        }

        /// <summary>
        /// 索敵対象のプレイヤーTargetを設定する
        /// </summary>
        /// <param name="target">プレイヤーのTransform</param>
        public void SetTarget(Transform target)
        {
            playerTarget = target;
        }

        private void Update()
        {
            DetectPlayer();
        }

        /// <summary>
        /// 視覚判定（距離、角度、遮蔽物レイキャスト）を実行してプレイヤーの発見状態を更新する
        /// </summary>
        private void DetectPlayer()
        {
            int visibleCount = 0;
            int totalPoints = 0;

            if (playerTarget != null)
            {
                Vector3 origin = eyePoint != null ? eyePoint.position : transform.position + Vector3.up * 1.6f;
                Vector3 forward = eyePoint != null ? eyePoint.forward : transform.forward;

                Vector3[] checkPoints = GetTargetCheckPositions();
                totalPoints = checkPoints.Length;

                foreach (Vector3 targetPos in checkPoints)
                {
                    Vector3 directionToTarget = targetPos - origin;
                    float distanceToTarget = directionToTarget.magnitude;

                    if (distanceToTarget > viewDistance) continue;

                    Vector3 normalizedDir = directionToTarget / Mathf.Max(distanceToTarget, 0.0001f);
                    float angleToTarget = Vector3.Angle(forward, normalizedDir);
                    if (angleToTarget > viewAngle * 0.5f) continue;

                    if (Physics.Raycast(origin, normalizedDir, out RaycastHit hit, distanceToTarget, obstacleMask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform == playerTarget || hit.transform.IsChildOf(playerTarget))
                        {
                            visibleCount++;
                        }
                    }
                    else
                    {
                        visibleCount++;
                    }
                }
            }

            // 露出度に応じた発見タイマーの更新
            if (visibleCount >= totalPoints && visibleCount > 0)
            {
                _currentDetectionTimer += Time.deltaTime;
            }
            else if (visibleCount > 0)
            {
                _currentDetectionTimer += Time.deltaTime * 0.3f;
            }
            else
            {
                _currentDetectionTimer = Mathf.Max(0.0f, _currentDetectionTimer - Time.deltaTime * 2.5f);
            }

            if (_currentDetectionTimer >= detectionDelay)
            {
                isPlayerDetected = true;
            }
            else if (_currentDetectionTimer <= 0.0f)
            {
                isPlayerDetected = false;
            }
        }

        /// <summary>
        /// 視認チェック対象のプレイヤー部位座標（頭部、胴体）を取得する
        /// </summary>
        /// <returns>チェック対象座標配列</returns>
        private Vector3[] GetTargetCheckPositions()
        {
            if (playerTarget == null) return new Vector3[] { transform.position };

            var points = new System.Collections.Generic.List<Vector3>();

            CharacterController controller = playerTarget.GetComponent<CharacterController>();
            CapsuleCollider capsule = playerTarget.GetComponent<CapsuleCollider>();

            Vector3 centerPos = playerTarget.position;
            float height = 1.8f;

            if (controller != null)
            {
                centerPos = playerTarget.position + controller.center;
                height = controller.height;
            }
            else if (capsule != null)
            {
                centerPos = playerTarget.position + capsule.center;
                height = capsule.height;
            }

            Vector3 headPos = centerPos + Vector3.up * (height * 0.25f);
            Vector3 torsoPos = centerPos;

            points.Add(headPos);
            points.Add(torsoPos);

            return points.ToArray();
        }

        private void OnDrawGizmosSelected()
        {
            DrawVisionGizmos();
        }

        private void OnDrawGizmos()
        {
            DrawVisionGizmos();
        }

        /// <summary>
        /// Sceneビュー上で敵の視界範囲および視線レイを描画する
        /// </summary>
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

            // プレイヤー発見時は頭部・胴体への視認ラインを描画
            if (isPlayerDetected && playerTarget != null)
            {
                Gizmos.color = Color.red;
                foreach (Vector3 checkPos in GetTargetCheckPositions())
                {
                    Gizmos.DrawLine(origin, checkPos);
                }
            }
        }
    }
}
