namespace Shinzui.Domain.Settings
{
    /// <summary>
    /// 音響全般に関連する設定データを保持するドメインモデル
    /// </summary>
    [System.Serializable]
    public class AudioSettings
    {
        /// <summary>ボイス音量 (0.0 ~ 1.0)</summary>
        public float VoiceVolume { get; set; } = 1.0f;

        /// <summary>BGM音量 (0.0 ~ 1.0)</summary>
        public float BgmVolume { get; set; } = 0.8f;

        /// <summary>SE音量 (0.0 ~ 1.0)</summary>
        public float SeVolume { get; set; } = 1.0f;

        /// <summary>システム音量 (0.0 ~ 1.0)</summary>
        public float SystemVolume { get; set; } = 0.8f;

        /// <summary>ダイナミックレンジコントロールの有無</summary>
        public bool EnableDynamicRangeControl { get; set; } = false;

        /// <summary>スピーカータイプ設定 (0: ヘッドホン, 1: ステレオスピーカー, 2: サラウンドシステム)</summary>
        public int SpeakerType { get; set; } = 0;

        /// <summary>バーチャルサラウンドの有効化</summary>
        public bool EnableVirtualSurround { get; set; } = false;
    }
}
