namespace Shinzui.Domain.Settings
{
    /// <summary>
    /// アクセシビリティに関連する設定データを保持するドメインモデル
    /// </summary>
    [System.Serializable]
    public class AccessibilitySettings
    {
        /// <summary>字幕等の文字サイズ (0: 小, 1: 中, 2: 大, 3: 極大)</summary>
        public int TextSizeIndex { get; set; } = 1;

        /// <summary>字幕の文字カラー設定 (0: 白, 1: 黄, 2: 緑, 3: 青)</summary>
        public int TextColorIndex { get; set; } = 0;

        /// <summary>字幕の背景（黒帯）の表示有無</summary>
        public bool ShowTextBackground { get; set; } = true;

        /// <summary>字幕の背景不透明度 (0.0 = 透明, 1.0 = 完全不透明)</summary>
        public float TextBackgroundOpacity { get; set; } = 0.5f;

        /// <summary>字幕での話者名表示（例：「プレイヤー：・・・」）の有無</summary>
        public bool ShowSpeakerName { get; set; } = true;

        /// <summary>サウンドの視覚化補助字幕を表示するかどうか</summary>
        public bool ShowSoundSubtitles { get; set; } = false;

        /// <summary>画面中央に基準点を表示して3D酔いを軽減するかどうか</summary>
        public bool ShowCenterDot { get; set; } = false;
    }
}
