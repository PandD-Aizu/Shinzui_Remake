using UnityEngine;

namespace Shinzui.Application.Interfaces
{
    public interface IInputService
    {
        Vector2 MoveInput { get; }
        bool SprintPressed { get; }
        bool CrouchPressed { get; }
        
        /// <summary>
        /// インベントリ開閉トグル用入力（Tabキーなど）
        /// </summary>
        bool InventoryTogglePressed { get; }

        /// <summary>
        /// アイテム使用入力（Eキーなど）
        /// </summary>
        bool ItemUsePressed { get; }

        /// <summary>
        /// プレイヤーの移動やカメラ回転入力（Playerアクションマップ）の入力をブロックするか制御します。
        /// </summary>
        /// <param name="blocked">true の場合、移動や視点移動の入力を無効化します。</param>
        void SetBlocked(bool blocked);

        /// <summary>
        /// 懐中電灯トグル用入力（Fキーなど）
        /// </summary>
        bool FlashlightTogglePressed { get; }
    }
}
