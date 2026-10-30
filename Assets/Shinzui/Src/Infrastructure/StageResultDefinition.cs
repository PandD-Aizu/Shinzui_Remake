using System.Linq;
using Shinzui.Domain.ValueObjects.StageResults;
using UnityEngine;

namespace Shinzui.Infrastructure
{
    /// <summary>ステージごとの結果表示名とメダル条件とランク境界を保存する</summary>
    [CreateAssetMenu(menuName = "Shinzui/Stage Result Definition")]
    public sealed class StageResultDefinition : ScriptableObject
    {
        [SerializeField] private string stageDisplayName = "トンネル - 通常";
        [SerializeField] private StageMedalRule[] medals = StageResultRules.CreateDefault().Medals.ToArray();
        [SerializeField, Range(0f, 1f)] private double sThreshold = 0.9;
        [SerializeField, Range(0f, 1f)] private double aThreshold = 0.7;
        [SerializeField, Range(0f, 1f)] private double bThreshold = 0.45;

        /// <summary>Unityに依存しない評価条件へ変換する</summary>
        /// <returns>ステージの評価条件</returns>
        public StageResultRules ToRules()
        {
            // アセットの未設定時は共通の標準条件へ戻す
            if (medals == null || medals.Length == 0) return StageResultRules.CreateDefault();
            return new StageResultRules(stageDisplayName, medals, sThreshold, aThreshold, bThreshold);
        }
    }
}
