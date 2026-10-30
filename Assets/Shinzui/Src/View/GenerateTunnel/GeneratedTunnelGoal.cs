using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>
    /// プレイヤーが開錠済みゴールへ触れたときに結果画面を開く
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GeneratedTunnelGoal : MonoBehaviour
    {
        public bool IsTransitioning { get; private set; }
        public bool IsUnlocked { get; private set; }

        /// <summary>
        /// 鍵でドアを開けた後にゴールへの進入を許可する
        /// </summary>
        public void Unlock()
        {
            IsUnlocked = true;
        }

        /// <summary>
        /// プレイヤーの接触のみ受け付け、重複したシーン遷移を防ぐ
        /// </summary>
        /// <param name="other">ゴールへ触れた当たり判定</param>
        private void OnTriggerEnter(Collider other)
        {
            PlayerView player = other.GetComponentInParent<PlayerView>();
            if (!IsUnlocked || IsTransitioning || player == null || player.gameObject.scene != gameObject.scene) return;

            // プレイヤーと同じスコープの結果画面へ接続する
            var result = player.GetComponent<StageResultTrigger>();
            if (result != null) IsTransitioning = result.TryComplete();
        }

        /// <summary>
        /// 開錠時にすでに判定内にいたプレイヤーにも対応する
        /// </summary>
        /// <param name="other">判定内の当たり判定</param>
        private void OnTriggerStay(Collider other)
        {
            OnTriggerEnter(other);
        }
    }
}
