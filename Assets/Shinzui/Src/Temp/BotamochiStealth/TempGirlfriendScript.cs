using System.Collections.Generic;
using static Shinzui.Temp.BotamochiStealth.TempTalk;

namespace Shinzui.Temp.BotamochiStealth
{
    // Say("セリフ")                 1行表示して入力待ち。Say("セリフ", 2f)で2秒後に勝手に進む
    // If(条件, ...)                 条件を満たすときだけ中を流す
    // Choose(Option(...), ...)      選択肢。Optionに.When(条件) .Once() .Back()を付けられる
    // Do(処理) End()
    // 条件は Has.Document("id")

    /// <summary>
    /// 彼女の会話の台本。会話や分岐を足すときはここに書く
    /// </summary>
    public static class TempGirlfriendScript
    {
        /// <summary>
        /// その階で話しかけたときの台本を返す
        /// </summary>
        public static TempTalkStep[] Build(int floor)
        {
            // 階ごとの会話を書いたら case 1: return Floor1(); のように足す
            switch (floor)
            {
                default: return PlaceholderFloor(floor);
            }
        }

        private static TempTalkStep[] PlaceholderFloor(int floor) => new[]
        {
            Say($"彼女『（仮）{floor}-1』"),
            Say($"彼女『（仮）{floor}-2』"),
            Choose(CommonOptions(floor)),
        };

        // どの階でも出る選択肢
        private static IEnumerable<TempTalkOption> CommonOptions(int floor)
        {
            yield return Option("（仮）帰る",
                Say($"彼女『（仮）{floor}-帰る』", 2f),
                End());

            // ドキュメントに反応させたいときはここに足す
            yield return Option("（仮）ドキュメントのことを聞く",
                    Say("彼女『（仮）document_001』"))
                .When(Has.Document("document_001")).Once().Back();
        }
    }
}
