namespace Shinzui.Temp
{
    /// <summary>
    /// 画面に出す文言をまとめる場所
    /// 会話のセリフは各キャラの台本（TempGirlfriendScriptなど）にある
    /// </summary>
    public static class TempMessages
    {
        public static class SpecialItem
        {
            public const string Prompt = "[E] 拾う（仮）";
            public const string Examine = "（仮）特殊アイテムです";
            public const string PickedUp = "（仮）特殊アイテムです";
        }

        public static class Talk
        {
            public const string ChoicePrompt = "（仮）どうする？";
        }

        public static class Girlfriend
        {
            public const string Prompt = "[E] 話しかける（仮）";
            public const string ReturnToModern = "（仮）現代トンネルへ戻される……";
        }
    }
}
