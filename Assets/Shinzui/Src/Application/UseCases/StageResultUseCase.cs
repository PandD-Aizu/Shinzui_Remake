using System;
using System.Collections.Generic;
using R3;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.Domain.ValueObjects.StageResults;

namespace Shinzui.Application.UseCases
{
    /// <summary>一回のステージの統計を集計してクリア時に固定する</summary>
    public sealed class StageResultUseCase : IDisposable
    {
        private readonly Dictionary<int, EnemyIntentType> _intents = new();
        private readonly IDisposable _itemSubscription;
        private readonly SpecialItemUseCase _specialItems;
        private int _chases;
        private int _preventions;
        private int _items;
        private double _startedAt;
        private bool _started;
        private StageResult _result;

        /// <summary>取得と身代わり消費の通知を購読する</summary>
        /// <param name="inventory">アイテム取得通知元</param>
        /// <param name="specialItems">身代わり消費通知元</param>
        public StageResultUseCase(InventoryUseCase inventory, SpecialItemUseCase specialItems)
        {
            // ロード時のスロット復元は取得として数えない
            _itemSubscription = inventory.OnItemGot.Subscribe(_ => RecordItem());
            _specialItems = specialItems;
            _specialItems.DeathPreventionConsumed += RecordPrevention;
        }

        /// <summary>シーン内のプレイヤー開始時刻を記録する</summary>
        /// <param name="scaledTime">ポーズを除いた累積秒数</param>
        public void Start(double scaledTime)
        {
            // 同一ステージの二重開始を防ぐ
            if (_started) return;
            _started = true;
            _startedAt = scaledTime;
        }

        /// <summary>敵ごとのChaseへの遷移だけを数える</summary>
        /// <param name="enemyId">敵の識別子</param>
        /// <param name="intent">現在の行動意図</param>
        public void ObserveIntent(int enemyId, EnemyIntentType intent)
        {
            // 同じChaseが継続するフレームは加算しない
            if (!_started || _result != null) return;
            _intents.TryGetValue(enemyId, out var previous);
            if (intent == EnemyIntentType.Chase && previous != EnemyIntentType.Chase) _chases++;
            _intents[enemyId] = intent;
        }

        /// <summary>成功したアイテム取得を記録する</summary>
        private void RecordItem()
        {
            // クリア後の非同期通知は結果に含めない
            if (_started && _result == null) _items++;
        }

        /// <summary>成功した身代わり消費を記録する</summary>
        private void RecordPrevention()
        {
            // 所持変更や消費失敗では通知されない
            if (_started && _result == null) _preventions++;
        }

        /// <summary>ゴール到達時の統計と評価を一度だけ確定する</summary>
        /// <param name="scaledTime">到達時の累積秒数</param>
        /// <param name="rules">評価条件</param>
        /// <returns>確定した結果</returns>
        public StageResult Complete(double scaledTime, StageResultRules rules)
        {
            // 再入場や複数コライダーでも同じ結果を返す
            return _result ??= new StageResult(new StageResultStats(scaledTime - _startedAt,
                _chases, _preventions, _items), rules);
        }

        /// <summary>シーン終了時に通知の購読を解放する</summary>
        public void Dispose()
        {
            // 次のステージへ通知を持ち越さない
            _itemSubscription.Dispose();
            _specialItems.DeathPreventionConsumed -= RecordPrevention;
        }
    }
}
