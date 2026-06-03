namespace Shinzui.Domain.Settings
{
    /// <summary>
    /// カメラ操作に関連する設定データを保持するドメインモデル
    /// </summary>
    [System.Serializable]
    public class CameraSettings
    {
        /// <summary>カメラ操作方向設定（通常時） (0: 通常, 1: 左右反転, 2: 上下反転, 3: 上下左右反転)</summary>
        public int NormalCameraInversion { get; set; } = 0;

        /// <summary>カメラ操作方向設定（構え時） (0: 通常, 1: 左右反転, 2: 上下反転, 3: 上下左右反転)</summary>
        public int AimCameraInversion { get; set; } = 0;

        /// <summary>カメラ視点以外／メニュー操作方向 (0: 通常, 1: 反転)</summary>
        public int MenuOperationInversion { get; set; } = 0;

        /// <summary>コントローラー用カメラ操作速度（通常時） (1.0 ~ 100.0)</summary>
        public float ControllerNormalSpeed { get; set; } = 50.0f;

        /// <summary>コントローラー用カメラ操作速度（構え時） (1.0 ~ 100.0)</summary>
        public float ControllerAimSpeed { get; set; } = 30.0f;

        /// <summary>コントローラー用カメラ回転速度（通常時） (1.0 ~ 100.0)</summary>
        public float ControllerNormalRotationSpeed { get; set; } = 50.0f;

        /// <summary>コントローラー用カメラ回転速度（構え時） (1.0 ~ 100.0)</summary>
        public float ControllerAimRotationSpeed { get; set; } = 30.0f;

        /// <summary>【PC専用】マウス用カメラ操作感度（通常時） (1.0 ~ 100.0)</summary>
        public float MouseNormalSensitivity { get; set; } = 50.0f;

        /// <summary>【PC専用】マウス用カメラ操作感度（構え時） (1.0 ~ 100.0)</summary>
        public float MouseAimSensitivity { get; set; } = 30.0f;

        /// <summary>【PC専用】UI/メニュー操作でのマウス感度 (1.0 ~ 100.0)</summary>
        public float MouseMenuSensitivity { get; set; } = 50.0f;
    }
}
