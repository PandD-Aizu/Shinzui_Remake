using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>
    /// プレイヤーがゴールへ触れたときにステージ選択へ遷移する
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GeneratedTunnelGoal : MonoBehaviour
    {
        private const string DestinationScene = "StageSelect";
        public bool IsTransitioning { get; private set; }

        /// <summary>
        /// プレイヤーの接触のみ受け付け、重複したシーン遷移を防ぐ
        /// </summary>
        /// <param name="other">ゴールへ触れた当たり判定</param>
        private void OnTriggerEnter(Collider other)
        {
            PlayerView player = other.GetComponentInParent<PlayerView>();
            if (IsTransitioning || player == null || player.gameObject.scene != gameObject.scene) return;

            // 遷移先の登録漏れでは操作不能にせず、原因を通知する
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(DestinationScene))
            {
                Debug.LogError("[GeneratedTunnelGoal] StageSelect must be enabled in the scene list.", this);
                return;
            }

            IsTransitioning = true;
            SceneManager.LoadSceneAsync(DestinationScene, LoadSceneMode.Single);
        }
    }
}
