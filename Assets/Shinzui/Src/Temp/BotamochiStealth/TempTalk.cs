using System;
using System.Collections;
using System.Collections.Generic;

namespace Shinzui.Temp.BotamochiStealth
{
    /// <summary>
    /// 台本の1手順。台本はこれを並べた配列
    /// </summary>
    public delegate IEnumerator TempTalkStep(TempTalkCharacter host);

    /// <summary>
    /// 選択肢1つ分。When・Once・Backをつなげて書ける
    /// </summary>
    public sealed class TempTalkOption
    {
        public readonly string Text;
        public readonly TempTalkStep[] Steps;
        public Func<bool> Condition { get; private set; }
        public bool IsOnce { get; private set; }
        public bool BackToChoices { get; private set; }

        public TempTalkOption(string text, TempTalkStep[] steps)
        {
            Text = text;
            Steps = steps;
        }

        /// <summary>
        /// 条件を満たすときだけ選択肢に出す
        /// </summary>
        public TempTalkOption When(Func<bool> condition)
        {
            Condition = condition;
            return this;
        }

        /// <summary>
        /// 1回選んだら次から出ない（セッション中だけ覚えてる）
        /// </summary>
        public TempTalkOption Once()
        {
            IsOnce = true;
            return this;
        }

        /// <summary>
        /// 選んだ後、中身を流し終えたらまた同じ選択肢に戻る
        /// </summary>
        public TempTalkOption Back()
        {
            BackToChoices = true;
            return this;
        }
    }

    /// <summary>
    /// 会話の台本を書くための部品（Say・If・Choose・Option・Do・End）と条件（Has）
    /// 台本側で using static して使う
    /// </summary>
    public static class TempTalk
    {
        private static readonly HashSet<string> s_chosenOnce = new();

        /// <summary>
        /// Once()で選んだ記録を消す
        /// </summary>
        public static void ResetChosen() => s_chosenOnce.Clear();

        /// <summary>
        /// セリフを1行出して入力待ち。autoAdvanceSecondsを入れるとその秒数で勝手に進む
        /// </summary>
        public static TempTalkStep Say(string text, float autoAdvanceSeconds = 0.0f) => host => host.ShowLine(text, autoAdvanceSeconds);

        /// <summary>
        /// 条件を満たすときだけ中の手順を流す
        /// </summary>
        public static TempTalkStep If(Func<bool> condition, params TempTalkStep[] then) => host => condition() ? RunAll(then, host) : Nothing();

        /// <summary>
        /// 好きな処理を1回呼ぶ
        /// </summary>
        public static TempTalkStep Do(Action action) => host =>
        {
            action();
            return Nothing();
        };

        /// <summary>
        /// 会話を終わらせる。この後の手順は流れない
        /// </summary>
        public static TempTalkStep End() => host =>
        {
            host.EndTalk();
            return Nothing();
        };

        public static TempTalkOption Option(string text, params TempTalkStep[] steps) => new TempTalkOption(text, steps);

        /// <summary>
        /// 選択肢を出して、選ばれたものの中身を流す
        /// </summary>
        public static TempTalkStep Choose(params TempTalkOption[] options) => host => RunChoose(options, host);

        public static TempTalkStep Choose(IEnumerable<TempTalkOption> options) => Choose(new List<TempTalkOption>(options).ToArray());

        /// <summary>
        /// 手順を上から順に流す。End()されたらそこで止まる
        /// </summary>
        public static IEnumerator RunAll(IReadOnlyList<TempTalkStep> steps, TempTalkCharacter host)
        {
            if (steps == null) yield break;
            foreach (TempTalkStep step in steps)
            {
                if (host.IsEnded) yield break;
                if (step != null) yield return step(host);
            }
        }

        // 出せる選択肢だけ並べて選ばせる。Back()付きならまた選択肢に戻る
        private static IEnumerator RunChoose(TempTalkOption[] options, TempTalkCharacter host)
        {
            while (!host.IsEnded)
            {
                var visible = new List<TempTalkOption>();
                foreach (TempTalkOption option in options)
                {
                    if (option == null || (option.IsOnce && s_chosenOnce.Contains(option.Text))) continue;
                    if (option.Condition == null || option.Condition()) visible.Add(option);
                }
                if (visible.Count == 0) yield break;

                int picked = -1;
                yield return host.ShowChoices(visible.ConvertAll(o => o.Text), i => picked = i);
                if (picked < 0) yield break;

                TempTalkOption chosen = visible[picked];
                if (chosen.IsOnce) s_chosenOnce.Add(chosen.Text);
                yield return RunAll(chosen.Steps, host);

                if (!chosen.BackToChoices) yield break;
            }
        }

        // 待たずにすぐ終わる手順
        private static IEnumerator Nothing()
        {
            yield break;
        }

        /// <summary>
        /// 台本で使う条件
        /// </summary>
        public static class Has
        {
            // documentIdはTempDocumentItemのDocument Id
            public static Func<bool> Document(string documentId) => () => TempDocumentCollector.IsCollected(documentId);
        }
    }
}
