namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>位置を含まない姿勢と光の知覚情報</summary>
    public readonly struct PlayerSignature
    {
        public bool IsCrouching { get; }
        public bool IsRunning { get; }
        public bool IsMoving { get; }
        public bool FlashlightOn { get; }
        public bool IlluminatesEnemy { get; }

        /// <summary>プレイヤーの見えやすさを保持</summary>
        /// <param name="isCrouching">しゃがみ中か</param>
        /// <param name="isRunning">走行中か</param>
        /// <param name="isMoving">移動中か</param>
        /// <param name="flashlightOn">ライト点灯中か</param>
        /// <param name="illuminatesEnemy">敵を照らしているか</param>
        public PlayerSignature(bool isCrouching, bool isRunning, bool isMoving, bool flashlightOn,
            bool illuminatesEnemy = false)
        {
            IsCrouching = isCrouching;
            IsRunning = isRunning;
            IsMoving = isMoving;
            FlashlightOn = flashlightOn;
            IlluminatesEnemy = illuminatesEnemy;
        }
    }
}
