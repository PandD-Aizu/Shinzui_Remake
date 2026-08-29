using System;
using System.Threading.Tasks;
using R3;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.Domain.ValueObjects.Inventory;

namespace Shinzui.Application.UseCases.Interaction
{
    /// <summary>
    /// プレイヤーのインタラクト実行およびメッセージテロップ表示、アイテム取得を仲介するユースケース
    /// </summary>
    public class InteractionUseCase
    {
        private const string SpiderWebInteractionId = "spider_web";
        private const string MatchStickItemId = "match_stick";

        private readonly InventoryUseCase _inventoryUseCase;
        private readonly SpecialItemUseCase _specialItemUseCase;
        private readonly IItemCatalog _itemCatalog;
        private readonly Subject<string> _onShowMessage = new();

        /// <summary>
        /// 画面下部にメッセージテロップを表示するイベント
        /// </summary>
        public Observable<string> OnShowMessage => _onShowMessage;

        public InteractionUseCase(
            InventoryUseCase inventoryUseCase,
            SpecialItemUseCase specialItemUseCase,
            IItemCatalog itemCatalog)
        {
            _inventoryUseCase = inventoryUseCase;
            _specialItemUseCase = specialItemUseCase;
            _itemCatalog = itemCatalog;
        }

        /// <summary>
        /// 指定されたインタラクトIDとメッセージ内容に応じてインタラクト処理を実行
        /// </summary>
        /// <param name="interactableId">インタラクト対象のID</param>
        /// <param name="message">インスペクター等で指定された表示メッセージ</param>
        public async Task<bool> InteractAsync(string interactableId, string message)
        {
            if (string.IsNullOrEmpty(interactableId)) return false;

            // 蜘蛛の巣は、装備中のマッチ棒を1本消費した場合だけ燃やせる。
            if (string.Equals(interactableId, SpiderWebInteractionId, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(
                        _inventoryUseCase.EquippedItemId.CurrentValue,
                        MatchStickItemId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _onShowMessage.OnNext("火をつけるにはマッチ棒を装備する必要がある。");
                    return false;
                }

                if (!await _inventoryUseCase.ConsumeEquippedItemAsync())
                {
                    return false;
                }

                _onShowMessage.OnNext(string.IsNullOrEmpty(message)
                    ? "蜘蛛の巣に火をつけた。"
                    : message);
                return true;
            }

            // アイテム取得の処理
            if (interactableId.StartsWith("ItemTest_"))
            {
                string itemId = interactableId.Substring(9); // "ItemTest_"の文字数分スキップしてItemIdを取得
                
                // カタログからアイテム情報を非同期で取得
                var item = await _itemCatalog.GetItemAsync(itemId);
                
                bool success = false;
                if (item != null && item.Type == ItemType.Special)
                {
                    var result = await _specialItemUseCase.AcquireAsync(itemId);
                    success = result.Succeeded;
                }
                else
                {
                    // インベントリへアイテムを追加
                    success = await _inventoryUseCase.AddItemAsync(itemId, 1);
                }
                
                if (success)
                {
                    // カスタムメッセージが指定されていない場合は、デフォルトの取得メッセージを表示
                    if (string.IsNullOrEmpty(message))
                    {
                        string displayName = item != null ? item.Name : itemId;
                        _onShowMessage.OnNext($"Obtained {displayName}.");
                    }
                }

                return success;
            }
            else
            {
                // メッセージ表示処理
                // インスペクターで設定されたカスタムメッセージがあれば表示
                if (!string.IsNullOrEmpty(message))
                {
                    _onShowMessage.OnNext(message);
                }
                
                // 将来的な他のオブジェクトに対するインタラクトロジック拡張用のプレースホルダー
                
                // カタログからアイテム情報を非同期で取得
                var item = await _itemCatalog.GetItemAsync(interactableId);
                
                bool success = false;
                if (item != null && item.Type == ItemType.Special)
                {
                    var result = await _specialItemUseCase.AcquireAsync(interactableId);
                    success = result.Succeeded;
                }
                else
                {
                    // インベントリへアイテムを追加
                    success = await _inventoryUseCase.AddItemAsync(interactableId, 1);
                }
                
                if (success)
                {
                    // カスタムメッセージが指定されていない場合は、デフォルトの取得メッセージを表示
                    if (string.IsNullOrEmpty(message))
                    {
                        string displayName = item != null ? item.Name : interactableId;
                        _onShowMessage.OnNext($"Obtained {displayName}.");
                    }
                }
                
                return success;
            }
        }
    }
}
