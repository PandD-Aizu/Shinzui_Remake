namespace Shinzui.Domain.Settings
{
    /// <summary>
    /// ゲームプレイ全般に関する設定データを保持するドメインモデル
    /// </summary>
    [System.Serializable]
    public class GameplaySettings
    {
        /// <summary>照準アシスト強度 (0: OFF, 1: 弱, 2: 中, 3: 強)</summary>
        public int AimAssistStrength { get; set; } = 2;

        /// <summary>被ダメージ時の画面赤明やエフェクト表現の有無</summary>
        public bool EnableDamageExpression { get; set; } = true;

        /// <summary>チュートリアルポップアップ等の表示有無</summary>
        public bool ShowTutorial { get; set; } = true;

        /// <summary>UI/HUD全体の表示有無</summary>
        public bool ShowHUD { get; set; } = true;

        /// <summary>画面中央の照準の表示有無</summary>
        public bool ShowReticle { get; set; } = true;

        /// <summary>照準の色設定 (0: 白, 1: 赤, 2: 緑, 3: 青, 4: 黄)</summary>
        public int ReticleColorIndex { get; set; } = 0;
    }
}
