using UnityEngine;
using Shinzui.View;

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>
    /// ワープ通路の中心トリガー領域コンポーネント。
    /// プレイヤーが侵入した際に対応するワープ通路へプレイヤーを移動させる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedWarpCorridorTrigger : MonoBehaviour
    {
        private const float WarpCooldown = 0.35f;
        private static float _lastWarpTime = -WarpCooldown;

        [SerializeField] private GeneratedCorridorInfo sourceCorridor;

        /// <summary>
        /// ワープ判定の起点になる通路情報を登録する。
        /// </summary>
        public void Initialize(GeneratedCorridorInfo corridor)
        {
            sourceCorridor = corridor;
        }

        /// <summary>
        /// プレイヤーが中心トリガーに入ったら、対応するワープ通路へ移動させる。
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (Time.time - _lastWarpTime < WarpCooldown)
            {
                return;
            }

            if (sourceCorridor == null || sourceCorridor.PairedCorridor == null)
            {
                return;
            }

            PlayerView player = other.GetComponentInParent<PlayerView>();
            if (player == null)
            {
                return;
            }

            Vector3 targetForward = sourceCorridor.PairedCorridor.transform.forward;
            Vector3 offset = sourceCorridor.PairedCorridor.transform.position
                             - targetForward * 1.25f
                             - sourceCorridor.transform.position;
            player.Warp(offset);
            _lastWarpTime = Time.time;
        }
    }
}
