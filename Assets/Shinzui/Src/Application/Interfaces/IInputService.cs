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

        /// <summary>
        /// 左クリックなどの攻撃・投擲入力
        /// </summary>
        bool AttackPressed { get; }

        /// <summary>
        /// 攻撃・投擲入力（左クリック等）が押し続けられているか
        /// </summary>
        bool AttackHeld { get; }
    }

    /// <summary>
    /// プレイヤーの現在位置および現在のトンネル境界情報を取得するための抽象インターフェース
    /// </summary>
    public interface IPlayerTracker
    {
        /// <summary>
        /// プレイヤーの現在位置座標
        /// </summary>
        Vector3 PlayerPosition { get; }

        /// <summary>
        /// プレイヤーの現在いるトンネルの両端座標。情報が取得できない場合は null
        /// </summary>
        (Vector3 start, Vector3 end)? CurrentTunnelBounds { get; }
    }
}
