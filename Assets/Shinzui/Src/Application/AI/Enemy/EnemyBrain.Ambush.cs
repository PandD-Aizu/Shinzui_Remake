using Shinzui.Application.AI.BehaviorTree;
using Shinzui.Application.DTOs.Enemy;
using UnityEngine;

namespace Shinzui.Application.AI.Enemy
{
    public sealed partial class EnemyBrain
    {
        private float _ambushCooldown = 20f;
        private float _ambushMoveTime;

        /// <summary>先回りと天井の待ち伏せを行う賢い個体か</summary>
        public bool IsCunning { get; set; }

        /// <summary>手掛かりがない時に、誘導先へ先回りしてから天井で待ち伏せる</summary>
        /// <param name="dt">経過秒数</param>
        /// <returns>待ち伏せ準備中ならRunning</returns>
        internal BtStatus Ambush(float dt)
        {
            if (!IsCunning || _ambushCooldown > 0f) return BtStatus.Failure;

            // ヒントがあればプレイヤーが来そうな区域まで移動してから登る
            DirectorDirective directive = Blackboard.Directive;
            if (directive.Type == DirectorDirectiveType.HintArea && !Arrived(directive.Position)
                && _ambushMoveTime < Positive(_settings.moveTimeout, 6f) * 2f)
            {
                _ambushMoveTime += dt;
                _intent = new EnemyIntent(EnemyIntentType.MoveTo, directive.Position,
                    Positive(_directorSettings.patrolSpeedMultiplier, .65f));
                return BtStatus.Running;
            }
            _intent = new EnemyIntent(EnemyIntentType.CeilingAmbush);
            return BtStatus.Running;
        }

        /// <summary>天井が低い、または見られていて登れなかった場合に少し後で再試行</summary>
        public void CancelAmbush()
        {
            _ambushCooldown = 4f;
            _ambushMoveTime = 0f;
        }

        /// <summary>天井から降りた後の状態を決める</summary>
        /// <param name="engaged">プレイヤーへ飛びかかったか</param>
        /// <param name="playerPosition">飛びかかった相手の位置</param>
        public void FinishAmbush(bool engaged, Vector3 playerPosition)
        {
            _ambushCooldown = engaged ? 45f : 25f;
            _ambushMoveTime = 0f;
            if (!engaged) return;

            // 落下直後は視線が切れていてもそのまま追跡する
            Blackboard.LastSeenPosition = playerPosition;
            Blackboard.LastSeenVelocity = Vector3.zero;
            Blackboard.LastSeenTime = _time;
            Blackboard.InvestigateTarget = playerPosition;
            Blackboard.HasClue = true;
            _perception.ForceEngage();
            _confirmed = true;
            _tree.Reset();
        }

        /// <summary>見えている間は距離に応じて進行方向へ先回りし、見失った直後は予測地点へ向かう</summary>
        /// <returns>追跡目標</returns>
        private Vector3 InterceptTarget()
        {
            if (_input.Sight.VisibleRatio <= 0f) return PredictedPosition(Positive(_settings.chaseGraceTime, 1.5f));
            float distance = Vector3.Distance(_input.EnemyPosition, Blackboard.LastSeenPosition);
            return PredictedPosition(Mathf.Clamp01((distance - 4f) / 8f) * 1.2f, 3f);
        }

        /// <summary>最後に見た速度から水平方向の移動先を予測</summary>
        /// <param name="seconds">予測する秒数</param>
        /// <param name="maxDistance">予測で動かす最大距離</param>
        /// <returns>予測位置</returns>
        private Vector3 PredictedPosition(float seconds, float maxDistance = 6f)
        {
            Vector3 velocity = Blackboard.LastSeenVelocity;
            velocity.y = 0f;
            return Blackboard.LastSeenPosition + Vector3.ClampMagnitude(velocity * seconds, maxDistance);
        }
    }
}
