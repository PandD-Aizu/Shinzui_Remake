using NUnit.Framework;
using Shinzui.Domain.ValueObjects.StageResults;

namespace Shinzui.Tests
{
    /// <summary>メダル条件と評価境界を最小限の入力で検証する</summary>
    public sealed class StageResultTests
    {
        /// <summary>全条件を満たした場合の得点と評価を検証する</summary>
        [Test]
        public void PerfectClearEarnsSeventeenPointsAndS()
        {
            // 指定された6枚すべての点数を確認する
            var result = new StageResult(new StageResultStats(600, 0, 0, 5), StageResultRules.CreateDefault());
            Assert.That(result.Earned, Is.All.True);
            Assert.That(result.EarnedPoints, Is.EqualTo(17));
            Assert.That(result.MaxPoints, Is.EqualTo(17));
            Assert.That(result.Rank, Is.EqualTo("S"));
        }

        /// <summary>メダルの境界と直後の失敗を検証する</summary>
        /// <param name="index">メダル番号</param>
        /// <param name="seconds">クリア秒数</param>
        /// <param name="chases">追跡回数</param>
        /// <param name="preventions">消費回数</param>
        /// <param name="items">取得数</param>
        /// <param name="earned">獲得するか</param>
        [TestCase(0, 900, 0, 0, 0, true)]
        [TestCase(0, 900.01, 0, 0, 0, false)]
        [TestCase(1, 600.01, 0, 0, 0, false)]
        [TestCase(2, 600, 3, 0, 0, true)]
        [TestCase(2, 600, 4, 0, 0, false)]
        [TestCase(3, 600, 1, 0, 0, false)]
        [TestCase(4, 600, 0, 1, 0, false)]
        [TestCase(5, 600, 0, 0, 4, false)]
        public void MedalUsesInclusiveBoundary(int index, double seconds, int chases, int preventions, int items, bool earned)
        {
            // 文言ではなくデータの条件で獲得を判定する
            var result = new StageResult(new StageResultStats(seconds, chases, preventions, items), StageResultRules.CreateDefault());
            Assert.That(result.Earned[index], Is.EqualTo(earned));
        }

        /// <summary>ランクの境界を比率で判定することを検証する</summary>
        /// <param name="points">100点中の獲得点</param>
        /// <param name="rank">期待するランク</param>
        [TestCase(90, "S")]
        [TestCase(89, "A")]
        [TestCase(70, "A")]
        [TestCase(69, "B")]
        [TestCase(45, "B")]
        [TestCase(44, "C")]
        public void RankUsesInclusiveRatios(int points, string rank)
        {
            // 取得済みと未取得の配点を変えて境界の前後を作る
            var rules = new StageResultRules("境界テスト", new[]
            {
                new StageMedalRule { Points = points, Metric = StageResultMetric.ChaseCount, Threshold = 0 },
                new StageMedalRule { Points = 100 - points, Metric = StageResultMetric.ItemsObtained, Threshold = 1, AtLeast = true }
            });
            Assert.That(new StageResult(default, rules).Rank, Is.EqualTo(rank));
        }

        /// <summary>ステージ固有のしきい値と配点ゼロを検証する</summary>
        [Test]
        public void CustomThresholdsAndEmptyMedalsAreRespected()
        {
            // 17点中15点を標準のS境界より低い設定でSにする
            var defaults = StageResultRules.CreateDefault();
            var rules = new StageResultRules("別のステージ", defaults.Medals, 0.8, 0.6, 0.3);
            Assert.That(new StageResult(new StageResultStats(600, 0, 0, 0), rules).Rank, Is.EqualTo("S"));

            // メダルが空でもゼロ除算せずC評価とする
            var empty = new StageResultRules("空", System.Array.Empty<StageMedalRule>());
            Assert.That(new StageResult(default, empty).Rank, Is.EqualTo("C"));
        }
    }
}
