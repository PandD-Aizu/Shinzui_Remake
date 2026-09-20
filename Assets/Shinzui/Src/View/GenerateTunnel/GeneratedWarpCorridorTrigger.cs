using System;
using UnityEngine;
using Shinzui.View;

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>
    /// ワープ通路の中心トリガー領域コンポーネント。
    /// プレイヤーの侵入イベントのみを公開する。ワープ制御はPresenterが担当する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedWarpCorridorTrigger : MonoBehaviour
    {
        [SerializeField] private GeneratedCorridorInfo sourceCorridor;
        public event Action<GeneratedCorridorInfo, PlayerView> PlayerEntered;

        /// <summary>
        /// ワープ判定の起点になる通路情報を登録する。
        /// </summary>
        public void Initialize(GeneratedCorridorInfo corridor)
        {
            sourceCorridor = corridor;
        }

        /// <summary>
        /// プレイヤーが中心トリガーに入ったことを通知する。
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            PlayerView player = other.GetComponentInParent<PlayerView>();
            if (player != null) PlayerEntered?.Invoke(sourceCorridor, player);
        }
    }
}
