namespace Shinzui.Domain.Settings
{
    /// <summary>
    /// 操作関連の設定データを保持するドメインモデル
    /// </summary>
    [System.Serializable]
    public class ControlsSettings
    {
        /// <summary>コントローラーの振動有無</summary>
        public bool EnableVibration { get; set; } = true;

        /// <summary>コントローラーのボタンレイアウト設定 (0: デフォルト, 1: タイプA, 2: タイプB ...)</summary>
        public int ControllerLayoutType { get; set; } = 0;

        /// <summary>【PC専用】マウスカーソルをゲームウィンドウ内に固定するかどうか</summary>
        public bool LockCursorToWindow { get; set; } = true;

        /// <summary>【PC専用】マウスクリックの左右反転</summary>
        public bool InvertMouseClick { get; set; } = false;

        /// <summary>【PC専用】マウスホイールの回転方向反転</summary>
        public bool InvertMouseWheel { get; set; } = false;
    }
}
