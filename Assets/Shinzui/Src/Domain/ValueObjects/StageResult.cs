using System;
using System.Collections.Generic;

namespace Shinzui.Domain.ValueObjects.StageResults
{
    /// <summary>ステージ内で集計する指標</summary>
    public enum StageResultMetric { ClearSeconds, ChaseCount, DeathPreventions, ItemsObtained }

    /// <summary>クリア時点で確定した統計</summary>
    public readonly struct StageResultStats
    {
        public double ClearSeconds { get; }
        public int ChaseCount { get; }
        public int DeathPreventions { get; }
        public int ItemsObtained { get; }

        /// <summary>クリア時点の統計を保持する</summary>
        /// <param name="clearSeconds">クリア秒数</param>
        /// <param name="chaseCount">追跡開始回数</param>
        /// <param name="deathPreventions">身代わり消費回数</param>
        /// <param name="itemsObtained">アイテム取得通知の回数</param>
        public StageResultStats(double clearSeconds, int chaseCount, int deathPreventions, int itemsObtained)
        {
            // 結果画面中に変化しない値へ固定する
            ClearSeconds = Math.Max(0, clearSeconds);
            ChaseCount = chaseCount;
            DeathPreventions = deathPreventions;
            ItemsObtained = itemsObtained;
        }
    }

    /// <summary>表示文と評価条件を持つメダル定義</summary>
    [Serializable]
    public sealed class StageMedalRule
    {
        public string Name;
        public string ConditionText;
        public int Points;
        public StageResultMetric Metric;
        public double Threshold;
        public bool AtLeast;

        /// <summary>指標をしきい値と比較する</summary>
        /// <param name="stats">クリア時の統計</param>
        /// <returns>獲得条件を満たす場合はtrue</returns>
        public bool IsEarned(StageResultStats stats)
        {
            // 表示文から独立した条件で判定する
            double value = Metric switch
            {
                StageResultMetric.ClearSeconds => stats.ClearSeconds,
                StageResultMetric.ChaseCount => stats.ChaseCount,
                StageResultMetric.DeathPreventions => stats.DeathPreventions,
                StageResultMetric.ItemsObtained => stats.ItemsObtained,
                _ => double.NaN
            };
            return AtLeast ? value >= Threshold : value <= Threshold;
        }
    }

    /// <summary>ステージのメダルと評価境界</summary>
    public sealed class StageResultRules
    {
        public string StageName { get; }
        public IReadOnlyList<StageMedalRule> Medals { get; }
        public double SThreshold { get; }
        public double AThreshold { get; }
        public double BThreshold { get; }

        /// <summary>ステージ固有の評価条件を保持する</summary>
        /// <param name="stageName">ステージ表示名</param>
        /// <param name="medals">メダル定義</param>
        /// <param name="sThreshold">S評価の比率</param>
        /// <param name="aThreshold">A評価の比率</param>
        /// <param name="bThreshold">B評価の比率</param>
        public StageResultRules(string stageName, IReadOnlyList<StageMedalRule> medals,
            double sThreshold = 0.9, double aThreshold = 0.7, double bThreshold = 0.45)
        {
            // アセットから受け取った条件を保持する
            StageName = stageName;
            Medals = medals;
            SThreshold = sThreshold;
            AThreshold = aThreshold;
            BThreshold = bThreshold;
        }

        /// <summary>アセット未指定時の標準条件を生成する</summary>
        /// <returns>通常トンネルの評価条件</returns>
        public static StageResultRules CreateDefault()
        {
            // アセットと共通の既定値を一か所で定義する
            return new StageResultRules("トンネル - 通常", new[]
            {
                new StageMedalRule { Name = "時間メダル1", ConditionText = "15分以内にクリア", Points = 3, Metric = StageResultMetric.ClearSeconds, Threshold = 900 },
                new StageMedalRule { Name = "時間メダル2", ConditionText = "10分以内にクリア", Points = 3, Metric = StageResultMetric.ClearSeconds, Threshold = 600 },
                new StageMedalRule { Name = "隠密メダル1", ConditionText = "徘徊者の追跡を3回以下に抑えてクリア", Points = 3, Metric = StageResultMetric.ChaseCount, Threshold = 3 },
                new StageMedalRule { Name = "隠密メダル2", ConditionText = "一度も追跡されずにクリア", Points = 4, Metric = StageResultMetric.ChaseCount, Threshold = 0 },
                new StageMedalRule { Name = "不屈メダル", ConditionText = "身代わりを使わずにクリア", Points = 2, Metric = StageResultMetric.DeathPreventions, Threshold = 0 },
                new StageMedalRule { Name = "探索メダル", ConditionText = "アイテムを5個以上入手する", Points = 2, Metric = StageResultMetric.ItemsObtained, Threshold = 5, AtLeast = true }
            });
        }
    }

    /// <summary>獲得メダルと得点とランクを保持する確定結果</summary>
    public sealed class StageResult
    {
        public StageResultRules Rules { get; }
        public StageResultStats Stats { get; }
        public IReadOnlyList<bool> Earned { get; }
        public int EarnedPoints { get; }
        public int MaxPoints { get; }
        public string Rank { get; }

        /// <summary>全メダルを評価して結果を確定する</summary>
        /// <param name="stats">クリア時の統計</param>
        /// <param name="rules">評価条件</param>
        public StageResult(StageResultStats stats, StageResultRules rules)
        {
            // メダルの獲得可否と得点をまとめて求める
            Stats = stats;
            Rules = rules;
            var earned = new bool[rules.Medals.Count];
            for (int i = 0; i < earned.Length; i++)
            {
                var medal = rules.Medals[i];
                earned[i] = medal.IsEarned(stats);
                MaxPoints += medal.Points;
                if (earned[i]) EarnedPoints += medal.Points;
            }
            Earned = Array.AsReadOnly(earned);

            // 浮動小数の誤差を抑えるため比率は倍精度で計算する
            double ratio = MaxPoints > 0 ? (double)EarnedPoints / MaxPoints : 0;
            Rank = ratio >= rules.SThreshold ? "S" : ratio >= rules.AThreshold ? "A"
                : ratio >= rules.BThreshold ? "B" : "C";
        }
    }
}
