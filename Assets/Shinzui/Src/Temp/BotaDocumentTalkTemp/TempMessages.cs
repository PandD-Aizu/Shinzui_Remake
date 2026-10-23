namespace Shinzui.Temp
{
    /// <summary>
    /// 画面に出す文言をまとめる場所
    /// 会話のセリフは各キャラの台本（TempGirlfriendScriptなど）にある
    /// </summary>
    public static class TempMessages
    {
        public static class Talk
        {
            public const string ChoicePrompt = "（仮）どうする？";
        }

        public static class Girlfriend
        {
            public const string Prompt = "[E] 話しかける（仮）";
            public const string ReturnToModern = "（仮）現代トンネルへ戻される……";
        }

        public static class EscapeKey
        {
            public const string Prompt = "[E] 鍵を拾う（仮）";
            public const string PickedUp = "（仮）古びた鍵を手に入れた";
        }

        public static class EscapeExit
        {
            public const string Prompt = "[E] 出口を調べる（仮）";
            public const string Locked = "（仮）鍵がかかっていて開かない……";
            public const string Unlocked = "（仮）古びた鍵で扉を開けた";
            public const string Cleared = "（仮）トンネルから脱出した";
        }
    }
}
