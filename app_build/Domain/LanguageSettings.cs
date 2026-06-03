namespace Shinzui.Domain.Settings
{
    /// <summary>
    /// 言語および字幕に関連する設定データを保持するドメインモデル
    /// </summary>
    [System.Serializable]
    public class LanguageSettings
    {
        /// <summary>音声言語 (例: "ja" = 日本語, "en" = 英語)</summary>
        public string VoiceLanguage { get; set; } = "ja";

        /// <summary>表示テキスト言語 (例: "ja" = 日本語, "en" = 英語)</summary>
        public string DisplayLanguage { get; set; } = "ja";

        /// <summary>字幕の全体表示有無</summary>
        public bool ShowSubtitles { get; set; } = true;
    }
}
